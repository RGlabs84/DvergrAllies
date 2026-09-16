using BepInEx;
using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace DvergrAllies
{
    // Server-authoritative history.
    //
    // WHY AN RPC AND NOT A DIRECT COUNTER BUMP: a Valheim creature is simulated by whichever machine
    // OWNS its ZDO, and ownership follows whoever is standing nearby - so on a dedicated server the
    // tame, the birth and (critically) the death all execute inside a CLIENT's process, not the
    // server's. Counting them where they fire would mean every client keeping its own partial tally
    // that nobody ever merges. Instead every machine reports the event to the server, and only the
    // server holds and writes the numbers.
    //
    // Deaths are the reason history has to exist separately from the world scan at all: a dead Dvergr
    // has no ZDO left to count, so if the event isn't banked at the moment it happens it is gone.
    public static class DvergrStatsStore
    {
        private const string RpcName = "DvergrAllies_StatEvent";

        public static DvergrLifetimeStats Current;

        private static ZRoutedRpc s_registeredOn;
        private static long s_loadedWorldUid;
        private static bool s_dirty;

        public static bool IsServer()
        {
            return ZNet.instance != null && ZNet.instance.IsServer();
        }

        // Called every export tick. ZRoutedRpc and ZNet are torn down and rebuilt between world
        // sessions, so this re-arms itself by comparing against the instance we actually registered on
        // rather than a one-shot bool that would silently stop working on the second world joined.
        public static void EnsureReady()
        {
            if (ZNet.instance == null) return;

            if (ZRoutedRpc.instance != null && !ReferenceEquals(s_registeredOn, ZRoutedRpc.instance))
            {
                ZRoutedRpc.instance.Register<string>(RpcName, RPC_StatEvent);
                s_registeredOn = ZRoutedRpc.instance;
            }

            if (!IsServer()) return;

            long uid = ZNet.instance.GetWorldUID();
            if (Current == null || s_loadedWorldUid != uid) Load(uid);
        }

        private static string StateFilePath(long worldUid)
        {
            // Lives beside the exports but is deliberately NOT named barrkbot_*.json: BarrkBOT's sweep
            // matches that prefix, and this file is our own bookkeeping, not something it should read.
            return Path.Combine(Path.Combine(Paths.ConfigPath, "DvergrAllies"),
                $"dvergr_lifetime_{worldUid}.json");
        }

        private static void Load(long worldUid)
        {
            string path = StateFilePath(worldUid);
            string worldName = ZNet.instance != null ? ZNet.instance.GetWorldName() : "";

            try
            {
                if (File.Exists(path))
                {
                    var loaded = DvergrLifetimeStats.FromJson(File.ReadAllText(path));
                    if (loaded != null && loaded.WorldUid == worldUid)
                    {
                        Current = loaded;
                        s_loadedWorldUid = worldUid;
                        Current.WorldName = worldName;
                        ConfigManager.LogDebug(
                            $"[StatsStore] Loaded lifetime stats for world {worldUid}: {Current.Tamed} tamed, {Current.Births} births, {Current.Deaths} deaths.");
                        return;
                    }

                    // A file whose world_uid doesn't match the world we're actually in would silently
                    // attribute another world's history to this one. Start clean instead.
                    Jotunn.Logger.LogWarning(
                        $"[StatsStore] {Path.GetFileName(path)} is for world {loaded?.WorldUid}, not {worldUid}. Starting fresh.");
                }
            }
            catch (Exception e)
            {
                // A corrupt or half-written state file must not take the world down with it, and must
                // not be silently overwritten either - back it up so the numbers can be recovered.
                Jotunn.Logger.LogWarning($"[StatsStore] Could not read {path} ({e.Message}); starting fresh.");
                TryBackupCorruptFile(path);
            }

            Current = DvergrLifetimeStats.CreateNew(worldUid, worldName, DateTime.UtcNow);
            s_loadedWorldUid = worldUid;
            s_dirty = true;
        }

        private static void TryBackupCorruptFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Move(path, path + ".corrupt");
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[StatsStore] Could not preserve corrupt state file: {e.Message}");
            }
        }

        public static void Save(bool force = false)
        {
            // Deliberately NOT gated on IsServer(): Current is only ever populated on the server (Load
            // runs nowhere else), and by the time the shutdown flush arrives ZNet may already be torn
            // down - an IsServer() check there would quietly throw away the final write.
            if (Current == null) return;
            if (!s_dirty && !force) return;

            try
            {
                string path = StateFilePath(Current.WorldUid);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                WriteAtomic(path, Current.ToJson());
                s_dirty = false;
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[StatsStore] Failed to save lifetime stats: {e}");
            }
        }

        public static void WriteAtomic(string path, string content)
        {
            // Readers (BarrkBOT for the exports, our own Load for the state file) open these on their
            // own schedule with no locking, so a partially-written file would be read as truncated
            // JSON. Write to a sibling temp file and swap it in, which is atomic enough on both
            // Windows and Linux for a reader that only ever opens the final name.
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        // ---- Event reporting (runs on whichever machine observed the event) ----

        private static void Report(JObject payload)
        {
            if (ZRoutedRpc.instance == null) return;

            if (IsServer())
            {
                Apply(payload);
                return;
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.instance.GetServerPeerID(), RpcName, payload.ToString(Newtonsoft.Json.Formatting.None));
        }

        public static void ReportTame(string className, long playerId, string playerName)
        {
            Report(new JObject
            {
                ["kind"] = "tame",
                ["cls"] = className,
                ["pid"] = playerId.ToString(),
                ["pname"] = playerName ?? ""
            });
        }

        public static void ReportBirth(string className, long playerId, string playerName, bool specialCombo,
            bool starUp, int level)
        {
            Report(new JObject
            {
                ["kind"] = "birth",
                ["cls"] = className,
                ["pid"] = playerId.ToString(),
                ["pname"] = playerName ?? "",
                ["combo"] = specialCombo,
                ["starup"] = starUp,
                ["level"] = level
            });
        }

        public static void ReportDeath(string className, long playerId, string playerName)
        {
            Report(new JObject
            {
                ["kind"] = "death",
                ["cls"] = className,
                ["pid"] = playerId.ToString(),
                ["pname"] = playerName ?? ""
            });
        }

        public static void ReportContract(string pulledClass, long playerId, string playerName)
        {
            Report(new JObject
            {
                ["kind"] = "contract",
                ["cls"] = pulledClass,
                ["pid"] = playerId.ToString(),
                ["pname"] = playerName ?? ""
            });
        }

        private static void RPC_StatEvent(long sender, string payload)
        {
            if (!IsServer() || string.IsNullOrEmpty(payload)) return;

            try
            {
                Apply(JObject.Parse(payload));
            }
            catch (Exception e)
            {
                ConfigManager.LogDebug($"[StatsStore] Ignored malformed stat event from {sender}: {e.Message}");
            }
        }

        // ---- Applying (server only) ----

        private static void Apply(JObject payload)
        {
            if (Current == null) return;

            string kind = (string)payload["kind"];
            if (string.IsNullOrEmpty(kind)) return;

            string cls = (string)payload["cls"];
            if (string.IsNullOrEmpty(cls)) cls = "Rogue";

            string pid = (string)payload["pid"];
            string pname = (string)payload["pname"];
            PlayerLifetimeStats player = Current.GetOrCreatePlayer(pid, pname);

            switch (kind)
            {
                case "tame":
                    Current.Tamed++;
                    if (player != null) player.Tamed++;
                    break;

                case "birth":
                    Current.Births++;
                    DvergrLifetimeStats.Bump(Current.BirthsByClass, cls);
                    if (player != null) player.Births++;

                    if (payload["combo"] != null && (bool)payload["combo"]) Current.BirthsSpecialCombo++;
                    if (payload["starup"] != null && (bool)payload["starup"]) Current.StarUpsAtBirth++;

                    if (payload["level"] != null)
                    {
                        int level = (int)payload["level"];
                        // Clamped against the config ceiling so a malformed or hostile payload can't
                        // park an impossible number in a field that only ever moves upward.
                        int cap = ConfigManager.MaxBreedingLevel != null ? ConfigManager.MaxBreedingLevel.Value : 3;
                        if (level > Current.HighestLevelBred && level <= cap) Current.HighestLevelBred = level;
                    }
                    break;

                case "death":
                    Current.Deaths++;
                    DvergrLifetimeStats.Bump(Current.DeathsByClass, cls);
                    if (player != null) player.Deaths++;
                    break;

                case "contract":
                    Current.ContractsConsumed++;
                    DvergrLifetimeStats.Bump(Current.GachaPullsByClass, cls);
                    if (player != null)
                    {
                        player.ContractsConsumed++;
                        if (DvergrStatsSchema.IsEliteClass(cls)) player.ElitePulls++;
                    }
                    break;

                default:
                    return;
            }

            s_dirty = true;
        }
    }
}
