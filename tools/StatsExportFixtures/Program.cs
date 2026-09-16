using System;
using System.Collections.Generic;
using System.IO;
using DvergrAllies;

namespace DvergrAllies.StatsExportFixtures
{
    // Standalone harness for BarrkBOT's test suite. Exercises the real shaping and persistence code
    // (DvergrStatsSchema + DvergrLifetimeStats are linked straight from the mod, not reimplemented)
    // against synthetic data, and writes the two requested scenarios: a populated world and a fresh
    // server. Also round-trips the persistent state file, since "history survives a restart" is the
    // whole point of the lifetime half and is worth proving rather than assuming.
    internal static class Program
    {
        private const string Source = "DvergrAllies 1.0.5";
        private static int s_failures;

        private static int Main(string[] args)
        {
            string outDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "fixtures");
            Directory.CreateDirectory(outDir);

            WritePopulated(outDir);
            WriteFreshServer(outDir);
            WriteCensusUnavailable(outDir);
            RoundTripStateFile(outDir);

            Console.WriteLine(s_failures == 0
                ? $"OK - fixtures written to {outDir}"
                : $"FAILED - {s_failures} check(s) failed");
            return s_failures == 0 ? 0 : 1;
        }

        private static void Check(string what, bool condition)
        {
            if (condition) { Console.WriteLine($"  pass: {what}"); return; }
            Console.WriteLine($"  FAIL: {what}");
            s_failures++;
        }

        private static DvergrLifetimeStats BuildPopulatedLifetime()
        {
            var lifetime = DvergrLifetimeStats.CreateNew(
                -8_170_395_712_004_442_113L, "Midgard", new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

            lifetime.Tamed = 23;
            lifetime.Births = 31;
            lifetime.BirthsSpecialCombo = 7;
            lifetime.Deaths = 12;
            lifetime.StarUpsAtBirth = 4;
            lifetime.HighestLevelBred = 3;
            lifetime.ContractsConsumed = 9;

            lifetime.BirthsByClass = new Dictionary<string, long>
            {
                ["Rogue"] = 11, ["Warrior"] = 6, ["Cleric"] = 4, ["Fire Spellsword"] = 3,
                ["Elemental Mage"] = 2, ["Berserker"] = 2, ["Support Mage"] = 3
            };
            lifetime.DeathsByClass = new Dictionary<string, long>
            {
                ["Rogue"] = 6, ["Warrior"] = 3, ["Berserker"] = 2, ["Cleric"] = 1
            };
            lifetime.GachaPullsByClass = new Dictionary<string, long>
            {
                ["Rogue"] = 3, ["Mage"] = 2, ["Ice Mage"] = 1, ["Warrior"] = 1,
                ["Cleric"] = 1, ["Berserker"] = 1
            };

            lifetime.Players["76561198012345678"] = new PlayerLifetimeStats
            {
                PlayerId = "76561198012345678", Name = "Rohan",
                Tamed = 12, Births = 19, Deaths = 5, ContractsConsumed = 6, ElitePulls = 2
            };
            lifetime.Players["76561198087654321"] = new PlayerLifetimeStats
            {
                PlayerId = "76561198087654321", Name = "Kyrrynth",
                Tamed = 8, Births = 10, Deaths = 3, ContractsConsumed = 3, ElitePulls = 1
            };
            // Owns nothing alive any more - the entire warband died. Exists ONLY in the lifetime store,
            // so this row proves the players file unions history with the live census instead of
            // keying off the census alone and dropping them.
            lifetime.Players["76561198000000042"] = new PlayerLifetimeStats
            {
                PlayerId = "76561198000000042", Name = "Fable",
                Tamed = 3, Births = 2, Deaths = 4, ContractsConsumed = 0, ElitePulls = 0
            };

            return lifetime;
        }

        private static void WritePopulated(string outDir)
        {
            Console.WriteLine("populated:");

            DateTime generatedAt = new DateTime(2026, 8, 22, 21, 4, 12, DateTimeKind.Utc);
            DateTime sessionStart = new DateTime(2026, 8, 22, 18, 30, 0, DateTimeKind.Utc);
            DvergrLifetimeStats lifetime = BuildPopulatedLifetime();

            var world = new DvergrWorldSnapshot
            {
                AliveTamed = 14,
                Pregnant = 2,
                Hungry = 1,
                Male = 6,
                Female = 7,
                GenderUnknown = 1, // Never loaded since it spawned, so its gender was never rolled.
                ClassCounts = new Dictionary<string, int>
                {
                    ["Rogue"] = 4, ["Mage"] = 1, ["Fire Mage"] = 1, ["Support Mage"] = 2,
                    ["Elemental Mage"] = 1, ["Fire Spellsword"] = 1, ["Cleric"] = 1,
                    ["Warrior"] = 2, ["Berserker"] = 1
                    // "Ice Mage"/"Ice Spellsword" absent - schema must still zero-fill them.
                }
            };

            var livePlayers = new List<DvergrPlayerSnapshot>
            {
                new DvergrPlayerSnapshot
                {
                    PlayerId = "76561198012345678", Name = "Rohan",
                    AliveTamed = 7, PregnantOwned = 1, HungryOwned = 0,
                    ClassCounts = new Dictionary<string, int>
                    {
                        ["Rogue"] = 2, ["Warrior"] = 2, ["Berserker"] = 1, ["Cleric"] = 1, ["Elemental Mage"] = 1
                    }
                },
                new DvergrPlayerSnapshot
                {
                    PlayerId = "76561198087654321", Name = "Kyrrynth",
                    AliveTamed = 5, PregnantOwned = 1, HungryOwned = 1,
                    ClassCounts = new Dictionary<string, int>
                    {
                        ["Rogue"] = 2, ["Mage"] = 1, ["Fire Mage"] = 1, ["Fire Spellsword"] = 1
                    }
                }
            };

            string worldJson = DvergrStatsSchema.BuildWorldJson(world, lifetime, Source, generatedAt, sessionStart, 60);
            string playersJson = DvergrStatsSchema.BuildPlayersJson(livePlayers, lifetime, Source, generatedAt, sessionStart);

            File.WriteAllText(Path.Combine(outDir, "barrkbot_dvergr_world.populated.json"), worldJson);
            File.WriteAllText(Path.Combine(outDir, "barrkbot_dvergr_players.populated.json"), playersJson);

            Check("dead-warband player survives into the players file via lifetime union",
                playersJson.Contains("76561198000000042") && playersJson.Contains("\"Fable\""));
            Check("that player reports 0 alive but non-zero deaths",
                playersJson.Contains("\"alive_tamed\": 0") && playersJson.Contains("\"deaths_alltime\": 4"));
            Check("absent classes are zero-filled rather than omitted",
                worldJson.Contains("\"Ice Spellsword\": 0"));
        }

        private static void WriteFreshServer(string outDir)
        {
            Console.WriteLine("fresh server:");

            // Brand-new world: nothing tamed, nothing recorded, nobody owns anything. generated_at is
            // moments after session start because the first export tick fires shortly after Awake.
            DateTime generatedAt = new DateTime(2026, 8, 22, 12, 1, 0, DateTimeKind.Utc);
            DateTime sessionStart = new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

            var lifetime = DvergrLifetimeStats.CreateNew(4_411_902_337_115_662_882L, "FreshWorld", sessionStart);

            string worldJson = DvergrStatsSchema.BuildWorldJson(
                new DvergrWorldSnapshot(), lifetime, Source, generatedAt, sessionStart, 60);
            string playersJson = DvergrStatsSchema.BuildPlayersJson(
                new List<DvergrPlayerSnapshot>(), lifetime, Source, generatedAt, sessionStart);

            File.WriteAllText(Path.Combine(outDir, "barrkbot_dvergr_world.fresh.json"), worldJson);
            File.WriteAllText(Path.Combine(outDir, "barrkbot_dvergr_players.fresh.json"), playersJson);

            Check("fresh world still emits tracking_since so 0 can be read as 'none since'",
                worldJson.Contains("\"tracking_since\": \"2026-08-22T12:00:00.000Z\""));
            Check("fresh players file has an empty object, not a missing key",
                playersJson.Contains("\"players\": {}"));
        }

        // The exporter fired but could not scan the world. Census fields go null (NOT 0, which would
        // read as "no Dvergr alive" - a confident wrong answer), while the lifetime block stays real
        // because it comes from the counter file rather than the scan. This is the fixture for the
        // state that used to be indistinguishable from "the mod never ran".
        private static void WriteCensusUnavailable(string outDir)
        {
            Console.WriteLine("census unavailable:");

            DateTime generatedAt = new DateTime(2026, 8, 22, 21, 5, 12, DateTimeKind.Utc);
            DateTime sessionStart = new DateTime(2026, 8, 22, 18, 30, 0, DateTimeKind.Utc);
            DvergrLifetimeStats lifetime = BuildPopulatedLifetime();
            const string reason = "no Dvergr creature prefabs are registered - prefab setup has not run, or another mod replaced them";

            string worldJson = DvergrStatsSchema.BuildWorldJson(
                new DvergrWorldSnapshot(), lifetime, Source, generatedAt, sessionStart, 60, reason);
            string playersJson = DvergrStatsSchema.BuildPlayersJson(
                new List<DvergrPlayerSnapshot>(), lifetime, Source, generatedAt, sessionStart, reason);

            File.WriteAllText(Path.Combine(outDir, "barrkbot_dvergr_world.unavailable.json"), worldJson);
            File.WriteAllText(Path.Combine(outDir, "barrkbot_dvergr_players.unavailable.json"), playersJson);

            Check("status says census_unavailable with a reason",
                worldJson.Contains("\"status\": \"census_unavailable\"") && worldJson.Contains(reason));
            Check("census fields are null, never 0",
                worldJson.Contains("\"alive_tamed\": null") && worldJson.Contains("\"class_counts\": null") &&
                !worldJson.Contains("\"alive_tamed\": 0"));
            Check("lifetime survives a failed census - it comes from the counter file, not the scan",
                worldJson.Contains("\"deaths_alltime\": 12") && worldJson.Contains("\"tamed_alltime\": 23"));
            Check("players still listed from history with alive fields nulled",
                playersJson.Contains("\"Fable\"") && playersJson.Contains("\"alive_tamed\": null") &&
                playersJson.Contains("\"deaths_alltime\": 4"));
            Check("status_notes present so the reader is told not to report the nulls as zero",
                worldJson.Contains("\"status_notes\"") && playersJson.Contains("\"status_notes\""));
            Check("healthy exports still say status ok",
                DvergrStatsSchema.BuildWorldJson(new DvergrWorldSnapshot(), lifetime, Source, generatedAt,
                    sessionStart, 60).Contains("\"status\": \"ok\""));
        }

        // The mod saves this file to disk and reads it back on the next server boot. If the round trip
        // loses a field, history silently resets to zero on restart - which is exactly the failure the
        // persistence work exists to prevent, so it gets an explicit check.
        private static void RoundTripStateFile(string outDir)
        {
            Console.WriteLine("state file round trip:");

            DvergrLifetimeStats original = BuildPopulatedLifetime();
            string statePath = Path.Combine(outDir, "dvergr_lifetime_example.json");
            File.WriteAllText(statePath, original.ToJson());

            DvergrLifetimeStats reloaded = DvergrLifetimeStats.FromJson(File.ReadAllText(statePath));

            Check("world uid survives", reloaded.WorldUid == original.WorldUid);
            Check("scalar counters survive",
                reloaded.Tamed == 23 && reloaded.Births == 31 && reloaded.Deaths == 12 &&
                reloaded.BirthsSpecialCombo == 7 && reloaded.StarUpsAtBirth == 4 &&
                reloaded.HighestLevelBred == 3 && reloaded.ContractsConsumed == 9);
            Check("per-class breakdowns survive",
                reloaded.DeathsByClass["Berserker"] == 2 && reloaded.GachaPullsByClass["Ice Mage"] == 1);
            Check("all three players survive with their totals",
                reloaded.Players.Count == 3 &&
                reloaded.Players["76561198012345678"].Births == 19 &&
                reloaded.Players["76561198000000042"].Deaths == 4);
            Check("tracking_since survives", reloaded.TrackingSince == original.TrackingSince);

            // A file written by an older build won't have the newer keys; loading it must not null out
            // the collections and NRE the next time a counter is bumped.
            var sparse = DvergrLifetimeStats.FromJson("{\"world_uid\":123,\"tamed\":5}");
            Check("sparse/legacy file loads with usable empty collections",
                sparse != null && sparse.Tamed == 5 && sparse.Players != null &&
                sparse.DeathsByClass != null && sparse.WorldName == "");

            // An explicit null in the file is a different path from an absent key: it overwrites the
            // field initializer, so NormalizeAfterLoad has to catch it too.
            var nulled = DvergrLifetimeStats.FromJson(
                "{\"world_uid\":123,\"players\":null,\"deaths_by_class\":null,\"world_name\":null}");
            Check("explicit nulls are normalised rather than left to NRE",
                nulled != null && nulled.Players != null && nulled.DeathsByClass != null && nulled.WorldName == "");

            var bumped = DvergrLifetimeStats.FromJson("{\"world_uid\":123}");
            DvergrLifetimeStats.Bump(bumped.DeathsByClass, "Cleric");
            var p = bumped.GetOrCreatePlayer("999", "Newcomer");
            p.Deaths++;
            Check("counters are usable immediately after a sparse load",
                bumped.DeathsByClass["Cleric"] == 1 && bumped.Players["999"].Deaths == 1);
            Check("unowned events are dropped rather than credited to a phantom player",
                bumped.GetOrCreatePlayer("0", "nobody") == null &&
                bumped.GetOrCreatePlayer(null, "nobody") == null);
        }
    }
}
