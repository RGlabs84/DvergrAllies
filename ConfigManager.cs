using BepInEx.Configuration;
using Jotunn.Configs;

namespace DvergrAllies
{
    public static class ConfigManager
    {
        // Taming
        public static ConfigEntry<float> TamingTime;
        public static ConfigEntry<string> TamingItems;
        public static ConfigEntry<float> FedDuration;
        public static ConfigEntry<float> ConsumeRange;

        // Breeding
        public static ConfigEntry<float> PregnancyDuration;
        public static ConfigEntry<int> BreedingLimit;
        public static ConfigEntry<float> SpecialComboChance;
        public static ConfigEntry<float> PregnancyChance;
        public static ConfigEntry<float> PartnerCheckRange;
        public static ConfigEntry<float> UpdateInterval;
        public static ConfigEntry<float> LevelUpChance;
        public static ConfigEntry<int> MaxBreedingLevel;

        // Leash Settings
        public static ConfigEntry<float> MaxFollowLeash;

        // Economy
        public static ConfigEntry<int> ContractCost;

        // Stats
        public static ConfigEntry<float> AllyHealthMultiplier;
        public static ConfigEntry<float> AllyDamageMultiplier;

        // BarrkBOT Export
        public static ConfigEntry<bool> EnableStatsExport;
        public static ConfigEntry<float> StatsExportInterval;
        public static ConfigEntry<bool> ForceCensusUnavailable;

        // Debug
        public static ConfigEntry<bool> EnableDebugLogs;
        public static ConfigEntry<bool> DebugFull;
        public static ConfigEntry<bool> DebugAI;
        public static ConfigEntry<bool> DebugBreeding;
        public static ConfigEntry<bool> DebugEquipment;

        public static void LogAI(string message)
        {
            if (EnableDebugLogs != null && EnableDebugLogs.Value && (DebugFull.Value || DebugAI.Value))
                Jotunn.Logger.LogInfo("[AI] " + message);
        }

        public static void LogBreeding(string message)
        {
            if (EnableDebugLogs != null && EnableDebugLogs.Value && (DebugFull.Value || DebugBreeding.Value))
                Jotunn.Logger.LogInfo("[Breeding] " + message);
        }

        public static void LogEquipment(string message)
        {
            if (EnableDebugLogs != null && EnableDebugLogs.Value && (DebugFull.Value || DebugEquipment.Value))
                Jotunn.Logger.LogInfo("[Equipment] " + message);
        }

        public static void LogDebug(string message)
        {
            if (EnableDebugLogs != null && EnableDebugLogs.Value && DebugFull.Value)
                Jotunn.Logger.LogInfo("[General] " + message);
        }

        private static ConfigDescription SyncedConfig(string description)
        {
            return new ConfigDescription(description, null, new ConfigurationManagerAttributes { IsAdminOnly = true });
        }

        public static void Init(ConfigFile config)
        {
            TamingTime = config.Bind("1 - Taming", "Taming Time", 1800f, SyncedConfig("Time in seconds required to tame a Dvergr."));
            TamingItems = config.Bind("1 - Taming", "Taming Items", "CookedMeat,Coins,Sausages,YggdrasilWood", SyncedConfig("Comma-separated list of item prefabs Dvergrs will eat to tame/breed."));
            FedDuration = config.Bind("1 - Taming", "Fed Duration", 600f, SyncedConfig("Time in seconds a Dvergr stays fed (not hungry) after eating."));
            ConsumeRange = config.Bind("1 - Taming", "Consume Range", 4f, SyncedConfig("Distance from which they can consume food. Increase if they get stuck pushing each other."));

            PregnancyDuration = config.Bind("2 - Breeding", "Pregnancy Duration", 600f, SyncedConfig("Time in seconds before a pregnant Dvergr gives birth."));
            BreedingLimit = config.Bind("2 - Breeding", "Breeding Limit", 4, SyncedConfig("Maximum number of Dvergrs in a 10m radius before they stop breeding."));
            SpecialComboChance = config.Bind("2 - Breeding", "Special Combo Chance", 25f, SyncedConfig("Percentage chance (0-100) to breed a special combo variant if parents are compatible."));
            PregnancyChance = config.Bind("2 - Breeding", "Pregnancy Chance", 0.33f, SyncedConfig("Chance (0.0 to 1.0) of getting pregnant when conditions are met."));
            PartnerCheckRange = config.Bind("2 - Breeding", "Partner Check Range", 10f, SyncedConfig("Distance within which to look for a partner."));
            UpdateInterval = config.Bind("2 - Breeding", "Update Interval", 10f, SyncedConfig("How often (in seconds) the breeding logic checks for partners/pregnancy."));
            LevelUpChance = config.Bind("2 - Breeding", "Level Up Chance", 10f, SyncedConfig("Percentage chance (0-100) for offspring to gain +1 level (star) above their highest level parent."));
            MaxBreedingLevel = config.Bind("2 - Breeding", "Max Breeding Level", 3, SyncedConfig("Maximum level (3 = 2 stars) a Dvergr can reach through breeding."));

            MaxFollowLeash = config.Bind("2.5 - AI", "Max Follow Leash", 40f, SyncedConfig("Max distance (meters) a Dvergr can chase an enemy before dropping aggro to return to the player they are following."));

            ContractCost = config.Bind("3 - Economy", "Contract Cost", 999, SyncedConfig("Cost in coins to purchase a Dvergr Contract from Haldor or Recruiter."));

            AllyHealthMultiplier = config.Bind("4 - Stats", "Health Multiplier", 1.0f, SyncedConfig("Multiplier applied to base health of Ally Dvergrs."));
            AllyDamageMultiplier = config.Bind("4 - Stats", "Damage Multiplier", 1.0f, SyncedConfig("Multiplier applied to base damage of Ally Dvergrs."));

            EnableStatsExport = config.Bind("6 - BarrkBOT Export", "Enable Stats Export", true, "Periodically writes Dvergr population/ownership stats to BepInEx/config/DvergrAllies/ as JSON for BarrkBOT to read. Dedicated server / world host only; local-only setting, not synced.");
            StatsExportInterval = config.Bind("6 - BarrkBOT Export", "Export Interval", 60f, "Seconds between stats exports. BarrkBOT only sweeps its watch folder every 20 minutes, so going below 60s buys nothing but disk churn.");
            ForceCensusUnavailable = config.Bind("6 - BarrkBOT Export", "Force Census Unavailable", false, "TESTING ONLY. Forces the export to report 'census_unavailable' as if the world could not be scanned, so the failure path can be exercised on a live server without breaking anything. Population counts are written as null (not zero); lifetime history is unaffected and keeps recording. The export says plainly that it was forced, so this cannot be mistaken for a real fault. Leave this off in normal play.");

            EnableDebugLogs = config.Bind("5 - Debug", "Enable Debug Logs", false, SyncedConfig("Master switch to enable debug logging."));
            DebugFull = config.Bind("5 - Debug", "Debug Full", false, SyncedConfig("If true, overrides sub-categories and logs absolutely everything."));
            DebugAI = config.Bind("5 - Debug", "Debug AI", false, SyncedConfig("Log AI behavior overrides, targeting, and behavior resets."));
            DebugBreeding = config.Bind("5 - Debug", "Debug Breeding", false, SyncedConfig("Log partner finding, pregnancy, birth, and genetic scaling."));
            DebugEquipment = config.Bind("5 - Debug", "Debug Equipment", false, SyncedConfig("Log prefab creation, loadout setup, and weapon scaling/upgrades."));
        }
    }
}

