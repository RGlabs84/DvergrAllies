// DvergrAllies's checks. All of them run in a real single-player world on the real client, with the mod DLL under test.
//   A  boot (main menu): plugin loaded at the expected version, every Harmony target patched, "[Patches] N applied, none failed".
//   P  prefabs: every wild Dvergr prefab carries the mod's components, every Ally prefab is Players, the contract item exists.
//   T  tame faction: a wild Dvergr stays Dverger until tamed, is Players while tamed, goes back if untamed; the setting turns it
//      off; Valkyrie's Cargo's Ingvar (stripped by the mod) is never moved.
//   H  player hits: the game's own hit rule (enemy OR aggravatable) lands on a wild Dverger, not on a tamed one or a tamed
//      Ally; a real player hit on a wild one does not aggravate a tame one beside it; tamed-while-angry is cleared.
//   C  the contract: consuming a Dvergr Contract summons a tamed Players-faction Ally.
//   B  ballistas: asks the game's own BaseAI.FindClosestCreature exactly as Turret.UpdateTarget does, then lets a real ballista
//      with ammo pick for itself. A player-built ballista must pass over tame Dvergr, a wild one must stay a target, and a ballista
//      nobody built (a Dvergr outpost's) must be left alone.
//   The negative ballista checks only mean something if the scene can see a wild Dvergr at all, so B1 gates the rest: if the
//   control fails, the others are SKIP (NOT ASSESSED), never a pass.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Bootstrap;
using DvergrAllies;
using HarmonyLib;
using UnityEngine;

namespace DvergrAlliesAutoTest
{
    public partial class AutoTest
    {
        const string ModGuid = "wubarrk.dvergrallies";

        // ------------------------------------------------------------------------------------------ helpers

        static Character Ch(GameObject go) => go != null ? go.GetComponent<Character>() : null;

        static string Name(Character c) => c != null ? c.gameObject.name.Replace("(Clone)", "") : "none";

        /// <summary>A Dvergr stood on the strip with its AI switched off, so it neither wanders out of a ballista's view
        /// nor goes hunting during a check. Components still run (the faction sync is a separate behaviour).</summary>
        static GameObject Stand(string prefab, Vector3 flatPos, bool freeze = true)
        {
            GameObject go = Spawn(prefab, OnGround(flatPos) + Vector3.up * 0.3f, Quaternion.LookRotation(-Fwd));
            if (go == null) { Log($"Stand: prefab {prefab} not found"); return null; }
            if (freeze) { var ai = go.GetComponent<BaseAI>(); if (ai != null) ai.enabled = false; }
            return go;
        }

        static void Tame(GameObject go)
        {
            // The same private Tameable.Tame the Haldor contract calls (RecruiterManager), and the one taming by feeding reaches.
            go.GetComponent<Tameable>().Tame();
        }

        static void Gone(params GameObject[] gos)
        {
            foreach (var go in gos)
                if (go != null && ZNetScene.instance != null) ZNetScene.instance.Destroy(go);
        }

        /// <summary>Waits for a body's faction, returns the seconds it took (or -1).</summary>
        static IEnumerator FactionIs(GameObject go, Character.Faction want, float timeout, Action<float> took)
        {
            float t0 = Time.realtimeSinceStartup;
            yield return WaitFor(() => Ch(go).m_faction == want, timeout);
            took(waitOk ? Time.realtimeSinceStartup - t0 : -1f);
        }

        static string Plain(Character.Faction f) => f.ToString();

        // ------------------------------------------------------------------------------------------ A: boot

        IEnumerator PhaseA()
        {
            bool loaded = Chainloader.PluginInfos.TryGetValue(ModGuid, out var info);
            Check(loaded, "plugin loaded", loaded ? $"{ModGuid} {info.Metadata.Version}" : "not in Chainloader.PluginInfos");
            if (loaded)
            {
                string expect = Environment.GetEnvironmentVariable("DA_EXPECT_VERSION");
                string have = info.Metadata.Version.ToString();
                if (string.IsNullOrEmpty(expect)) Result("SKIP", "version matches the manifest", "DA_EXPECT_VERSION not set");
                else Check(have == expect, "version matches the manifest", $"plugin {have}, manifest {expect}");
            }

            var targets = new[]
            {
                (typeof(BaseAI), "FindClosestCreature"), (typeof(Character), "SetTamed"), (typeof(MonsterAI), "UpdateAI"),
                (typeof(ZNetScene), "Awake"), (typeof(Trader), "Start"), (typeof(Tameable), "Awake"),
            };
            foreach (var (type, method) in targets)
            {
                var m = AccessTools.Method(type, method);
                var pi = m != null ? Harmony.GetPatchInfo(m) : null;
                int mine = 0;
                if (pi != null)
                    foreach (var p in pi.Prefixes.Concat(pi.Postfixes).Concat(pi.Finalizers))
                        if (p.owner == ModGuid) mine++;
                Check(mine > 0, $"patch {type.Name}.{method}", m == null ? "method not found" : $"{mine} DvergrAllies patch(es)");
            }

            // The mod's own summary line, written in Plugin.Awake before this driver exists, so it is read from the log file.
            string line = null;
            try
            {
                string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "LogOutput.log");
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    var m = Regex.Match(sr.ReadToEnd(), @"\[Patches\][^\r\n]*");
                    if (m.Success) line = m.Value.Trim();
                }
            }
            catch (Exception e) { Log("reading LogOutput.log: " + e.Message); }
            if (line == null) Result("FAIL", "patch summary", "no [Patches] line in LogOutput.log");
            else Check(Regex.IsMatch(line, @"\d+ applied, none failed"), "patch summary", line);
            yield break;
        }

        // ------------------------------------------------------------------------------------------ P: prefabs

        static readonly string[] BaseAllies = { "AllyDvergrRogue", "AllyDvergrMage", "AllyDvergrMageFire", "AllyDvergrMageIce", "AllyDvergrMageSupport" };

        /// <summary>The wild Dvergr prefabs the mod retrofits: the same rule as Patch_ZNetScene_Awake_Dvergrs.</summary>
        static List<GameObject> WildPrefabs() =>
            ZNetScene.instance.m_prefabs
                .Where(g => g != null && g.GetComponent<Humanoid>() != null && g.GetComponent<MonsterAI>() != null)
                .Where(g => { string n = g.name.ToLowerInvariant(); return (n.Contains("dverg") || n.Contains("dverger")) && !n.Contains("ally"); })
                .ToList();

        IEnumerator PhaseP()
        {
            var wild = WildPrefabs();
            Check(wild.Count > 0, "wild Dvergr prefabs found", $"{wild.Count}: {string.Join(", ", wild.Select(g => g.name))}");

            var missing = new List<string>();
            foreach (var g in wild)
            {
                var gaps = new List<string>();
                if (g.GetComponent<DvergrTameable>() == null) gaps.Add("DvergrTameable");
                if (g.GetComponent<DvergrFactionSync>() == null) gaps.Add("DvergrFactionSync");
                if (g.GetComponent<DvergrGenetics>() == null) gaps.Add("DvergrGenetics");
                if (g.GetComponent<DvergrProcreation>() == null) gaps.Add("DvergrProcreation");
                if (gaps.Count > 0) missing.Add($"{g.name} lacks {string.Join("+", gaps)}");
            }
            Check(missing.Count == 0, "every wild Dvergr prefab carries the mod's components",
                missing.Count == 0 ? $"{wild.Count} of {wild.Count}" : string.Join("; ", missing));

            var notWild = wild.Where(g => g.GetComponent<Character>().m_faction != Character.Faction.Dverger).Select(g => $"{g.name}={g.GetComponent<Character>().m_faction}").ToList();
            Check(notWild.Count == 0, "wild prefabs are still Dverger faction (the mod does not change the prefab)",
                notWild.Count == 0 ? $"{wild.Count} of {wild.Count}" : string.Join(", ", notWild));

            var allies = BaseAllies.Concat(AllyPrefabManager.CustomVariants.Select(v => v.PrefabName)).ToList();
            var bad = new List<string>();
            foreach (string n in allies)
            {
                GameObject g = ZNetScene.instance.GetPrefab(n);
                if (g == null) { bad.Add(n + " missing"); continue; }
                if (g.GetComponent<DvergrTameable>() == null) bad.Add(n + " has no DvergrTameable");
                if (g.GetComponent<Character>().m_faction != Character.Faction.Players) bad.Add($"{n} is {g.GetComponent<Character>().m_faction}");
                if (g.GetComponent<DvergrFactionSync>() != null) bad.Add(n + " carries a faction sync it does not need");
            }
            Check(bad.Count == 0, "every Ally prefab is Players faction, tameable", bad.Count == 0 ? $"{allies.Count} of {allies.Count}" : string.Join("; ", bad));

            GameObject contract = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("DvergrContract") : null;
            Check(contract != null, "the Dvergr Contract item exists", contract != null ? "DvergrContract in ObjectDB" : "not in ObjectDB");
            yield break;
        }

        // ------------------------------------------------------------------------------------------ T: tame faction

        IEnumerator PhaseT()
        {
            Vector3 spot = Origin + Fwd * 12f;
            try
            {
                // T1: every wild prefab, tamed, then compared with what it was while wild.
                var wild = WildPrefabs();
                int ok = 0;
                var fails = new List<string>();
                foreach (var prefab in wild)
                {
                    Character.Faction wildFaction = prefab.GetComponent<Character>().m_faction;
                    GameObject go = Stand(prefab.name, spot);
                    if (go == null) { fails.Add(prefab.name + ": could not spawn"); continue; }
                    yield return Sec(2.5f);   // Start has run, and the sync has polled at least twice

                    Character c = Ch(go);
                    string why = null;
                    if (c.IsTamed()) why = "was tamed before anyone tamed it";
                    else if (c.m_faction != wildFaction) why = $"wild faction became {c.m_faction}";
                    if (why == null)
                    {
                        Tame(go);
                        float took = -1f;
                        yield return FactionIs(go, Character.Faction.Players, 4f, s => took = s);
                        if (!c.IsTamed()) why = "Tame() did not tame it";
                        else if (took < 0f) why = $"tamed but faction stayed {c.m_faction} for 4 s";
                    }
                    if (why == null) ok++; else fails.Add($"{prefab.name}: {why}");
                    Gone(go);
                    yield return Sec(0.3f);
                }
                Check(fails.Count == 0 && ok > 0, "wild stays its faction, tamed becomes Players (every wild prefab)",
                    fails.Count == 0 ? $"{ok} of {wild.Count}" : $"{ok} of {wild.Count}; " + string.Join("; ", fails));

                // T2: one Dvergr through the whole life: wild, tamed, untamed, setting off, setting on.
                GameObject d = Stand("Dverger", spot);
                if (d == null) { Result("FAIL", "spawn Dverger", "prefab not found"); yield break; }
                Character dc = Ch(d);
                yield return Sec(2.5f);
                Check(dc.m_faction == Character.Faction.Dverger && !dc.IsTamed(), "wild Dverger is Dverger and untamed",
                    $"faction {dc.m_faction}, tamed {dc.IsTamed()}");

                Tame(d);
                float t1 = -1f;
                yield return FactionIs(d, Character.Faction.Players, 4f, s => t1 = s);
                Check(t1 >= 0f && dc.IsTamed(), "tamed Dverger is Players", t1 >= 0f ? $"Players after {t1:0.0} s" : $"faction {dc.m_faction}, tamed {dc.IsTamed()}");
                Check(d.GetComponent<ZNetView>().GetZDO().GetBool(ZDOVars.s_tamed, false), "the tamed flag is in the ZDO (what every peer derives the faction from)", "s_tamed = true");

                dc.SetTamed(false);
                float t2 = -1f;
                yield return FactionIs(d, Character.Faction.Dverger, 4f, s => t2 = s);
                Check(t2 >= 0f && !dc.IsTamed(), "untamed again goes back to Dverger", t2 >= 0f ? $"Dverger after {t2:0.0} s" : $"faction {dc.m_faction}, tamed {dc.IsTamed()}");

                ConfigManager.TamedJoinPlayerFaction.Value = false;
                try
                {
                    Tame(d);
                    yield return WaitFor(() => dc.IsTamed(), 3f);
                    yield return Sec(3.5f);
                    Check(dc.IsTamed() && dc.m_faction == Character.Faction.Dverger, "setting off: tamed but stays Dverger",
                        $"tamed {dc.IsTamed()}, faction {dc.m_faction}");
                }
                finally { ConfigManager.TamedJoinPlayerFaction.Value = true; }
                float t3 = -1f;
                yield return FactionIs(d, Character.Faction.Players, 4f, s => t3 = s);
                Check(t3 >= 0f, "setting back on: the tamed Dverger joins Players", t3 >= 0f ? $"Players after {t3:0.0} s" : $"faction {dc.m_faction}");
                Gone(d);

                // T3: an Ally prefab is Players from the start, tamed or not, and stays so.
                GameObject ally = Stand("AllyDvergrWarrior", spot);
                if (ally == null) Result("FAIL", "spawn AllyDvergrWarrior", "prefab not found");
                else
                {
                    yield return Sec(2.5f);
                    Check(Ch(ally).m_faction == Character.Faction.Players && !Ch(ally).IsTamed(), "an untamed Ally prefab is Players",
                        $"faction {Ch(ally).m_faction}, tamed {Ch(ally).IsTamed()}");
                    Gone(ally);
                }

                // T4: Valkyrie's Cargo's merchant. Built the way ZNetScene builds a persisted object: a ZDO that already carries
                // the key is handed to ZNetView.m_initZDO, then the prefab is instantiated.
                Vector3 pos = OnGround(spot) + Vector3.up * 0.3f;
                GameObject prefabD = ZNetScene.instance.GetPrefab("Dverger");
                ZDO zdo = ZDOMan.instance.CreateNewZDO(pos, "Dverger".GetStableHashCode());
                zdo.Set(ValkyriesCargoCompat.IngvarKey, 1);
                ZNetView.m_initZDO = zdo;
                GameObject ing = UnityEngine.Object.Instantiate(prefabD, pos, Quaternion.LookRotation(-Fwd));
                ZNetView.m_initZDO = null;
                var iai = ing.GetComponent<BaseAI>(); if (iai != null) iai.enabled = false;
                yield return Sec(1.5f);
                bool stripped = ing.GetComponent<DvergrAlliesExcluded>() != null && ing.GetComponent<DvergrTameable>() == null
                                && ing.GetComponent<DvergrFactionSync>() == null && ing.GetComponent<DvergrGenetics>() == null;
                Check(stripped, "Ingvar is stripped of every DvergrAllies component",
                    string.Join(", ", ing.GetComponents<MonoBehaviour>().Where(m => m != null && m.GetType().Assembly == typeof(DvergrFactionSync).Assembly).Select(m => m.GetType().Name)));
                Ch(ing).SetTamed(true);   // what Valkyrie's Cargo does to him
                yield return WaitFor(() => Ch(ing).IsTamed(), 3f);
                yield return Sec(3.5f);
                Check(Ch(ing).IsTamed() && Ch(ing).m_faction == Character.Faction.Dverger, "a tamed Ingvar stays Dverger",
                    $"tamed {Ch(ing).IsTamed()}, faction {Ch(ing).m_faction}");
                Gone(ing);
            }
            finally { ConfigManager.TamedJoinPlayerFaction.Value = true; }
        }

        // ------------------------------------------------------------------------------------------ H: player hits

        /// <summary>Whether a player's hit on <paramref name="target"/> lands. This is the 1.0.21 rule, copied from Attack.cs
        /// (melee and AoE-of-attack, lines 1152-1153), Projectile.cs 548-553 and Aoe.cs 489, for a player with PVP off and a
        /// normal (not tamed-only) weapon: the hit lands if the target is an enemy OR its AI is aggravatable.</summary>
        static bool PlayerHitLands(Character target)
        {
            Player p = Player.m_localPlayer;
            BaseAI ai = target.GetBaseAI();
            bool flag = BaseAI.IsEnemy(p, target) || (ai != null && ai.IsAggravatable() && p.IsPlayer());
            return p.IsPVPEnabled() || flag;
        }

        static string HitInfo(Character c)
        {
            BaseAI ai = c.GetBaseAI();
            return $"{Name(c)}: tamed {c.IsTamed()}, faction {c.m_faction}, aggravatable {(ai != null && ai.IsAggravatable())}, " +
                   $"enemy {BaseAI.IsEnemy(Player.m_localPlayer, c)}";
        }

        IEnumerator PhaseH()
        {
            Vector3 spot = Origin + Fwd * 12f;
            Vector3 side = Vector3.Cross(Vector3.up, Fwd).normalized;
            GameObject wild = null, tame = null, ally = null;
            try
            {
                Check(!Player.m_localPlayer.IsPVPEnabled(), "the player has PVP off (the case the hit rule is about)",
                    $"PVP {Player.m_localPlayer.IsPVPEnabled()}");

                wild = Stand("Dverger", spot - side * 3f);
                tame = Stand("Dverger", spot);
                ally = Stand("AllyDvergrRogue", spot + side * 3f);
                if (wild == null || tame == null || ally == null) { Result("FAIL", "spawn the H scene", "a prefab was not found"); yield break; }
                yield return Sec(3.5f);   // CombatAI.Start's 1 s and 3 s re-forces on the Ally have run
                Tame(tame);
                Tame(ally);
                yield return WaitFor(() => Ch(tame).IsTamed() && Ch(ally).IsTamed() && !Ch(tame).GetBaseAI().IsAggravatable(), 4f);

                Character w = Ch(wild), t = Ch(tame), a = Ch(ally);
                Check(PlayerHitLands(w), "H1 control: a player's hit lands on a wild Dverger", HitInfo(w));
                Check(!PlayerHitLands(t), "H2 a player's hit does not land on a tamed Dverger", HitInfo(t));
                Check(!PlayerHitLands(a), "H3 a player's hit does not land on a tamed Ally", HitInfo(a));

                // H4: a real player hit on the wild one runs Character.RPC_Damage, which calls BaseAI.AggravateAllInArea(20 m)
                // and Alert()s everything aggravatable there. The tame one 3 m away must not be pulled in.
                // AggravateAllInArea walks BaseAI.BaseAIInstances, which a disabled BaseAI leaves (OnDisable), so Stand's
                // frozen AI would be invisible to it: switch both on for the hit window.
                w.GetBaseAI().enabled = true;
                t.GetBaseAI().enabled = true;
                HitData hit = new HitData();
                hit.m_damage.m_blunt = 1f;
                hit.m_point = wild.transform.position + Vector3.up;
                hit.m_dir = Fwd;
                hit.SetAttacker(Player.m_localPlayer);
                w.Damage(hit);
                yield return Sec(1.5f);   // IsAggravated() re-reads the ZDO at most once a second
                bool wildAngry = w.GetBaseAI().IsAggravated();
                Check(wildAngry, "H4 control: the hit wild Dverger is aggravated", $"aggravated {wildAngry}, alerted {w.GetBaseAI().IsAlerted()}");
                // Alerted is allowed: vanilla IsEnemy makes an aggravated Dverger an enemy of anything tamed, so the tame one
                // turning on the angry wild one is an ally doing its job. What must not happen is it joining the anger.
                bool tameZ = tame.GetComponent<ZNetView>().GetZDO().GetBool(ZDOVars.s_aggravated);
                Character tameTarget = (t.GetBaseAI() as MonsterAI)?.GetTargetCreature();
                Check(wildAngry && !tameZ && !t.GetBaseAI().IsAggravated() && !(tameTarget is Player),
                    "H5 the tamed Dverger beside it is not aggravated and does not target the player",
                    $"saved aggravated {tameZ}, alerted {t.GetBaseAI().IsAlerted()}, target {Name(tameTarget)}");
                w.GetBaseAI().enabled = false;
                t.GetBaseAI().enabled = false;

                // H6: the setting off reproduces the reported bug (vanilla: a tamed Dverger stays aggravatable), so H2 is the fix.
                ConfigManager.TamedJoinPlayerFaction.Value = false;
                try
                {
                    yield return WaitFor(() => t.GetBaseAI().IsAggravatable(), 3f);
                    Check(PlayerHitLands(t), "H6 setting off: the player's hit lands on the tamed Dverger again (the reported bug)", HitInfo(t));
                }
                finally { ConfigManager.TamedJoinPlayerFaction.Value = true; }
                yield return WaitFor(() => !t.GetBaseAI().IsAggravatable(), 3f);
                Check(!PlayerHitLands(t), "H7 setting back on: the hit stops landing", HitInfo(t));
                Gone(tame, ally);
                tame = ally = null;

                // H8: tamed while angry (the video: punched, then tamed). The saved aggravation is cleared, and if it is ever
                // untamed it comes back neutral and hittable, not angry.
                Check(wild.GetComponent<ZNetView>().GetZDO().GetBool(ZDOVars.s_aggravated), "H8 setup: the wild Dverger is saved as aggravated before taming",
                    $"saved aggravated {wild.GetComponent<ZNetView>().GetZDO().GetBool(ZDOVars.s_aggravated)}");
                Tame(wild);
                // The sync polls once a second: wait for it to act, not for a flag that may already read false.
                yield return WaitFor(() => w.IsTamed() && !w.GetBaseAI().m_aggravatable
                                           && !wild.GetComponent<ZNetView>().GetZDO().GetBool(ZDOVars.s_aggravated), 4f);
                Check(waitOk && w.IsTamed() && !PlayerHitLands(w), "H8 a Dverger tamed while aggravated: saved aggravation cleared, not hittable",
                    $"saved aggravated {wild.GetComponent<ZNetView>().GetZDO().GetBool(ZDOVars.s_aggravated)}; {HitInfo(w)}");
                w.SetTamed(false);
                yield return WaitFor(() => w.GetBaseAI().IsAggravatable(), 4f);
                yield return Sec(1.2f);
                Check(waitOk && !w.GetBaseAI().IsAggravated() && PlayerHitLands(w), "H9 untamed again: hittable, and not angry",
                    $"aggravated {w.GetBaseAI().IsAggravated()}; {HitInfo(w)}");
            }
            finally
            {
                ConfigManager.TamedJoinPlayerFaction.Value = true;
                Gone(wild, tame, ally);
            }
        }

        // ------------------------------------------------------------------------------------------ L: leash, fear, stay (1.0.14)

        static MonsterAI MAI(GameObject go) => go != null ? go.GetComponent<MonsterAI>() : null;

        IEnumerator PhaseL()
        {
            Vector3 side = Vector3.Cross(Vector3.up, Fwd).normalized;
            Vector3 home = Origin + Fwd * 6f;
            GameObject wild = null, tame = null, warrior = null, foe = null;
            float leash = ConfigManager.MaxFollowLeash.Value;
            try
            {
                // L1-L2: the hurt-and-stuck flee is off for a tamed Dvergr, on (vanilla) for a wild one, and the setting brings it back.
                wild = Stand("Dverger", home - side * 3f, freeze: false);
                tame = Stand("Dverger", home + side * 3f, freeze: false);
                if (wild == null || tame == null) { Result("FAIL", "spawn the L scene", "Dverger not found"); yield break; }
                Tame(tame);
                yield return WaitFor(() => Ch(tame).IsTamed() && !MAI(tame).m_fleeIfHurtWhenTargetCantBeReached, 3f);
                Check(waitOk, "L1 a tamed Dverger does not flee when hurt and stuck",
                    $"tamed {Ch(tame).IsTamed()}, flee flag {MAI(tame).m_fleeIfHurtWhenTargetCantBeReached}");
                Check(MAI(wild).m_fleeIfHurtWhenTargetCantBeReached, "L2 control: a wild Dverger keeps vanilla's flee",
                    $"flee flag {MAI(wild).m_fleeIfHurtWhenTargetCantBeReached}");
                ConfigManager.FleeWhenHurtAndStuck.Value = true;
                try
                {
                    yield return WaitFor(() => MAI(tame).m_fleeIfHurtWhenTargetCantBeReached, 2f);
                    Check(waitOk, "L2b setting on: the tamed one flees like vanilla again", $"flee flag {MAI(tame).m_fleeIfHurtWhenTargetCantBeReached}");
                }
                finally { ConfigManager.FleeWhenHurtAndStuck.Value = false; }
                Gone(wild); wild = null;

                // L3-L4: following the player, put it past the leash with a target: the target is dropped and STAYS dropped
                // while it is outside 60% of the leash (the old leash called SetTarget(null), a vanilla no-op).
                MonsterAI ai = MAI(tame);
                ai.SetFollowTarget(Player.m_localPlayer.gameObject);
                Vector3 far = Player.m_localPlayer.transform.position + Fwd * (leash + 12f);
                // The foe stands NEAR the player: vanilla already drops a tamed creature's target when the TARGET is more
                // than m_alertRange from its leader or post (MonsterAI.cs:280-291), so a far foe would pass without the leash.
                foe = Stand("Greyling", Player.m_localPlayer.transform.position + Fwd * 8f + side * 4f);
                if (foe == null) { Result("FAIL", "spawn the leash foe", "Greyling not found"); yield break; }
                tame.transform.position = OnGround(far) + Vector3.up * 0.3f;
                yield return Sec(0.3f);
                ai.m_targetCreature = Ch(foe);
                ai.SetAlerted(true);
                yield return WaitFor(() => ai.m_targetCreature == null, 2f);
                Check(waitOk, "L3 following: past the leash, the target is dropped",
                    $"{Utils.DistanceXZ(tame.transform.position, Player.m_localPlayer.transform.position):F1} m out, leash {leash:F0} m, target {Name(ai.m_targetCreature)}");
                bool reacquired = false;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 3f)
                {
                    if (Utils.DistanceXZ(tame.transform.position, Player.m_localPlayer.transform.position) > leash * 0.6f && ai.m_targetCreature != null) reacquired = true;
                    yield return null;
                }
                float now = Utils.DistanceXZ(tame.transform.position, Player.m_localPlayer.transform.position);
                Check(!reacquired, "L4 it does not pick the fight back up while still outside 60% of the leash (3 s)",
                    $"re-acquired {reacquired}; now {now:F1} m out");
                Check(now < leash + 12f - 1f, "L4b it is heading back to the player", $"{now:F1} m out after 3 s (started {leash + 12f:F0} m)");

                // L5: told to stay (vanilla's command: no follow, patrol point here), the leash is measured from the post.
                ai.SetFollowTarget(null);
                tame.transform.position = OnGround(home + side * 3f) + Vector3.up * 0.3f;
                yield return Sec(0.3f);
                ai.SetPatrolPoint();
                Vector3 post = tame.transform.position;
                yield return Sec(0.5f);
                foe.transform.position = OnGround(post + Fwd * 8f) + Vector3.up * 0.3f;   // near the post, as in L3
                tame.transform.position = OnGround(post + Fwd * (leash + 12f)) + Vector3.up * 0.3f;
                yield return Sec(0.3f);
                ai.m_targetCreature = Ch(foe);
                ai.SetAlerted(true);
                yield return WaitFor(() => ai.m_targetCreature == null, 2f);
                Check(waitOk, "L5 on a stay post: past the leash from the post, the target is dropped",
                    $"{Utils.DistanceXZ(tame.transform.position, post):F1} m from the post, target {Name(ai.m_targetCreature)}");

                // L6: inside the leash it fights as before (the leash must not make it passive).
                tame.transform.position = OnGround(post + side * 2f) + Vector3.up * 0.3f;
                yield return Sec(1.5f);
                ai.m_targetCreature = Ch(foe);
                yield return Sec(0.5f);
                Check(ai.m_targetCreature != null, "L6 control: inside the leash a target is kept", $"target {Name(ai.m_targetCreature)}");

                // L7: a melee Ally on a stay post keeps the prefab's idle wander pace (was forced to 0 s: a new spot every frame).
                warrior = Stand("AllyDvergrWarrior", home - side * 3f, freeze: false);
                if (warrior == null) { Result("FAIL", "spawn AllyDvergrWarrior", "prefab not found"); yield break; }
                yield return Sec(3.5f);   // CombatAI.Start's 1 s and 3 s re-forces have run
                float interval = MAI(warrior).m_randomMoveInterval;
                Check(interval >= 5f, "L7 a melee Ally's idle wander interval is not forced to 0", $"m_randomMoveInterval {interval:F1} s");
            }
            finally
            {
                ConfigManager.FleeWhenHurtAndStuck.Value = false;
                Gone(wild, tame, warrior, foe);
            }
        }

        // ------------------------------------------------------------------------------------------ C: the contract

        static int AllyCount() => Character.GetAllCharacters().Count(c => c != null && c.gameObject.name.StartsWith("AllyDvergr"));

        IEnumerator PhaseC()
        {
            Player p = Player.m_localPlayer;
            yield return PlaceAt(Origin, Fwd);
            Inventory inv = p.GetInventory();
            ItemDrop.ItemData item = inv.AddItem("DvergrContract", 1, 1, 0, 0L, "", false);
            if (item == null) { Result("FAIL", "contract into the inventory", "Inventory.AddItem returned null"); yield break; }
            Result("PASS", "contract into the inventory", item.m_shared.m_name);

            int before = AllyCount();
            bool consumed = p.ConsumeItem(inv, item);
            Check(consumed, "the contract is consumed", consumed ? "ConsumeItem true" : "ConsumeItem refused it");
            if (!consumed) yield break;

            yield return WaitFor(() => AllyCount() > before, 5f);
            Check(waitOk, "an Ally Dvergr appears", $"{before} -> {AllyCount()}");
            if (!waitOk) yield break;

            Character ally = Character.GetAllCharacters().First(c => c != null && c.gameObject.name.StartsWith("AllyDvergr") && !c.IsPlayer() && c.IsTamed());
            yield return Sec(2f);
            Check(ally.IsTamed() && ally.m_faction == Character.Faction.Players, "the summoned Ally is tamed and Players",
                $"{Name(ally)}: tamed {ally.IsTamed()}, faction {ally.m_faction}");
            foreach (var c in Character.GetAllCharacters().Where(c => c != null && c.gameObject.name.StartsWith("AllyDvergr")).ToList()) Gone(c.gameObject);
        }

        // ------------------------------------------------------------------------------------------ B: ballistas

        /// <summary>BaseAI.FindClosestCreature exactly as Turret.UpdateTarget calls it (no trophy target set).</summary>
        static Character Ask(Turret t) =>
            BaseAI.FindClosestCreature(t.transform, t.m_eye.transform.position, 0f, t.m_viewDistance, t.m_horizontalAngle,
                alerted: false, mistVision: false, passiveAggresive: true, t.m_targetPlayers, t.m_targetTamed, t.m_targetEnemies, t.m_targetCharacters);

        static GameObject TurretPrefab(out string candidates)
        {
            var c = ZNetScene.instance.m_prefabs.Where(g => g != null && g.GetComponent<Turret>() != null).ToList();
            candidates = string.Join(", ", c.Select(g => g.name));
            return c.FirstOrDefault(g => g.name == "piece_turret") ?? c.FirstOrDefault(g => g.GetComponent<Piece>() != null);
        }

        IEnumerator PhaseB()
        {
            GameObject tp = TurretPrefab(out string cands);
            if (tp == null) { Result("FAIL", "a ballista prefab exists", "no prefab with a Turret component; candidates: " + cands); yield break; }
            Turret tdef = tp.GetComponent<Turret>();
            Result("INFO", "ballista prefab defaults", $"{tp.name} (candidates: {cands}): targetPlayers {tdef.m_targetPlayers}, targetTamed {tdef.m_targetTamed}, " +
                $"targetEnemies {tdef.m_targetEnemies}, targetTamedConfig {tdef.m_targetTamedConfig}, view {tdef.m_viewDistance} m, angle {tdef.m_horizontalAngle}, maxAmmo {tdef.m_maxAmmo}");

            yield return PlaceAt(Origin - Fwd * 4f, Fwd);
            Vector3 tpos = OnGround(Origin + Fwd * 3f);
            float d = Mathf.Min(6f, tdef.m_viewDistance * 0.6f);
            Vector3 at(float dist, float side = 0f) => tpos + Fwd * dist + Vector3.Cross(Vector3.up, Fwd) * side;

            GameObject built = PlacePiece(tp.name, tpos + Vector3.up * 0.05f, Quaternion.LookRotation(Fwd));    // SetCreator: player-built
            GameObject notBuilt = null;
            var toClean = new List<GameObject> { built };
            try
            {
                if (built == null) { Result("FAIL", "place a ballista", $"{tp.name} could not be spawned"); yield break; }
                Turret t = built.GetComponent<Turret>();
                Piece piece = built.GetComponent<Piece>();
                Check(piece != null && piece.IsPlacedByPlayer(), "the placed ballista counts as player-built", piece != null ? $"creator {piece.GetCreator()}" : "no Piece");
                yield return Sec(1f);

                // The game's own "Everything" mode, forced, so the premise is explicit whatever the prefab ships with.
                t.m_targetPlayers = false; t.m_targetEnemies = true; t.m_targetTamed = true; t.m_targetTamedConfig = false;

                // B1 (control): a wild Dverger in view must be the pick. If not, the scene is wrong and the negatives are void.
                GameObject wild = Stand("Dverger", at(d));
                yield return Sec(1f);
                Character pick = Ask(t);
                bool control = pick != null && pick.gameObject == wild;
                Check(control, "B1 control: a wild Dverger in view is the ballista's pick", $"picked {Name(pick)}; turret at {V(t.transform.position)}, target at {V(wild.transform.position)}");
                Gone(wild);
                if (!control)
                {
                    foreach (string n in new[] { "B2 tame Dverger is passed over", "B3 tame nearer + wild farther: the wild one", "B4 setting off: vanilla picks the tame one",
                                                 "B5 untamed Ally is passed over", "B6 tamed Ally is passed over", "B7 not player-built: tame still a target", "B8 tamed Ingvar is not spared", "B9 the real ballista" })
                        Result("SKIP", n, "the control (B1) could not see a wild Dvergr, so this check would mean nothing");
                    yield break;
                }

                // B2: tamed wild-body Dvergr, ballista in Everything mode.
                GameObject tame1 = Stand("Dverger", at(d)); Tame(tame1);
                yield return WaitFor(() => Ch(tame1).IsTamed(), 3f);
                yield return Sec(1.5f);
                pick = Ask(t);
                Check(pick == null, "B2 a tame Dverger is passed over (everything mode)", $"picked {Name(pick)}; tamed {Ch(tame1).IsTamed()}, faction {Ch(tame1).m_faction}");

                // B4: the same scene with the setting off must pick the tame one: this is the game's own behaviour, and it shows B2 is the mod.
                ConfigManager.SpareAlliesFromBallistas.Value = false;
                try
                {
                    pick = Ask(t);
                    Check(pick != null && pick.gameObject == tame1, "B4 setting off: vanilla picks the tame Dverger (the reported bug)", $"picked {Name(pick)}");
                }
                finally { ConfigManager.SpareAlliesFromBallistas.Value = true; }

                // B3: a tame one nearer, a wild one farther, both in view: the ballista must pick the wild one, not go blind.
                Gone(tame1);
                GameObject nearTame = Stand("Dverger", at(d * 0.5f, 0.6f)); Tame(nearTame);
                GameObject farWild = Stand("Dverger", at(d, -0.6f));
                yield return WaitFor(() => Ch(nearTame).IsTamed(), 3f);
                yield return Sec(1.5f);
                pick = Ask(t);
                Check(pick != null && pick.gameObject == farWild, "B3 tame nearer + wild farther: the wild one is picked", $"picked {Name(pick)} (wild {V(farWild.transform.position)}, tame {V(nearTame.transform.position)})");
                Gone(nearTame, farWild);

                // B5 / B6: an Ally prefab, untamed (console spawn, another mod's spawner) and tamed.
                GameObject a1 = Stand("AllyDvergrWarrior", at(d));
                yield return Sec(1.5f);
                pick = Ask(t);
                Check(pick == null, "B5 an untamed Players-faction Ally is passed over", $"picked {Name(pick)}; tamed {Ch(a1).IsTamed()}, faction {Ch(a1).m_faction}");
                Tame(a1);
                yield return WaitFor(() => Ch(a1).IsTamed(), 3f);
                yield return Sec(1f);
                pick = Ask(t);
                Check(pick == null, "B6 a tamed Ally is passed over", $"picked {Name(pick)}");
                Gone(a1);

                // B8: Ingvar, tamed by Valkyrie's Cargo, has none of our components and is not spared.
                Vector3 ipos = OnGround(at(d)) + Vector3.up * 0.3f;
                ZDO zdo = ZDOMan.instance.CreateNewZDO(ipos, "Dverger".GetStableHashCode());
                zdo.Set(ValkyriesCargoCompat.IngvarKey, 1);
                ZNetView.m_initZDO = zdo;
                GameObject ing = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("Dverger"), ipos, Quaternion.LookRotation(-Fwd));
                ZNetView.m_initZDO = null;
                var iai = ing.GetComponent<BaseAI>(); if (iai != null) iai.enabled = false;
                Ch(ing).SetTamed(true);
                yield return Sec(1.5f);
                pick = Ask(t);
                Check(pick != null && pick.gameObject == ing, "B8 a tamed Ingvar is not spared (he is not ours)", $"picked {Name(pick)}");
                Gone(ing);

                // B7: a ballista nobody built, like a Dvergr outpost's: it is not ours to change.
                notBuilt = Spawn(tp.name, tpos + Fwd * 0f + Vector3.right * 0f + Vector3.up * 0.05f, Quaternion.LookRotation(Fwd));
                toClean.Add(notBuilt);
                if (notBuilt != null)
                {
                    Turret t2 = notBuilt.GetComponent<Turret>();
                    t2.m_targetPlayers = false; t2.m_targetEnemies = true; t2.m_targetTamed = true; t2.m_targetTamedConfig = false;
                    Piece p2 = notBuilt.GetComponent<Piece>();
                    Gone(built); toClean.Remove(built); built = null;    // one ballista in the scene at a time
                    GameObject tame2 = Stand("Dverger", at(d)); Tame(tame2);
                    yield return WaitFor(() => Ch(tame2).IsTamed(), 3f);
                    yield return Sec(1.5f);
                    pick = Ask(t2);
                    Check(p2 != null && !p2.IsPlacedByPlayer() && pick != null && pick.gameObject == tame2,
                        "B7 a ballista nobody built still targets tame Dvergr", $"built-by-player {(p2 != null ? p2.IsPlacedByPlayer().ToString() : "no Piece")}, picked {Name(pick)}");
                    Gone(tame2, notBuilt); toClean.Remove(notBuilt); notBuilt = null;
                }
                else Result("FAIL", "B7 spawn an unbuilt ballista", "Spawn returned null");

                // B9: the real thing. A ballista with ammo picks for itself (Turret.UpdateTarget, then RPC_SetTarget), first with a
                // tame Dvergr alone in view, then with a wild one beside it.
                GameObject real = PlacePiece(tp.name, tpos + Vector3.up * 0.05f, Quaternion.LookRotation(Fwd));
                toClean.Add(real);
                Turret rt = real.GetComponent<Turret>();
                rt.m_targetPlayers = false; rt.m_targetEnemies = true; rt.m_targetTamed = true; rt.m_targetTamedConfig = false;
                ZDO rz = real.GetComponent<ZNetView>().GetZDO();
                if (rt.m_maxAmmo != 0)
                {
                    string ammo = rt.m_allowedAmmo.Count > 0 && rt.m_allowedAmmo[0].m_ammo != null ? rt.m_allowedAmmo[0].m_ammo.gameObject.name : null;
                    if (ammo != null) { rz.Set(ZDOVars.s_ammo, rt.m_maxAmmo); rz.Set(ZDOVars.s_ammoType, ammo); }
                }
                if (!rt.HasAmmo()) Result("SKIP", "B9 the real ballista", $"could not load it with ammo (maxAmmo {rt.m_maxAmmo}, ammo {rt.GetAmmo()})");
                else
                {
                    GameObject tame3 = Stand("Dverger", at(d)); Tame(tame3);
                    yield return WaitFor(() => Ch(tame3).IsTamed(), 3f);
                    bool everTargeted = false;
                    float until = Time.realtimeSinceStartup + 6f;
                    while (Time.realtimeSinceStartup < until)
                    {
                        rt.m_updateTargetTimer = 0f;
                        if (rt.m_target != null && rt.m_target.gameObject == tame3) everTargeted = true;
                        yield return Sec(0.5f);
                    }
                    Check(!everTargeted, "B9a the real ballista never targets a lone tame Dverger (6 s)", everTargeted ? "it targeted the tame Dverger" : $"target stayed {Name(rt.m_target)}");

                    GameObject wild2 = Stand("Dverger", at(d, -0.8f));
                    yield return Sec(0.5f);
                    bool gotWild = false;
                    bool shotTame = false;
                    until = Time.realtimeSinceStartup + 8f;
                    while (Time.realtimeSinceStartup < until && !gotWild)
                    {
                        rt.m_updateTargetTimer = 0f;
                        if (rt.m_target != null && rt.m_target.gameObject == tame3) shotTame = true;
                        if (rt.m_target != null && rt.m_target.gameObject == wild2) gotWild = true;
                        yield return Sec(0.5f);
                    }
                    Check(gotWild && !shotTame, "B9b the real ballista picks the wild Dverger beside the tame one (8 s)",
                        $"wild picked {gotWild}, tame picked {shotTame}, target now {Name(rt.m_target)}");
                    Gone(tame3, wild2);
                }
            }
            finally
            {
                ConfigManager.SpareAlliesFromBallistas.Value = true;
                Gone(toClean.Where(g => g != null).ToArray());
            }
        }
    }
}
