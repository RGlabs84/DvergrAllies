using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DvergrAllies
{
    // Snapshots the living population and writes both BarrkBOT export files.
    //
    // The census reads the ZDO DATABASE rather than walking instantiated GameObjects. On a server that
    // database holds every object in the world, loaded or not, so a Dvergr standing in a zone no player
    // has visited for a week is still counted - where FindObjectsOfType would only ever have seen the
    // handful currently rendered near somebody. History (births, deaths, contracts) is not derivable
    // from a scan and lives in DvergrStatsStore instead.
    public class DvergrStatsExporter : MonoBehaviour
    {
        // Public so DvergrProcreation/RecruiterManager can stamp the same keys.
        public const string OwnerIdKey = "dvergr_owner_id";
        public const string OwnerNameKey = "dvergr_owner_name";
        public const string TameCountedKey = "dvergr_tame_counted";

        // Heuristic tame-time owner search: "the player standing close enough to have fed it". Not
        // exposed as a config option - this is an approximation, not a gameplay tuning knob.
        private const float OwnerSearchRadius = 15f;

        // The walk touches every ZDO in the world, which on a long-lived server is six figures. Spread
        // it over frames so the export never shows up as a stutter.
        private const int ZdosPerFrame = 2000;

        private DateTime m_sessionStart;
        private string m_exportDir;

        private Dictionary<int, string> m_classByPrefabHash;
        private int m_prefabMapBuiltFromCount = -1;

        private string m_lastStatus;

        private void Awake()
        {
            m_sessionStart = DateTime.UtcNow;
            m_exportDir = Path.Combine(Paths.ConfigPath, "DvergrAllies");

            // Unconditional, not LogDebug. Every other line this class emits is a warning, so a
            // healthy exporter used to be indistinguishable in the log from one that was never
            // constructed at all - which is precisely the state 1.0.4 and 1.0.5 shipped in. One line
            // at startup makes "enabled but not running" impossible to mistake for "running quietly".
            Jotunn.Logger.LogInfo(
                $"[StatsExport] Armed. Enable Stats Export = {ConfigManager.EnableStatsExport.Value}; " +
                $"writing to {m_exportDir} once this process is confirmed to be the server.");

            StartCoroutine(ExportLoop());
        }

        // Logs only when the answer to "is the exporter doing anything?" changes, so a server that
        // idles for hours costs one line rather than one per minute - but never stays silent about
        // why nothing is being written.
        private void LogStatus(string status)
        {
            if (status == m_lastStatus) return;
            m_lastStatus = status;
            Jotunn.Logger.LogInfo("[StatsExport] " + status);
        }

        private IEnumerator ExportLoop()
        {
            while (true)
            {
                float interval = Mathf.Max(30f, ConfigManager.StatsExportInterval.Value);
                yield return new WaitForSeconds(interval);

                if (!ConfigManager.EnableStatsExport.Value)
                {
                    LogStatus("Idle: 'Enable Stats Export' is off, so no files are being written.");
                    continue;
                }

                // An exception escaping the coroutine body kills the coroutine outright, and it never
                // restarts - the exporter would be dead for the rest of the session with nothing but
                // a Unity-log entry that BepInEx does not write to disk by default. Catch it, say so,
                // and come back next cycle.
                bool ready = false;
                string prepFailure = null;
                try
                {
                    DvergrStatsStore.EnsureReady();
                    ready = DvergrStatsStore.IsServer();
                }
                catch (Exception e)
                {
                    prepFailure = e.ToString();
                }

                if (prepFailure != null)
                {
                    Jotunn.Logger.LogWarning(
                        $"[StatsExport] Could not prepare the counter store this cycle; retrying in {interval:0}s: {prepFailure}");
                    continue;
                }

                if (!ready)
                {
                    LogStatus("Waiting: no world is loaded yet, or this process is not the server. " +
                              "Nothing is written until it is.");
                    continue;
                }

                LogStatus($"Running: exporting every {interval:0}s to {m_exportDir}.");
                yield return StartCoroutine(CensusAndWrite());
            }
        }

        // Maps every Dvergr-ish creature prefab hash to the class name we report it as. Mirrors the
        // "name contains dverg + is a creature" rule used by Patch_ZNetScene_Awake_Dvergrs, so any
        // Ashlands or third-party Dvergr the taming patch picks up is counted here too, rather than a
        // hardcoded list that silently under-reports the moment another mod adds one.
        private void EnsurePrefabMap()
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null || scene.m_prefabs == null) return;
            if (m_classByPrefabHash != null && m_prefabMapBuiltFromCount == scene.m_prefabs.Count) return;

            var map = new Dictionary<int, string>();
            foreach (GameObject prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;
                if (prefab.name.IndexOf("dverg", StringComparison.OrdinalIgnoreCase) < 0) continue;
                // Weapons and staves are named after them too ("AllyDvergr_SwordBronze",
                // "DvergerStaffHeal"); only actual creatures carry a Humanoid.
                if (prefab.GetComponent<Humanoid>() == null) continue;

                map[prefab.name.GetStableHashCode()] = DvergrStatsSchema.ClassifyPrefabName(prefab.name);
            }

            m_classByPrefabHash = map;
            m_prefabMapBuiltFromCount = scene.m_prefabs.Count;
        }

        private IEnumerator CensusAndWrite()
        {
            EnsurePrefabMap();

            // Bailing out here used to write nothing at all, which made "the exporter is broken and
            // never ran" and "the exporter ran but could not count" identical on disk - both just an
            // absent file. Now the file is always written and carries the reason, so a MISSING file
            // means one unambiguous thing: the exporter never executed.
            string unavailable = null;
            if (ConfigManager.ForceCensusUnavailable.Value)
                // Deliberately worded so a reader can tell a drill from a real fault, and so a server
                // owner who forgot to switch it off sees why their numbers vanished rather than
                // hunting a bug. A failure path that has only ever been exercised by fixtures is the
                // one thing you cannot prove works; this is how it gets exercised in anger.
                unavailable = "forced by the 'Force Census Unavailable' config switch - this is a deliberate test of the failure path, not a real fault";
            else if (ZNetScene.instance == null)
                unavailable = "world scene is not loaded yet";
            else if (m_classByPrefabHash == null || m_classByPrefabHash.Count == 0)
                unavailable = "no Dvergr creature prefabs are registered - prefab setup has not run, or another mod replaced them";
            else if (ZDOMan.instance == null)
                unavailable = "the object database is not available";

            if (unavailable != null)
            {
                Jotunn.Logger.LogWarning($"[StatsExport] Census unavailable: {unavailable}. " +
                                         "Writing exports with census fields nulled.");
                Write(new DvergrWorldSnapshot(), new List<DvergrPlayerSnapshot>(), unavailable);
                yield break;
            }

            var world = new DvergrWorldSnapshot();
            var byPlayer = new Dictionary<string, DvergrPlayerSnapshot>();

            // Snapshot the values up front: the walk yields between batches, and ZDOMan mutates freely
            // in those gaps, which would invalidate a live enumerator mid-iteration. Taking the copy
            // races that same mutation, so it is guarded - an exception escaping here would kill the
            // coroutine permanently rather than costing a single cycle.
            List<ZDO> zdos;
            try
            {
                zdos = ZDOMan.instance.m_objectsByID.Values.ToList();
            }
            catch (Exception e)
            {
                zdos = null;
                Jotunn.Logger.LogWarning($"[StatsExport] Could not snapshot the object database this cycle: {e.Message}");
            }

            if (zdos == null)
            {
                Write(new DvergrWorldSnapshot(), new List<DvergrPlayerSnapshot>(),
                    "the object database could not be read this cycle");
                yield break;
            }

            for (int i = 0; i < zdos.Count; i++)
            {
                if (i > 0 && i % ZdosPerFrame == 0) yield return null;

                ZDO zdo = zdos[i];
                if (zdo == null) continue;

                if (!m_classByPrefabHash.TryGetValue(zdo.GetPrefab(), out string className)) continue;
                // Wild Mistlands Dvergr share these prefabs and vastly outnumber ours - only allies count.
                if (!zdo.GetBool(ZDOVars.s_tamed, false)) continue;
                // Valkyrie's Cargo's merchant is a tamed Dverger on this very prefab, and nobody's ally.
                if (ValkyriesCargoCompat.IsIngvar(zdo)) continue;

                bool isPregnant = zdo.GetLong("pregnant", 0L) != 0L;
                bool isHungry = IsHungry(zdo);
                int genderInt = zdo.GetInt("dvergr_gender", -1);

                world.AliveTamed++;
                if (isPregnant) world.Pregnant++;
                if (isHungry) world.Hungry++;
                if (genderInt == (int)DvergrGenetics.Gender.Male) world.Male++;
                else if (genderInt == (int)DvergrGenetics.Gender.Female) world.Female++;
                else world.GenderUnknown++;
                Bump(world.ClassCounts, className);

                long ownerId = zdo.GetLong(OwnerIdKey, 0L);
                if (ownerId == 0L) continue; // Tamed before owner tracking existed - unattributed.

                string ownerIdStr = ownerId.ToString();
                if (!byPlayer.TryGetValue(ownerIdStr, out DvergrPlayerSnapshot playerSnap))
                {
                    playerSnap = new DvergrPlayerSnapshot
                    {
                        PlayerId = ownerIdStr,
                        Name = zdo.GetString(OwnerNameKey, "Unknown")
                    };
                    byPlayer[ownerIdStr] = playerSnap;
                }

                playerSnap.AliveTamed++;
                if (isPregnant) playerSnap.PregnantOwned++;
                if (isHungry) playerSnap.HungryOwned++;
                Bump(playerSnap.ClassCounts, className);
            }

            Write(world, byPlayer.Values, null);
        }

        // Prefers the live component when the Dvergr happens to be loaded, because that is the same
        // value the player sees on the hover text. Falls back to deriving it from the ZDO for the
        // unloaded majority, using the configured fed duration (which is what MakePrefabTamable
        // assigned to m_fedDuration in the first place).
        private static bool IsHungry(ZDO zdo)
        {
            ZNetView instance = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(zdo) : null;
            if (instance != null)
            {
                Tameable tameable = instance.GetComponent<Tameable>();
                if (tameable != null) return tameable.IsHungry();
            }

            long ticks = zdo.GetLong(ZDOVars.s_tameLastFeeding, 0L);
            if (ticks <= 0L) return true; // No feeding ever recorded.

            try
            {
                double elapsed = (ZNet.instance.GetTime() - new DateTime(ticks)).TotalSeconds;
                return elapsed > ConfigManager.FedDuration.Value;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Garbage tick value (older save, another mod writing the same key). Unknown, not fed.
                return true;
            }
        }

        private void Write(DvergrWorldSnapshot world, IEnumerable<DvergrPlayerSnapshot> players,
            string censusUnavailableReason)
        {
            try
            {
                string source = $"DvergrAllies {Plugin.PluginVersion}";
                DateTime now = DateTime.UtcNow;
                DvergrLifetimeStats lifetime = DvergrStatsStore.Current;

                string worldJson = DvergrStatsSchema.BuildWorldJson(world, lifetime, source, now, m_sessionStart,
                    Mathf.RoundToInt(ConfigManager.StatsExportInterval.Value), censusUnavailableReason);
                string playersJson = DvergrStatsSchema.BuildPlayersJson(players, lifetime, source, now, m_sessionStart,
                    censusUnavailableReason);

                Directory.CreateDirectory(m_exportDir);
                DvergrStatsStore.WriteAtomic(Path.Combine(m_exportDir, "barrkbot_dvergr_world.json"), worldJson);
                DvergrStatsStore.WriteAtomic(Path.Combine(m_exportDir, "barrkbot_dvergr_players.json"), playersJson);

                DvergrStatsStore.Save();

                ConfigManager.LogDebug($"[StatsExport] {world.AliveTamed} alive tamed across the world; " +
                                       $"lifetime {lifetime?.Tamed ?? 0} tamed / {lifetime?.Deaths ?? 0} deaths.");
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[StatsExport] Export failed, skipping this cycle: {e}");
            }
        }

        private static void Bump(Dictionary<string, int> dict, string key)
        {
            dict.TryGetValue(key, out int current);
            dict[key] = current + 1;
        }

        // ---- Ownership stamping ----

        public static void StampOwner(ZDO zdo, long playerId, string playerName)
        {
            if (zdo == null || playerId == 0L) return;
            zdo.Set(OwnerIdKey, playerId);
            zdo.Set(OwnerNameKey, string.IsNullOrEmpty(playerName) ? "Unknown" : playerName);
        }

        public static bool TryGetOwner(ZDO zdo, out long playerId, out string playerName)
        {
            playerId = 0L;
            playerName = "Unknown";
            if (zdo == null) return false;

            playerId = zdo.GetLong(OwnerIdKey, 0L);
            if (playerId == 0L) return false;

            playerName = zdo.GetString(OwnerNameKey, "Unknown");
            return true;
        }

        // Called from the SetTamed postfix. Assigns the nearest player as owner, but only if nothing
        // has claimed this Dvergr already - BirthingProcess and the contract summon both stamp an owner
        // explicitly, and those should always win over this proximity guess.
        public static void AssignOwnerOnTame(Character character, ZNetView nview)
        {
            ZDO zdo = nview.GetZDO();
            if (zdo.GetLong(OwnerIdKey, 0L) != 0L) return;

            Player nearest = null;
            float nearestDist = OwnerSearchRadius;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                float dist = Vector3.Distance(player.transform.position, character.transform.position);
                if (dist <= nearestDist)
                {
                    nearest = player;
                    nearestDist = dist;
                }
            }

            if (nearest == null) return;
            StampOwner(zdo, nearest.GetPlayerID(), nearest.GetPlayerName());
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.SetTamed))]
    public static class Patch_Character_SetTamed_OwnerTracking
    {
        [HarmonyPostfix]
        public static void Postfix(Character __instance, bool tamed)
        {
            if (!tamed || __instance == null || __instance.GetComponent<DvergrGenetics>() == null) return;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
            // LOAD-BEARING: Valkyrie's Cargo's CargoMerchant.Awake calls SetTamed(true) from inside VC's
            // Humanoid.Awake postfix, BEFORE our components have reached Awake, so on the pilot's client this
            // runs while DvergrGenetics is still on Ingvar and the check above passes. Without this line he
            // gets an owner tag and a counted tame stamped onto a ZDO VC owns, and BarrkBOT hears of a tame.
            if (ValkyriesCargoCompat.IsIngvar(nview)) return;

            DvergrStatsExporter.AssignOwnerOnTame(__instance, nview);

            // SetTamed re-fires on reload and on re-taming, so the count is gated on a flag stored in
            // the ZDO itself - which persists with the creature and replicates, unlike an in-memory set
            // that would reset every restart and recount the whole population.
            ZDO zdo = nview.GetZDO();
            if (zdo.GetInt(DvergrStatsExporter.TameCountedKey, 0) != 0) return;
            zdo.Set(DvergrStatsExporter.TameCountedKey, 1);

            DvergrStatsExporter.TryGetOwner(zdo, out long ownerId, out string ownerName);
            DvergrStatsStore.ReportTame(
                DvergrStatsSchema.ClassifyPrefabName(__instance.gameObject.name.Replace("(Clone)", "").Trim()),
                ownerId, ownerName);
        }
    }

    // Prefix, not postfix: OnDeath tears the creature down and drops its ZDO, so the owner tag has to
    // be read while it still exists.
    //
    // Targets Character.OnDeath, which is where the method is actually DECLARED. 1.0.4 and 1.0.5
    // targeted Humanoid on the belief that Humanoid overrides it - it does not; Player is the only
    // override in the game. nameof(Humanoid.OnDeath) still compiles because the member is inherited,
    // so the mistake survived to runtime, where Harmony looks for a DECLARED method, found none, and
    // threw out of PatchAll - killing every patch class after this one and the rest of Plugin.Awake
    // with it. See the patch loop in Plugin for why one bad target can no longer do that.
    [HarmonyPatch(typeof(Character), "OnDeath")]
    public static class Patch_Humanoid_OnDeath_Stats
    {
        [HarmonyPrefix]
        public static void Prefix(Character __instance)
        {
            if (__instance == null || __instance.GetComponent<DvergrGenetics>() == null) return;
            if (!__instance.IsTamed()) return; // Wild Dvergr kills are not our losses.

            ZNetView nview = __instance.GetComponent<ZNetView>();
            // The owner gate keeps exactly one machine reporting: every other client also runs OnDeath
            // locally, and without this each of them would report the same death.
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
            if (ValkyriesCargoCompat.IsIngvar(nview)) return; // Valkyrie's Cargo's merchant: tamed, not ours.

            DvergrStatsExporter.TryGetOwner(nview.GetZDO(), out long ownerId, out string ownerName);
            DvergrStatsStore.ReportDeath(
                DvergrStatsSchema.ClassifyPrefabName(__instance.gameObject.name.Replace("(Clone)", "").Trim()),
                ownerId, ownerName);
        }
    }

    // The 60s export tick is also what flushes the counter file, so a graceful shutdown in between
    // would otherwise drop up to a minute of history. Prefix so ZNet (and the world UID the file is
    // named for) is still alive when we write.
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
    public static class Patch_ZNet_Shutdown_SaveStats
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            DvergrStatsStore.Save(force: true);
        }
    }
}
