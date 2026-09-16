using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DvergrAllies
{
    // Persistent history. Everything here is a cumulative fact about events that ALREADY HAPPENED and
    // is therefore unrecoverable from a world scan: the dead leave no ZDO behind, and a consumed
    // contract leaves no trace at all. Counts of the LIVING are not stored here - those are recounted
    // from the ZDO database every export (see DvergrStatsExporter), because a stored living-count
    // would drift out of sync with reality the moment anything died off-tick.
    //
    // Deliberately Unity- and Valheim-free so the StatsExportFixtures harness can link this file
    // directly and exercise the real save/load path without the game.
    public class PlayerLifetimeStats
    {
        [JsonProperty("player_id")] public string PlayerId;
        [JsonProperty("name")] public string Name = "Unknown";
        [JsonProperty("tamed")] public long Tamed;
        [JsonProperty("births")] public long Births;
        [JsonProperty("deaths")] public long Deaths;
        [JsonProperty("contracts_consumed")] public long ContractsConsumed;
        [JsonProperty("elite_pulls")] public long ElitePulls;
    }

    public class DvergrLifetimeStats
    {
        [JsonProperty("world_uid")] public long WorldUid;
        [JsonProperty("world_name")] public string WorldName = "";
        [JsonProperty("tracking_since")] public string TrackingSince;

        [JsonProperty("tamed")] public long Tamed;
        [JsonProperty("births")] public long Births;
        [JsonProperty("births_special_combo")] public long BirthsSpecialCombo;
        [JsonProperty("deaths")] public long Deaths;
        [JsonProperty("star_ups_at_birth")] public long StarUpsAtBirth;
        [JsonProperty("highest_level_bred")] public int HighestLevelBred;
        [JsonProperty("contracts_consumed")] public long ContractsConsumed;

        [JsonProperty("births_by_class")] public Dictionary<string, long> BirthsByClass = new Dictionary<string, long>();
        [JsonProperty("deaths_by_class")] public Dictionary<string, long> DeathsByClass = new Dictionary<string, long>();
        [JsonProperty("gacha_pulls_by_class")] public Dictionary<string, long> GachaPullsByClass = new Dictionary<string, long>();

        [JsonProperty("players")] public Dictionary<string, PlayerLifetimeStats> Players = new Dictionary<string, PlayerLifetimeStats>();

        public static DvergrLifetimeStats CreateNew(long worldUid, string worldName, DateTime nowUtc)
        {
            return new DvergrLifetimeStats
            {
                WorldUid = worldUid,
                WorldName = worldName ?? "",
                TrackingSince = DvergrStatsSchema.FormatTimestamp(nowUtc)
            };
        }

        // Newtonsoft leaves a field untouched when its JSON key is absent, but an explicit `null` in the
        // file overwrites the initializer with null. Both happen in practice: the first from a file
        // written by an older schema, the second from a hand-edited or truncated file. Normalising on
        // load means every consumer downstream can dereference these without a null check.
        public void NormalizeAfterLoad()
        {
            if (WorldName == null) WorldName = "";
            if (BirthsByClass == null) BirthsByClass = new Dictionary<string, long>();
            if (DeathsByClass == null) DeathsByClass = new Dictionary<string, long>();
            if (GachaPullsByClass == null) GachaPullsByClass = new Dictionary<string, long>();
            if (Players == null) Players = new Dictionary<string, PlayerLifetimeStats>();

            foreach (var kv in Players)
            {
                if (kv.Value == null) continue;
                if (string.IsNullOrEmpty(kv.Value.PlayerId)) kv.Value.PlayerId = kv.Key;
                if (string.IsNullOrEmpty(kv.Value.Name)) kv.Value.Name = "Unknown";
            }
        }

        public PlayerLifetimeStats GetOrCreatePlayer(string playerId, string name)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == "0") return null;

            if (!Players.TryGetValue(playerId, out PlayerLifetimeStats stats) || stats == null)
            {
                stats = new PlayerLifetimeStats { PlayerId = playerId, Name = string.IsNullOrEmpty(name) ? "Unknown" : name };
                Players[playerId] = stats;
            }
            else if (!string.IsNullOrEmpty(name) && name != "Unknown")
            {
                // Players rename themselves; keep the most recent name we've seen so leaderboard labels
                // don't go stale.
                stats.Name = name;
            }

            return stats;
        }

        public static void Bump(Dictionary<string, long> dict, string key, long amount = 1)
        {
            if (dict == null || string.IsNullOrEmpty(key)) return;
            dict.TryGetValue(key, out long current);
            dict[key] = current + amount;
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static DvergrLifetimeStats FromJson(string json)
        {
            var parsed = JsonConvert.DeserializeObject<DvergrLifetimeStats>(json);
            parsed?.NormalizeAfterLoad();
            return parsed;
        }
    }
}
