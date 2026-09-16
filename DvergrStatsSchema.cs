using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DvergrAllies
{
    public class DvergrWorldSnapshot
    {
        public int AliveTamed;
        public int Pregnant;
        public int Hungry;
        public int Male;
        public int Female;
        public int GenderUnknown;
        public Dictionary<string, int> ClassCounts = new Dictionary<string, int>();
    }

    public class DvergrPlayerSnapshot
    {
        public string PlayerId;
        public string Name;
        public int AliveTamed;
        public int PregnantOwned;
        public int HungryOwned;
        public Dictionary<string, int> ClassCounts = new Dictionary<string, int>();
    }

    // Pure JSON shaping for the BarrkBOT filesystem-sweep export contract. No UnityEngine/Valheim
    // dependency on purpose: DvergrStatsExporter feeds this real live-game state, the
    // StatsExportFixtures console harness feeds it synthetic state to produce committed test fixtures -
    // same shaping code either way, so the fixtures can't drift from what the mod actually writes.
    public static class DvergrStatsSchema
    {
        // 2: added the `lifetime` block and per-player all-time fields (persistent history).
        // 3: BarrkBOT export contract v3. No shape change - v2 already satisfied it; the bump just
        //    declares which contract this file is written against.
        public const int SchemaVersion = 3;

        // Order matters: check compound/specific names before the generic substrings they contain
        // (e.g. "SpellswordFire" before "Fire", "MageElemental" before "Mage"). Matches both the
        // wild-tamed prefabs (Dverger, DvergerMageFire, ...) and the contract/gacha clones
        // (AllyDvergrRogue, AllyDvergrSpellswordFire, ...) - see AllyPrefabManager.cs.
        public static readonly string[] AllClassNames =
        {
            "Rogue", "Mage", "Fire Mage", "Ice Mage", "Support Mage", "Elemental Mage",
            "Fire Spellsword", "Ice Spellsword", "Cleric", "Warrior", "Berserker"
        };

        // The five classes obtainable directly from the wild. Everything else has to be bred or pulled
        // from a contract, which is what makes it an "elite" result worth counting separately.
        private static readonly HashSet<string> BaseClasses = new HashSet<string>
        {
            "Rogue", "Mage", "Fire Mage", "Ice Mage", "Support Mage"
        };

        public static bool IsEliteClass(string className)
        {
            return !string.IsNullOrEmpty(className) && !BaseClasses.Contains(className);
        }

        public static string ClassifyPrefabName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return "Rogue";

            if (prefabName.Contains("Berserker")) return "Berserker";
            if (prefabName.Contains("SpellswordFire")) return "Fire Spellsword";
            if (prefabName.Contains("SpellswordIce")) return "Ice Spellsword";
            if (prefabName.Contains("MageElemental")) return "Elemental Mage";
            if (prefabName.Contains("Cleric")) return "Cleric";
            if (prefabName.Contains("Warrior")) return "Warrior";
            if (prefabName.Contains("MageFire")) return "Fire Mage";
            if (prefabName.Contains("MageIce")) return "Ice Mage";
            if (prefabName.Contains("MageSupport")) return "Support Mage";
            if (prefabName.Contains("Mage")) return "Mage";
            return "Rogue"; // Base "Dverger" / "AllyDvergrRogue" - the breeding tree's base tame class.
        }

        private static JObject FullClassCounts<T>(Dictionary<string, T> partial)
        {
            var obj = new JObject();
            foreach (var name in AllClassNames)
            {
                T count = default(T);
                if (partial != null) partial.TryGetValue(name, out count);
                obj[name] = Convert.ToInt64(count);
            }
            return obj;
        }

        public static string FormatTimestamp(DateTime dt)
        {
            return dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }

        // A census figure is emitted as null - never as 0 - when the census could not run. Zero would
        // read as "no Dvergr are alive", which is a confident wrong answer rather than a missing one;
        // null says we did not measure. Only census fields are affected: the lifetime block comes from
        // the counter file and stays valid regardless of whether the world could be scanned.
        private static JToken CensusValue(bool measured, int value)
        {
            return measured ? (JToken)value : JValue.CreateNull();
        }

        private const string CensusUnavailableNote =
            "The live census did NOT run this cycle, so every census field is null rather than a " +
            "number - that means 'not measured', not 'none'. Do not report population, class counts or " +
            "any alive_* figure from this file; say the count is unavailable and give the reason in " +
            "'status_detail'. The 'lifetime' block and every '*_alltime' field come from the counter " +
            "file rather than the world scan and are still accurate.";

        // Kept deliberately short. Under contract v3 the reader qualifies zeros per scope on its own
        // ("none right now" for a census field, "none recorded since tracking began" for a cumulative
        // one) and names which blocks survive restarts, so spelling either out here would only repeat
        // it. What remains is the one thing the reader cannot infer: that mixing the two bases in a
        // single arithmetic operation is meaningless.
        private const string IncomparableNote =
            "Never subtract or compare a live count against a lifetime one. 'population', " +
            "'class_counts' and every 'alive_*' field count Dvergr existing right now; the 'lifetime' " +
            "block and every '*_alltime' field are cumulative since 'tracking_since'. A player with 2 " +
            "alive and 9 tamed_alltime has not lost 7 - use 'deaths_alltime' for losses.";

        public static string BuildWorldJson(DvergrWorldSnapshot snapshot, DvergrLifetimeStats lifetime,
            string source, DateTime generatedAt, DateTime sessionStartedAt, int exportIntervalSeconds,
            string censusUnavailableReason = null)
        {
            bool measured = censusUnavailableReason == null;

            var root = new JObject
            {
                ["schema_version"] = SchemaVersion,
                ["generated_at"] = FormatTimestamp(generatedAt),
                ["source"] = source,
                // Written on EVERY cycle, including one where the census failed, so that the absence of
                // this file means one thing only: the exporter never ran at all.
                ["status"] = measured ? "ok" : "census_unavailable",
                ["status_detail"] = censusUnavailableReason,
                ["session_started_at"] = FormatTimestamp(sessionStartedAt),
                ["tracking_since"] = lifetime != null ? lifetime.TrackingSince : null,
                ["world_name"] = lifetime != null ? lifetime.WorldName : "",
                ["intervals"] = new JObject { ["write_seconds"] = exportIntervalSeconds },
                ["population"] = new JObject
                {
                    ["alive_tamed"] = CensusValue(measured, snapshot.AliveTamed),
                    ["pregnant"] = CensusValue(measured, snapshot.Pregnant),
                    ["hungry"] = CensusValue(measured, snapshot.Hungry),
                    ["male"] = CensusValue(measured, snapshot.Male),
                    ["female"] = CensusValue(measured, snapshot.Female),
                    ["gender_unknown"] = CensusValue(measured, snapshot.GenderUnknown)
                },
                ["class_counts"] = measured ? FullClassCounts(snapshot.ClassCounts) : (JToken)JValue.CreateNull(),
                ["lifetime"] = new JObject
                {
                    ["tamed_alltime"] = lifetime != null ? lifetime.Tamed : 0L,
                    ["births_alltime"] = lifetime != null ? lifetime.Births : 0L,
                    ["births_special_combo"] = lifetime != null ? lifetime.BirthsSpecialCombo : 0L,
                    ["deaths_alltime"] = lifetime != null ? lifetime.Deaths : 0L,
                    ["star_ups_at_birth"] = lifetime != null ? lifetime.StarUpsAtBirth : 0L,
                    ["highest_level_bred"] = lifetime != null ? lifetime.HighestLevelBred : 0,
                    ["contracts_consumed"] = lifetime != null ? lifetime.ContractsConsumed : 0L
                },
                ["lifetime_births_by_class"] = FullClassCounts(lifetime?.BirthsByClass),
                ["lifetime_deaths_by_class"] = FullClassCounts(lifetime?.DeathsByClass),
                ["gacha_pulls_by_class"] = FullClassCounts(lifetime?.GachaPullsByClass),
                ["notes"] =
                    "The census reads the save database directly, so it includes Dvergr in zones no " +
                    "player has loaded. Only TAMED Dvergr are counted; wild Mistlands ones are ignored. " +
                    "'gender_unknown' means it has never been near a player since spawning, so its " +
                    "gender was never rolled.",
                ["counter_notes"] = IncomparableNote
            };

            if (!measured) root["status_notes"] = CensusUnavailableNote;

            return root.ToString(Formatting.Indented);
        }

        public static string BuildPlayersJson(IEnumerable<DvergrPlayerSnapshot> livePlayers,
            DvergrLifetimeStats lifetime, string source, DateTime generatedAt, DateTime sessionStartedAt,
            string censusUnavailableReason = null)
        {
            bool measured = censusUnavailableReason == null;

            // Union of "owns something alive right now" and "has history on this world". A player whose
            // whole warband died still belongs in this file - that they have 0 alive and 12 deaths is
            // the single most interesting thing the persistent half of this export can say, and keying
            // only off the live census would drop them entirely.
            var byId = new Dictionary<string, DvergrPlayerSnapshot>();
            if (livePlayers != null)
            {
                foreach (var p in livePlayers)
                {
                    if (p == null || string.IsNullOrEmpty(p.PlayerId)) continue;
                    byId[p.PlayerId] = p;
                }
            }

            if (lifetime?.Players != null)
            {
                foreach (var kv in lifetime.Players)
                {
                    if (kv.Value == null || byId.ContainsKey(kv.Key)) continue;
                    byId[kv.Key] = new DvergrPlayerSnapshot { PlayerId = kv.Key, Name = kv.Value.Name };
                }
            }

            var playersObj = new JObject();
            foreach (var kv in byId)
            {
                DvergrPlayerSnapshot live = kv.Value;
                PlayerLifetimeStats hist = null;
                lifetime?.Players?.TryGetValue(kv.Key, out hist);

                // Prefer the lifetime store's name: the live census reads a name stamped onto the
                // Dvergr's ZDO when it was tamed, which goes stale if the player later renames.
                string name = hist != null && !string.IsNullOrEmpty(hist.Name) && hist.Name != "Unknown"
                    ? hist.Name
                    : (string.IsNullOrEmpty(live.Name) ? "Unknown" : live.Name);

                playersObj[kv.Key] = new JObject
                {
                    ["player_id"] = kv.Key,
                    ["name"] = name,
                    // Census half goes null when unmeasured; the all-time half comes from the counter
                    // file and stays real, which is the whole reason a player with nothing alive is
                    // still worth a row.
                    ["alive_tamed"] = CensusValue(measured, live.AliveTamed),
                    ["pregnant_owned"] = CensusValue(measured, live.PregnantOwned),
                    ["hungry_owned"] = CensusValue(measured, live.HungryOwned),
                    ["tamed_alltime"] = hist != null ? hist.Tamed : 0L,
                    ["births_alltime"] = hist != null ? hist.Births : 0L,
                    ["deaths_alltime"] = hist != null ? hist.Deaths : 0L,
                    ["contracts_consumed"] = hist != null ? hist.ContractsConsumed : 0L,
                    ["elite_pulls"] = hist != null ? hist.ElitePulls : 0L,
                    ["class_counts"] = measured ? FullClassCounts(live.ClassCounts) : (JToken)JValue.CreateNull()
                };
            }

            var root = new JObject
            {
                ["schema_version"] = SchemaVersion,
                ["generated_at"] = FormatTimestamp(generatedAt),
                ["source"] = source,
                ["status"] = measured ? "ok" : "census_unavailable",
                ["status_detail"] = censusUnavailableReason,
                ["session_started_at"] = FormatTimestamp(sessionStartedAt),
                ["tracking_since"] = lifetime != null ? lifetime.TrackingSince : null,
                ["notes"] =
                    "A player appears here if they own a living Dvergr OR have recorded history on this " +
                    "world. Ownership is stamped at tame/summon and inherited by offspring at birth; " +
                    "Dvergr tamed before tracking existed belong to nobody and appear only in the world " +
                    "file's totals. 'elite_pulls' counts contracts that produced a bred-tier class.",
                ["counter_notes"] = IncomparableNote,
                ["players"] = playersObj
            };

            if (!measured) root["status_notes"] = CensusUnavailableNote;

            return root.ToString(Formatting.Indented);
        }
    }
}
