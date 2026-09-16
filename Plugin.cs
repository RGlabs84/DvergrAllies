using BepInEx;
using HarmonyLib;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DvergrAllies
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Soft: only orders us after Valkyrie's Cargo when it is present, so the detection line below is
    // accurate. Ingvar handling itself keys off his ZDO and works whether or not this ever resolves.
    [BepInDependency(ValkyriesCargoCompat.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // Patch, not Minor (1.0.8): every client and the server must run the exact same version. Minor let a
    // 1.0.7 client into a 1.0.8 session, where its still-live DvergrTameable could pet/command/rename
    // Valkyrie's Cargo's merchant and write follow/name keys onto his ZDO that the new build cannot stop
    // (see ValkyriesCargoCompat). Patch releases of this mod change Harmony patches and ZDO writes, so
    // exact-match is the honest contract; Jotunn shows the mismatch screen with both versions on it.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    internal class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "wubarrk.dvergrallies";
        public const string PluginName = "DvergrAllies";
        public const string PluginVersion = "1.0.8";

        public static bool HasBalrondIdleActors;

        private readonly Harmony harmony = new Harmony(PluginGUID);

        private void Awake()
        {
            // Matched IGNORING CASE - Chainloader.PluginInfos is ordinal/case-sensitive, so a plugin that
            // re-cases its own GUID between releases silently stops being detected. See PluginLookup.
            HasBalrondIdleActors = PluginLookup.IsLoaded("balrond.astafaraios.BalrondIdleActors");

            ValkyriesCargoCompat.HasValkyriesCargo = PluginLookup.IsLoaded(ValkyriesCargoCompat.PluginGuid);
            if (ValkyriesCargoCompat.HasValkyriesCargo)
                Logger.LogInfo("[Compat] Valkyrie's Cargo detected: its merchant Ingvar (ZDO key " +
                               ValkyriesCargoCompat.IngvarKey + ") will never be tamed, bred, petted, renamed or counted.");

            // Initialize config
            ConfigManager.Init(Config);

            // Subscribe to Jotunn's prefab event
            Jotunn.Managers.PrefabManager.OnVanillaPrefabsAvailable += CustomStavesManager.Setup;
            Jotunn.Managers.PrefabManager.OnVanillaPrefabsAvailable += AllyPrefabManager.Setup;
            Jotunn.Managers.PrefabManager.OnVanillaPrefabsAvailable += RecruiterManager.Setup;

            // Apply Harmony patches
            ApplyPatches();

            gameObject.AddComponent<DvergrStatsExporter>();

            Logger.LogInfo($"{PluginName} v{PluginVersion} has loaded!");
        }

        // Harmony's PatchAll() is all-or-nothing: the first patch class whose target method cannot be
        // resolved throws straight out of Awake, so every patch class after it in metadata order is
        // never applied and the rest of Awake never runs. Unity swallows that exception into the Unity
        // log, and BepInEx ships with WriteUnityLog = false, so on a dedicated server it leaves no
        // trace in LogOutput.log whatsoever.
        //
        // That is not hypothetical: 1.0.4 and 1.0.5 aimed a patch at Humanoid.OnDeath, which Humanoid
        // does not declare. The throw cost the stats exporter (never constructed) AND the Haldor
        // contract (its patch class sorts after the bad one), and the only symptom anyone could see
        // was a missing folder. Patching class by class means a bad target now costs exactly its own
        // patch, and says so in the log everybody actually reads.
        private void ApplyPatches()
        {
            int applied = 0;
            List<string> failed = new List<string>();

            foreach (Type type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
            {
                try
                {
                    List<MethodInfo> patched = harmony.CreateClassProcessor(type).Patch();
                    if (patched != null && patched.Count > 0) applied++;
                }
                catch (Exception e)
                {
                    failed.Add(type.Name);
                    Jotunn.Logger.LogError(
                        $"[Patches] {type.Name} could not be applied and was SKIPPED - the feature it " +
                        $"backs is inactive this session, everything else still loaded: {e.Message}");
                }
            }

            if (failed.Count == 0)
                Jotunn.Logger.LogInfo($"[Patches] {applied} applied, none failed.");
            else
                Jotunn.Logger.LogError(
                    $"[Patches] {applied} applied, {failed.Count} FAILED: {string.Join(", ", failed.ToArray())}.");
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }
    }
}

