using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Collections.Generic;
using UnityEngine;

namespace DvergrAllies
{
    public class CustomDvergrDef
    {
        public string PrefabName;
        public string BasePrefab;      // Dictates the visual suit (Dverger, DvergerMageFire, etc.)
        public string DisplayName;
        public string[] Loadout;       // Cosmetics, attack prefabs, and weapons
        public float HealthMultiplier = 1f;
        public bool StripArmor = false; // Used for Berserker to spawn naked
        public bool AddRage = false;    // Used for Berserker to add rage component
        public bool IsMeleeAI = false;  // Charge in!
        public bool IsRangedAI = false; // Stand ground but don't kite
        public bool IsClericAI = false; // Heal allies
}

    public static class AllyPrefabManager
    {
        // Define all our custom combo variants here in one easy-to-edit place!
        public static List<CustomDvergrDef> CustomVariants = new List<CustomDvergrDef>()
        {
            new CustomDvergrDef {
                PrefabName = "AllyDvergrMageElemental",
                BasePrefab = "DvergerMageFire",
                DisplayName = "Dvergr Elemental Mage",
                Loadout = new string[0], // Handled at runtime
                IsRangedAI = true
            },
            new CustomDvergrDef {
                PrefabName = "AllyDvergrSpellswordFire",
                BasePrefab = "DvergerMageFire", // Changed to Mage so the animator supports the magic properly!
                DisplayName = "Dvergr Fire Spellsword",
                Loadout = new[] { 
                    "AllyDvergr_SwordBronze" // Fireball added at runtime
                },
                IsMeleeAI = true
            },
            new CustomDvergrDef {
                PrefabName = "AllyDvergrSpellswordIce",
                BasePrefab = "DvergerMageIce", // Changed to Mage so the animator supports the magic properly!
                DisplayName = "Dvergr Ice Spellsword",
                Loadout = new[] { 
                    "AllyDvergr_SwordBronze" // Icebolt added at runtime
                },
                IsMeleeAI = true
            },
            new CustomDvergrDef {
                PrefabName = "AllyDvergrCleric",
                BasePrefab = "DvergerMageSupport",
                DisplayName = "Dvergr Cleric",
                Loadout = new[] { 
                    "AllyDvergr_MaceBronze", 
                    "DvergerStaffHeal_heal", "DvergerStaffSupport_buff" 
                },
                IsClericAI = true
            },
            new CustomDvergrDef {
                PrefabName = "AllyDvergrWarrior",
                BasePrefab = "Dverger",
                DisplayName = "Dvergr Warrior",
                Loadout = new[] { 
                    "AllyDvergr_SwordBronze", "ShieldBanded"
                },
                HealthMultiplier = 1.5f,
                IsMeleeAI = true
            },
            new CustomDvergrDef {
                PrefabName = "AllyDvergrBerserker",
                BasePrefab = "Dverger",
                DisplayName = "Dvergr Berserker",
                Loadout = new[] { 
                    "AllyDvergr_Battleaxe"
                },
                StripArmor = false,
                AddRage = true,
                IsMeleeAI = true
}
        };

        public static void Setup()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Setup;
            Jotunn.Logger.LogInfo("Setting up Ally Dvergr prefabs...");

            // 1. Base variants (keep vanilla loadout intact)
            CreateAllyVariant("Dverger", "AllyDvergrRogue", "Dvergr Rogue");
            CreateAllyVariant("DvergerMage", "AllyDvergrMage", "Dvergr Mage");
            CreateAllyVariant("DvergerMageFire", "AllyDvergrMageFire", "Dvergr Fire Mage");
            CreateAllyVariant("DvergerMageIce", "AllyDvergrMageIce", "Dvergr Ice Mage");
            CreateAllyVariant("DvergerMageSupport", "AllyDvergrMageSupport", "Dvergr Support Mage");

            // 2. Combo variants (custom weapon loadouts built from definitions)
            foreach (var def in CustomVariants)
            {
                CreateCustomVariant(def);
}

            // 3. Pre-clone scaled weapons so Jotunn registers them in ZNetScene at load time!
            GetOrCloneWeaponWithMeleeAnim("AllyDvergr_SwordIron");
            GetOrCloneWeaponWithMeleeAnim("AllyDvergr_SwordBlackmetal");
            GetOrCloneWeaponWithMeleeAnim("AllyDvergr_MaceIron");
            GetOrCloneWeaponWithMeleeAnim("AllyDvergr_MaceBlackmetal");
            GetOrCloneWeaponWithMeleeAnim("AllyDvergr_BattleaxeBlackmetal");

            Jotunn.Logger.LogInfo("Ally Dvergr prefabs created.");
}

        // We now use a Harmony Patch in ZNetScene to catch all dynamically loaded Dvergrs (including Ashlands/Mods)
        public static void MakePrefabTamable(GameObject prefab)
        {
            if (prefab == null) return;

            // Add Custom Tameable
            Tameable oldTameable = prefab.GetComponent<Tameable>();
            if (oldTameable != null) UnityEngine.Object.DestroyImmediate(oldTameable);

            DvergrTameable tameable = prefab.GetComponent<DvergrTameable>();
            if (tameable == null) tameable = prefab.AddComponent<DvergrTameable>();
            
            tameable.m_tamingTime = ConfigManager.TamingTime.Value;
            tameable.m_fedDuration = ConfigManager.FedDuration.Value;
            tameable.m_commandable = true;

            // Add Procreation so wild ones can breed
            Procreation oldProc = prefab.GetComponent<Procreation>();
            if (oldProc != null) UnityEngine.Object.DestroyImmediate(oldProc);

            DvergrProcreation dvergrProc = prefab.GetComponent<DvergrProcreation>();
            if (dvergrProc == null) prefab.AddComponent<DvergrProcreation>();

            if (prefab.GetComponent<DvergrGenetics>() == null)
                prefab.AddComponent<DvergrGenetics>();

            // Configure MonsterAI for eating and disable passive-aggressiveness
            MonsterAI monsterAI = prefab.GetComponent<MonsterAI>();
            if (monsterAI != null)
            {
                // If true, they get angry if you stand near them too long (Mistlands Dvergrs do this!)
                // We must disable this so players can safely stand nearby to tame them.
                monsterAI.m_passiveAggresive = false;
                monsterAI.m_fleeIfNotAlerted = false; // Prevent immediate fleeing if startled
                monsterAI.m_fleeIfLowHealth = 0f;
                monsterAI.m_attackPlayerObjects = false; // Prevent them from attacking the player's taming pens!
                monsterAI.m_avoidFire = false; // Prevent panicking near campfires

                if (monsterAI.m_consumeItems == null) monsterAI.m_consumeItems = new List<ItemDrop>();
                var itemNames = ConfigManager.TamingItems.Value.Split(',');
                foreach (var itemName in itemNames)
                {
                    GameObject itemPrefab = PrefabManager.Instance.GetPrefab(itemName.Trim());
                    if (itemPrefab != null)
                    {
                        ItemDrop itemDrop = itemPrefab.GetComponent<ItemDrop>();
                        if (itemDrop != null && !monsterAI.m_consumeItems.Contains(itemDrop)) 
                            monsterAI.m_consumeItems.Add(itemDrop);
}
}
                if (monsterAI.m_consumeSearchRange < 10f) monsterAI.m_consumeSearchRange = 10f;
                if (monsterAI.m_consumeSearchInterval < 10f) monsterAI.m_consumeSearchInterval = 10f;
                monsterAI.m_consumeRange = ConfigManager.ConsumeRange.Value;
}
}

        public static GameObject GetOrCloneWeaponWithMeleeAnim(string itemName)
        {

            if (itemName.StartsWith("AllyDvergr_"))
            {
                string baseItemName = itemName.Replace("AllyDvergr_", "");
                
                GameObject existing = PrefabManager.Instance.GetPrefab(itemName);
                if (existing != null) return existing;

                GameObject baseWeapon = PrefabManager.Instance.GetPrefab(baseItemName);
                if (baseWeapon == null) return null;

                GameObject cloned = PrefabManager.Instance.CreateClonedPrefab(itemName, baseItemName);
                CustomItem customItem = new CustomItem(cloned, true);

                ItemDrop drop = customItem.ItemDrop;
                if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
                {
                    // Melee weapon fixes: Perform 'Brain Transplant' with Dverger_melee to ensure 100% monster engine compatibility
                    if (itemName.Contains("Sword") || itemName.Contains("Mace") || itemName.Contains("Battleaxe") || itemName.Contains("Shield"))
                    {
                        GameObject dvergerMelee = PrefabManager.Instance.GetPrefab("Dverger_melee");
                        if (dvergerMelee != null)
                        {
                            ItemDrop dvergerDrop = dvergerMelee.GetComponent<ItemDrop>();
                            if (dvergerDrop != null && dvergerDrop.m_itemData.m_shared != null)
                            {
                                drop.m_itemData.m_shared.m_attack = dvergerDrop.m_itemData.m_shared.m_attack.Clone();
}
}

                        // Set proper ranges
                        drop.m_itemData.m_shared.m_aiAttackInterval = 1.2f;
                        drop.m_itemData.m_shared.m_aiAttackRange = 3.5f;
                        drop.m_itemData.m_shared.m_attack.m_attackRange = 3.5f;
                        
                        // CRITICAL FIX: Zero out stamina/health/eitr costs so NPCs can actually attack!
                        drop.m_itemData.m_shared.m_attack.m_attackStamina = 0f;
                        drop.m_itemData.m_shared.m_attack.m_attackHealth = 0f;
                        drop.m_itemData.m_shared.m_attack.m_attackHealthPercentage = 0f;
                        drop.m_itemData.m_shared.m_attack.m_attackEitr = 0f;
                        
                        // Give our allies infinite durability on weapons so they never break
                        drop.m_itemData.m_shared.m_useDurability = false;
}

                    // Berserker Damage Buff
                    if (itemName.Contains("Battleaxe"))
                    {
                        drop.m_itemData.m_shared.m_damages.m_slash *= 1.5f;
}

                    // Apply Global Config Damage Multiplier to the weapon
                    float dmgMult = ConfigManager.AllyDamageMultiplier.Value;
                    if (dmgMult != 1.0f)
                    {
                        drop.m_itemData.m_shared.m_damages.m_damage *= dmgMult;
                        drop.m_itemData.m_shared.m_damages.m_blunt *= dmgMult;
                        drop.m_itemData.m_shared.m_damages.m_slash *= dmgMult;
                        drop.m_itemData.m_shared.m_damages.m_pierce *= dmgMult;
                        drop.m_itemData.m_shared.m_damages.m_chop *= dmgMult;
                        drop.m_itemData.m_shared.m_damages.m_pickaxe *= dmgMult;
                        drop.m_itemData.m_shared.m_damages.m_fire *= dmgMult;
                        drop.m_itemData.m_shared.m_damages.m_frost *= dmgMult;
                        drop.m_itemData.m_shared.m_damages.m_lightning *= dmgMult;
                        drop.m_itemData.m_shared.m_damages.m_poison *= dmgMult;
                        drop.m_itemData.m_shared.m_damages.m_spirit *= dmgMult;
}
}

                ItemManager.Instance.AddItem(customItem);
                return cloned;
}

            return PrefabManager.Instance.GetPrefab(itemName);
}

        /// <summary>
        /// Gets the suit name for a custom class based on its base prefab.
        /// </summary>
        private static string GetSuitName(string basePrefabName)
        {
            switch (basePrefabName)
            {
                case "Dverger": return "DvergerSuitArbalest";
                case "DvergerMageFire": return "DvergerSuitFire";
                case "DvergerMageIce": return "DvergerSuitIce";
                case "DvergerMageSupport": return "DvergerSuitSupport";
                default: return null;
}
}

        private static void CreateCustomVariant(CustomDvergrDef def)
        {
            CreateAllyVariant(def.BasePrefab, def.PrefabName, def.DisplayName, def.HealthMultiplier, def.IsMeleeAI, def.IsRangedAI, def.IsClericAI);

            GameObject comboObj = PrefabManager.Instance.GetPrefab(def.PrefabName);
            if (comboObj == null) return;
            Humanoid comboHum = comboObj.GetComponent<Humanoid>();
            if (comboHum == null) return;

            // === STEP 1: Completely clear all item sources ===
            comboHum.m_randomSets = new Humanoid.ItemSet[0];
            comboHum.m_randomWeapon = new GameObject[0];
            comboHum.m_randomShield = new GameObject[0];
            if (def.StripArmor) comboHum.m_randomArmor = new GameObject[0];

            // === STEP 2: Build clean m_defaultItems with suit + class loadout ===
            var newDefaults = new List<GameObject>();

            // Add the suit (clothing) — unless stripped
            if (!def.StripArmor)
            {
                string suitName = GetSuitName(def.BasePrefab);
                if (suitName != null)
                {
                    GameObject suitPrefab = PrefabManager.Instance.GetPrefab(suitName);
                    if (suitPrefab != null)
                    {
                        newDefaults.Add(suitPrefab);
                        ConfigManager.LogEquipment($"[CustomVariant] {def.PrefabName}: Added suit: {suitName}");
}
}
}

            // Add class-specific weapons and attack items
            foreach (string itemName in def.Loadout)
            {
                GameObject itemPrefab = GetOrCloneWeaponWithMeleeAnim(itemName);
                if (itemPrefab != null)
                {
                    newDefaults.Add(itemPrefab);
                    ConfigManager.LogEquipment($"[CustomVariant] {def.PrefabName}: Added: {itemName}");
}
                else
                {
                    Jotunn.Logger.LogWarning($"[CustomVariant] {def.PrefabName}: Could not find prefab '{itemName}'!");
}
}

            comboHum.m_defaultItems = newDefaults.ToArray();
            comboHum.m_unarmedWeapon = null; // Completely strip the unarmed punch so they MUST use their weapon!
            ConfigManager.LogEquipment($"[CustomVariant] {def.PrefabName}: Final m_defaultItems count: {newDefaults.Count}");

            // === STEP 3: Class-specific logic ===
            if (def.AddRage)
            {
                comboObj.AddComponent<DvergrBerserkerRage>();
}

            DvergrCombatAI aiComponent = comboObj.GetComponent<DvergrCombatAI>();
            if (aiComponent != null && def.PrefabName == "AllyDvergrMageElemental")
            {
                aiComponent.IsElementalMage = true;
}
}

        private static void CreateAllyVariant(string basePrefabName, string newPrefabName, string displayName, float healthMultiplier = 1f, bool isMelee = false, bool isRanged = false, bool isCleric = false)
        {
            GameObject basePrefab = PrefabManager.Instance.GetPrefab(basePrefabName);
            if (basePrefab == null)
            {
                Jotunn.Logger.LogWarning($"Base prefab {basePrefabName} not found!");
                return;
}

            GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(newPrefabName, basePrefabName);
            if (prefab == null) return;
            CustomPrefab customPrefab = new CustomPrefab(prefab, true);

            StripBalrondComponents(prefab);

            // 1. Add Custom Tameable
            Tameable oldTameable = prefab.GetComponent<Tameable>();
            if (oldTameable != null) UnityEngine.Object.DestroyImmediate(oldTameable);

            DvergrTameable tameable = prefab.GetComponent<DvergrTameable>();
            if (tameable == null) tameable = prefab.AddComponent<DvergrTameable>();
            
            tameable.m_tamingTime = ConfigManager.TamingTime.Value;
            tameable.m_fedDuration = ConfigManager.FedDuration.Value;
            tameable.m_commandable = true;

            // Add MonsterAI adjustments
            MonsterAI monsterAI = prefab.GetComponent<MonsterAI>();
            if (monsterAI != null)
            {
                // Consume items (taming food)
                var itemNames = ConfigManager.TamingItems.Value.Split(',');
                monsterAI.m_consumeItems = new List<ItemDrop>();
                foreach (var itemName in itemNames)
                {
                    GameObject itemPrefab = PrefabManager.Instance.GetPrefab(itemName.Trim());
                    if (itemPrefab != null)
                    {
                        ItemDrop itemDrop = itemPrefab.GetComponent<ItemDrop>();
                        if (itemDrop != null) 
                            monsterAI.m_consumeItems.Add(itemDrop);
                        else
                            Jotunn.Logger.LogWarning($"Item {itemName} is missing ItemDrop component!");
}
                    else
                    {
                        Jotunn.Logger.LogWarning($"Could not find taming item prefab: {itemName}");
}
}
                
                monsterAI.m_consumeSearchRange = 10f;
                monsterAI.m_consumeSearchInterval = 10f;
                monsterAI.m_consumeRange = ConfigManager.ConsumeRange.Value;
                
                // Vanilla Dvergers are neutral NPCs — these flags make them fight
                monsterAI.m_aggravatable = false;
                monsterAI.m_passiveAggresive = false;
                monsterAI.m_fleeIfLowHealth = 0f;
                monsterAI.m_fleeIfNotAlerted = false;
                monsterAI.m_circulateWhileCharging = false;
                monsterAI.m_circulateWhileChargingFlying = false;
                monsterAI.m_avoidFire = false;
                monsterAI.m_avoidWater = false;
                monsterAI.m_interceptTimeMax = 2f;
                monsterAI.m_interceptTimeMin = 0f;
                monsterAI.m_maxChaseDistance = 0f;
                monsterAI.m_alertRange = 30f;
                monsterAI.m_attackPlayerObjects = false;
}

            // 2. Add Custom Procreation
            Procreation oldProc = prefab.GetComponent<Procreation>();
            if (oldProc != null) UnityEngine.Object.DestroyImmediate(oldProc);

            DvergrProcreation dvergrProc = prefab.GetComponent<DvergrProcreation>();
            if (dvergrProc == null) dvergrProc = prefab.AddComponent<DvergrProcreation>();

            // 3. Add Genetics Component
            prefab.AddComponent<DvergrGenetics>();

            // 4. Add Weapon Scaler (tier upgrades for starred units)
            prefab.AddComponent<DvergrWeaponScaler>();

            // 5. Add Combat AI override (runtime aggressive behavior enforcement)
            DvergrCombatAI combatAI = prefab.AddComponent<DvergrCombatAI>();
            combatAI.IsMeleeClass = isMelee;
            combatAI.IsRangedClass = isRanged;
            combatAI.IsClericClass = isCleric;

            // 6. Adjust Stats & Faction
            Character character = prefab.GetComponent<Character>();
            if (character != null)
            {
                character.m_health *= (ConfigManager.AllyHealthMultiplier.Value * healthMultiplier);
                character.m_faction = Character.Faction.Players; 
                character.m_runSpeed = Mathf.Max(character.m_runSpeed, 6.5f);
                character.m_walkSpeed = Mathf.Max(character.m_walkSpeed, 3f);
}

            Humanoid humanoid = prefab.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                humanoid.m_name = displayName;
}

            PrefabManager.Instance.AddPrefab(customPrefab);
}

        private static void StripBalrondComponents(GameObject prefab)
        {
            if (!Plugin.HasBalrondIdleActors) return;

            var componentsToStrip = new string[] { 
                "BalrondActor", 
                "BalrondActorTalker", 
                "BalrondActorNamePicker" 
            };

            foreach (var compName in componentsToStrip)
            {
                var comp = prefab.GetComponent(compName);
                if (comp != null)
                {
                    UnityEngine.Object.DestroyImmediate(comp);
                    Jotunn.Logger.LogInfo($"[DvergrAllies] Stripped {compName} from cloned {prefab.name}");
                    ConfigManager.LogDebug($"[Compat] Removed {compName} from {prefab.name} to prevent Balrond Idle Actors conflict.");
                }
            }
        }
}

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    public static class Patch_ZNetScene_Awake_Dvergrs
    {
        [HarmonyPostfix]
        public static void Postfix(ZNetScene __instance)
        {
            if (__instance == null || __instance.m_prefabs == null) return;
            
            // Compat check for BalrondIdleActors
            if (Plugin.HasBalrondIdleActors)
            {
                UnityEngine.Debug.LogError("=======================================================================");
                UnityEngine.Debug.LogError("[DvergrAllies] BalrondIdleActors detected! Skipping wild Dvergr injection!");
                UnityEngine.Debug.LogError("[DvergrAllies] Enabling compatibility mode: Wild Dvergr modifications are disabled.");
                UnityEngine.Debug.LogError("=======================================================================");
                return;
            }

            Jotunn.Logger.LogInfo("Scanning ZNetScene for wild Dvergr prefabs to make tamable...");
            int count = 0;
            foreach (var prefab in __instance.m_prefabs)
            {
                if (prefab == null) continue;
                string name = prefab.name.ToLower();
                
                // Catch anything named dvergr or dverger, but skip our custom allies
                if ((name.Contains("dverg") || name.Contains("dverger")) && !name.Contains("ally"))
                {
                    if (prefab.GetComponent<Humanoid>() != null && prefab.GetComponent<MonsterAI>() != null)
                    {
                        AllyPrefabManager.MakePrefabTamable(prefab);
                        count++;
}
}
}
            Jotunn.Logger.LogInfo($"Successfully injected Taming/Breeding components into {count} wild Dvergr prefabs!");
        }
    }

    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
    public static class Patch_MonsterAI_UpdateAI_Dvergrs
    {
        [HarmonyPrefix]
        public static void Prefix(MonsterAI __instance)
        {
            if (__instance == null || __instance.gameObject == null) return;
            string name = __instance.gameObject.name.ToLower();
            
            // If this is a wild Dvergr, FORCE these values every frame to prevent random hostility
            // This bypasses any ZDO syncs or hidden Valheim engine overrides
            if ((name.Contains("dverg") || name.Contains("dverger")) && !name.Contains("ally"))
            {
                // Compat check for BalrondIdleActors
                if (Plugin.HasBalrondIdleActors)
                {
                    return;
                }

                // Valkyrie's Cargo's merchant is a "Dverger(Clone)" too; his AI is CargoMerchant's to drive.
                if (ValkyriesCargoCompat.IsExcluded(__instance.gameObject)) return;

                __instance.m_passiveAggresive = false;
                __instance.m_attackPlayerObjects = false;
                __instance.m_avoidFire = false;
                __instance.m_fleeIfNotAlerted = false;
            }
        }
    }
}
