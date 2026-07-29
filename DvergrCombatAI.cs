using UnityEngine;
using System.Collections.Generic;
namespace DvergrAllies
{
    /// <summary>
    /// Runtime component that forces ally Dvergrs to actually fight instead of flee.
    /// The vanilla Dverger is a neutral NPC with aggravatable/passive AI flags.
    /// This component overrides those at runtime after all vanilla systems have initialized.
    /// </summary>
    public class DvergrCombatAI : MonoBehaviour
    {
        public bool IsMeleeClass = false;
        public bool IsRangedClass = false;
        public bool IsClericClass = false;
        public bool IsElementalMage = false;
        
        private MonsterAI m_monsterAI;
        private Character m_character;
        private bool m_initialized = false;
        private bool m_isConfigured = false;
        private float m_clericHealTimer = 15f; // Start off cooldown

        private void Awake()
        {
            m_monsterAI = GetComponent<MonsterAI>();
            m_character = GetComponent<Character>();

            m_isConfigured = false;

            if (Plugin.HasBalrondIdleActors)
            {
                var componentsToStrip = new string[] { "BalrondActor", "BalrondActorTalker", "BalrondActorNamePicker" };
                foreach (var compName in componentsToStrip)
                {
                    var comp = GetComponent(compName);
                    if (comp != null)
                    {
                        Destroy(comp);
                        ConfigManager.LogDebug($"[Compat] Runtime stripped {compName} from existing active {gameObject.name}");
                    }
                }
            }
        }

        private void Start()
        {
            ForceAggressiveAI();
            Invoke(nameof(ForceAggressiveAI), 1f);
            Invoke(nameof(ForceAggressiveAI), 3f);
            
            // Execute the magic injection slightly delayed to ensure inventory is initialized
            Invoke(nameof(InjectMagicWeapons), 0.5f);
        }

        private void ForceAggressiveAI()
        {
            if (m_monsterAI == null) return;

            // Universal flags for ALL allies
            m_monsterAI.m_aggravatable = false;
            m_monsterAI.m_passiveAggresive = false;
            m_monsterAI.m_fleeIfNotAlerted = false;
            m_monsterAI.m_avoidFire = false;
            m_monsterAI.m_avoidWater = false;
            m_monsterAI.m_fleeIfLowHealth = 0f;
            m_monsterAI.m_attackPlayerObjects = false;

            // Behavior-specific overrides
            if (IsMeleeClass)
            {
                // Full charge, never circle, no random wandering during combat
                m_monsterAI.m_circulateWhileCharging = false;
                m_monsterAI.m_circulateWhileChargingFlying = false;
                m_monsterAI.m_randomMoveInterval = 0f;
                m_monsterAI.m_randomCircleInterval = 0f;
                m_monsterAI.m_alertRange = 30f;
            }
            else if (IsRangedClass)
            {
                // Stand ground and shoot, don't circle like a chicken
                m_monsterAI.m_circulateWhileCharging = false;
                m_monsterAI.m_circulateWhileChargingFlying = false;
                m_monsterAI.m_randomMoveInterval = 10f; 
            }
            else if (IsClericClass)
            {
                // Cleric should STAY BACK and heal, not charge into melee
                m_monsterAI.m_circulateWhileCharging = false;
                m_monsterAI.m_randomMoveInterval = 5f;
                m_monsterAI.m_randomCircleInterval = 5f;
                m_monsterAI.m_alertRange = 30f;
            }

            if (!m_initialized)
            {
                m_initialized = true;
                ConfigManager.LogAI($"[CombatAI] Forced aggressive AI on {gameObject.name} (Melee:{IsMeleeClass}, Ranged:{IsRangedClass})");
            }
        }

        private float m_healScanTimer = 0f;
        private float m_elementRotateTimer = 0f;
        private List<ItemDrop.ItemData> m_elementalStaves = new List<ItemDrop.ItemData>();

        private void FixedUpdate()
        {
            if (m_monsterAI == null) return;

            // Enforce aggressive AI flags every second
            if (Time.frameCount % 60 == 0)
            {
                if (m_monsterAI.m_aggravatable || m_monsterAI.m_passiveAggresive || m_monsterAI.m_fleeIfNotAlerted)
                {
                    ConfigManager.LogAI($"[CombatAI] AI flags were reset on {gameObject.name}, re-forcing!");
                    ForceAggressiveAI();
                }
            }

            // Force lock melee pacing every frame to stop engine overrides
            if (IsMeleeClass || IsClericClass)
            {
                m_monsterAI.m_randomCircleInterval = 9999f;
                m_monsterAI.m_circulateWhileCharging = false;
            }

            // Custom Leash Logic
            GameObject followTarget = m_monsterAI.GetFollowTarget();
            if (followTarget != null)
            {
                Character enemy = m_monsterAI.GetTargetCreature();
                if (enemy != null)
                {
                    float distToLeader = Vector3.Distance(transform.position, followTarget.transform.position);
                    if (distToLeader > ConfigManager.MaxFollowLeash.Value)
                    {
                        m_monsterAI.SetTarget(null);
                        ConfigManager.LogAI($"[Leash] {gameObject.name} strayed too far ({distToLeader:F1}m > {ConfigManager.MaxFollowLeash.Value}m), dropping aggro to return to leader.");
                    }
                }
            }

            // Custom Cleric Healing Logic
            if (IsClericClass)
            {
                m_clericHealTimer += Time.fixedDeltaTime;

                m_healScanTimer += Time.fixedDeltaTime;
                if (m_healScanTimer >= 2f)
                {
                    m_healScanTimer = 0f;
                    UpdateClericAI();
                }
            }

            // Custom Elemental Mage Rotation
            if (IsElementalMage)
            {
                UpdateElementalMageAI();
            }
        }

        private void UpdateClericAI()
        {
            if (m_monsterAI.m_character != null && m_monsterAI.m_character.InAttack()) return;

            // 15-second cooldown for Cleric AoE Heal
            if (m_clericHealTimer < 15f) return;

            bool playedAnimation = false;
            int clericLevel = m_monsterAI.m_character.GetLevel();

            Collider[] array = Physics.OverlapSphere(transform.position, 15f, LayerMask.GetMask("character", "character_net", "character_ghost"));
            foreach (Collider col in array)
            {
                Character c = col.GetComponent<Character>();
                if (c != null && !c.IsDead())
                {
                    // Heal ONLY self, players, and explicitly tamed allies (excludes wild vanilla Dvergrs!)
                    if (c == m_monsterAI.m_character || c.IsPlayer() || c.IsTamed())
                    {
                        if (c.GetHealthPercentage() < 0.8f)
                        {
                            if (!playedAnimation)
                            {
                                m_monsterAI.m_character.GetZAnim().SetTrigger("StaffCast");
                                playedAnimation = true;
                                m_clericHealTimer = 0f; // Reset cooldown ONLY if we actually healed someone
                                ConfigManager.LogAI($"[Cleric] Triggered AoE HoT Pulse (Level {clericLevel})");
                                
                                if (ZNetScene.instance != null)
                                {
                                    GameObject vfx = ZNetScene.instance.GetPrefab("vfx_Potion_health_medium");
                                    if (vfx != null) Instantiate(vfx, transform.position, Quaternion.identity);
                                    GameObject vfx2 = ZNetScene.instance.GetPrefab("vfx_creature_soothed");
                                    if (vfx2 != null) Instantiate(vfx2, transform.position + Vector3.up, Quaternion.identity);
                                }
                            }
                            
                            // Attach or Refresh HoT component
                            DvergrHoT[] existingHots = c.gameObject.GetComponents<DvergrHoT>();
                            foreach (var oldHot in existingHots)
                            {
                                Destroy(oldHot);
                            }
                            
                            DvergrHoT hot = c.gameObject.AddComponent<DvergrHoT>();
                            hot.Setup(clericLevel);
                        }
                    }
                }
            }
        }

        private ItemDrop.ItemData CreateNativeVariant(string baseWeaponName, string newProjectileName = null)
        {
            if (ObjectDB.instance == null || ZNetScene.instance == null) return null;
            
            GameObject baseWep = ObjectDB.instance.GetItemPrefab(baseWeaponName) ?? ZNetScene.instance.GetPrefab(baseWeaponName);
            if (baseWep == null)
            {
                ConfigManager.LogAI($"[NativeVariant] Base weapon {baseWeaponName} not found!");
                return null;
            }

            ItemDrop.ItemData clone = baseWep.GetComponent<ItemDrop>().m_itemData.Clone();
            clone.m_dropPrefab = baseWep;

            var origShared = clone.m_shared;
            clone.m_shared = new ItemDrop.ItemData.SharedData();
            var fields = typeof(ItemDrop.ItemData.SharedData).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var f in fields) f.SetValue(clone.m_shared, f.GetValue(origShared));
            
            clone.m_shared.m_attack = origShared.m_attack.Clone();
            if (origShared.m_secondaryAttack != null) clone.m_shared.m_secondaryAttack = origShared.m_secondaryAttack.Clone();

            // Set stamina costs to 0 to ensure NPCs always cast
            clone.m_shared.m_attack.m_attackStamina = 0f;
            clone.m_shared.m_attack.m_attackHealth = 0f;
            clone.m_shared.m_attack.m_attackEitr = 0f;

            // Apply Global Config Damage Multiplier to the magic weapon
            float dmgMult = ConfigManager.AllyDamageMultiplier.Value;
            if (dmgMult != 1.0f)
            {
                clone.m_shared.m_damages.m_damage *= dmgMult;
                clone.m_shared.m_damages.m_blunt *= dmgMult;
                clone.m_shared.m_damages.m_slash *= dmgMult;
                clone.m_shared.m_damages.m_pierce *= dmgMult;
                clone.m_shared.m_damages.m_chop *= dmgMult;
                clone.m_shared.m_damages.m_pickaxe *= dmgMult;
                clone.m_shared.m_damages.m_fire *= dmgMult;
                clone.m_shared.m_damages.m_frost *= dmgMult;
                clone.m_shared.m_damages.m_lightning *= dmgMult;
                clone.m_shared.m_damages.m_poison *= dmgMult;
                clone.m_shared.m_damages.m_spirit *= dmgMult;
            }

            if (newProjectileName != null)
            {
                GameObject proj = ObjectDB.instance.GetItemPrefab(newProjectileName) ?? ZNetScene.instance.GetPrefab(newProjectileName);
                if (proj != null)
                {
                    clone.m_shared.m_attack.m_attackProjectile = proj;
                }
            }

            return clone;
        }

        private void InjectMagicWeapons()
        {
            if (m_monsterAI == null || m_monsterAI.m_character == null) return;
            Humanoid hum = m_monsterAI.m_character as Humanoid;
            if (hum == null || hum.GetInventory() == null) return;

            ConfigManager.LogAI($"[RuntimeBuilder] {gameObject.name} - inventory has {hum.GetInventory().GetAllItems().Count} items");

            // 1. Nerf Heal staff if they have it
            foreach (var item in hum.GetInventory().GetAllItems())
            {
                if (item.IsWeapon() && item.m_dropPrefab != null && item.m_dropPrefab.name.Contains("Heal"))
                {
                    item.m_shared.m_aiAttackInterval = 30f;
                    ConfigManager.LogAI($"[RuntimeBuilder] NERFED heal staff: {item.m_dropPrefab.name} -> interval=30s");
                }
            }

            // 2. Spellsword Injection
            if (gameObject.name.Contains("SpellswordFire"))
            {
                ItemDrop.ItemData fireball = CreateNativeVariant("DvergerStaffFire_fireball");
                if (fireball != null)
                {
                    fireball.m_shared.m_aiAttackInterval = 15f; // Long cooldown so sword is prioritized!
                    hum.GetInventory().AddItem(fireball);
                    ConfigManager.LogAI($"[SpellswordFire] Injected Native Fireball into memory inventory!");
                }
            }
            else if (gameObject.name.Contains("SpellswordIce"))
            {
                ItemDrop.ItemData icebolt = CreateNativeVariant("DvergerStaffIce_icebolt");
                if (icebolt != null)
                {
                    icebolt.m_shared.m_aiAttackInterval = 15f; // Long cooldown so sword is prioritized!
                    hum.GetInventory().AddItem(icebolt);
                    ConfigManager.LogAI($"[SpellswordIce] Injected Native Icebolt into memory inventory!");
                }
            }

            // 3. Elemental Mage Payload Setup
            if (IsElementalMage)
            {
                m_elementalStaves.Clear();
                // Inject Native Elements
                m_elementalStaves.Add(CreateNativeVariant("DvergerStaffFire_fireball"));
                m_elementalStaves.Add(CreateNativeVariant("DvergerStaffIce_icebolt"));
                // Custom Native Variant: Dverger staff animations with Goblin projectile!
                m_elementalStaves.Add(CreateNativeVariant("DvergerStaffFire_fireball", "GoblinShaman_projectile_fireball"));
                
                m_elementalStaves.RemoveAll(x => x == null);
                
                // Clear any physical junk they spawned with
                List<ItemDrop.ItemData> toRemove = new List<ItemDrop.ItemData>();
                foreach (var item in hum.GetInventory().GetAllItems())
                {
                    if (item.IsWeapon())
                    {
                        hum.UnequipItem(item);
                        toRemove.Add(item);
                    }
                }
                foreach (var item in toRemove) hum.GetInventory().RemoveItem(item);

                // Immediately equip the first element
                if (m_elementalStaves.Count > 0)
                {
                    hum.GetInventory().AddItem(m_elementalStaves[0]);
                    hum.EquipItem(m_elementalStaves[0]);
                }

                ConfigManager.LogAI($"[ElementalMage] Loaded {m_elementalStaves.Count} elements into memory cache.");
                m_elementRotateTimer = 0f;
            }
        }

        private int m_elementSetIndex = 0;

        private void UpdateElementalMageAI()
        {
            if (m_monsterAI.m_character == null) return;
            Humanoid hum = m_monsterAI.m_character as Humanoid;
            if (hum == null) return;
            
            var inventory = hum.GetInventory();
            if (inventory == null) return;

            if (m_elementalStaves.Count == 0) return;

            m_elementRotateTimer += Time.fixedDeltaTime;
            if (m_elementRotateTimer >= 10f)
            {
                m_elementRotateTimer = 0f;
                m_elementSetIndex = (m_elementSetIndex + 1) % m_elementalStaves.Count;
                
                ItemDrop.ItemData activeWep = m_elementalStaves[m_elementSetIndex];

                // Completely obliterate all weapons from the Mage's inventory
                List<ItemDrop.ItemData> toRemove = new List<ItemDrop.ItemData>();
                foreach (var item in inventory.GetAllItems())
                {
                    if (item.IsWeapon())
                    {
                        hum.UnequipItem(item);
                        toRemove.Add(item);
                    }
                }
                foreach (var item in toRemove) inventory.RemoveItem(item);

                // Add and equip the active element
                inventory.AddItem(activeWep);
                hum.EquipItem(activeWep);

                ConfigManager.LogAI($"[ElementalMage] Swapped to element {m_elementSetIndex}");
            }
        }
    }
}
