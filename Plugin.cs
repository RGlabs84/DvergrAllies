using BepInEx;
using HarmonyLib;
using Jotunn.Utils;

namespace DvergrAllies
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "wubarrk.dvergrallies";
        public const string PluginName = "DvergrAllies";
        public const string PluginVersion = "1.0.3";

        public static bool HasBalrondIdleActors;

        private readonly Harmony harmony = new Harmony(PluginGUID);

        private void Awake()
        {
            HasBalrondIdleActors = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("balrond.astafaraios.BalrondIdleActors");

            // Initialize config
            ConfigManager.Init(Config);

            // Subscribe to Jotunn's prefab event
            Jotunn.Managers.PrefabManager.OnVanillaPrefabsAvailable += CustomStavesManager.Setup;
            Jotunn.Managers.PrefabManager.OnVanillaPrefabsAvailable += AllyPrefabManager.Setup;
            Jotunn.Managers.PrefabManager.OnVanillaPrefabsAvailable += RecruiterManager.Setup;

            // Apply Harmony patches
            harmony.PatchAll();

            Logger.LogInfo($"{PluginName} v{PluginVersion} has loaded!");
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }
    }
}

