using Jotunn.Managers;
using System;
using System.Collections;
using UnityEngine;

namespace DvergrAllies
{
    public class DvergrProcreation : MonoBehaviour
    {
        private ZNetView m_nview;
        private Character m_character;
        private BaseAI m_baseAI;
        private DvergrGenetics m_genetics;

        private Coroutine m_birthingCoroutine;

        public EffectList m_birthEffects = new EffectList();
        public float m_spawnOffset = 1.0f;

        private void Awake()
        {
            // Valkyrie's Cargo's merchant is a plain Dverger wearing this component off the shared prefab. He
            // is tamed (by VC, not by us) and must never breed. ExcludeIfIngvar has destroyed this component
            // when it returns true - nothing below may run. See ValkyriesCargoCompat.
            if (ValkyriesCargoCompat.ExcludeIfIngvar(gameObject, nameof(DvergrProcreation))) return;

            m_nview = GetComponent<ZNetView>();
            m_character = GetComponent<Character>();
            m_baseAI = GetComponent<BaseAI>();
            m_genetics = GetComponent<DvergrGenetics>();

            if (m_nview == null || !m_nview.IsValid()) return;

            InvokeRepeating(nameof(Procreate), UnityEngine.Random.Range(0f, ConfigManager.UpdateInterval.Value), ConfigManager.UpdateInterval.Value);
        }

        public bool IsPregnant()
        {
            return m_nview != null && m_nview.IsValid() && m_nview.GetZDO().GetLong("pregnant", 0L) != 0L;
        }

        public int GetPregnancyTimeLeft()
        {
            if (!IsPregnant()) return 0;
            long pregnantTime = m_nview.GetZDO().GetLong("pregnant", 0L);
            if (pregnantTime == 0L) return 0;

            DateTime start = new DateTime(pregnantTime);
            double elapsed = (ZNet.instance.GetTime() - start).TotalSeconds;
            int remaining = Mathf.CeilToInt((float)(ConfigManager.PregnancyDuration.Value - elapsed));
            return Mathf.Max(0, remaining);
        }

        private void MakePregnant(string partnerPrefab, int partnerLevel)
        {
            if (m_nview == null || !m_nview.IsValid()) return;
            m_nview.GetZDO().Set("pregnant", ZNet.instance.GetTime().Ticks);
            m_nview.GetZDO().Set("dvergr_partner_prefab", partnerPrefab);
            m_nview.GetZDO().Set("dvergr_partner_level", partnerLevel);
        }

        private void ResetPregnancy()
        {
            if (m_nview == null || !m_nview.IsValid()) return;
            m_nview.GetZDO().Set("pregnant", 0L);
            m_nview.GetZDO().Set("dvergr_partner_prefab", "");
        }

        private bool ReadyForProcreation()
        {
            if (IsPregnant()) return false;
            if (m_character == null || !m_character.IsTamed()) return false;
            if (m_baseAI != null && m_baseAI.IsAlerted()) return false;
            
            DvergrTameable tameable = GetComponent<DvergrTameable>();
            if (tameable != null && tameable.IsHungry()) return false;

            return true;
        }

        private void Procreate()
        {
            if (!m_nview.IsValid() || !m_nview.IsOwner()) return;

            if (IsPregnant())
            {
                long pregnantTime = m_nview.GetZDO().GetLong("pregnant", 0L);
                if (pregnantTime != 0L)
                {
                    DateTime start = new DateTime(pregnantTime);
                    if ((ZNet.instance.GetTime() - start).TotalSeconds > ConfigManager.PregnancyDuration.Value)
                    {
                        if (m_birthingCoroutine == null)
                        {
                            m_birthingCoroutine = StartCoroutine(BirthingProcess());
                        }
                    }
                }
                return;
            }

            if (!ReadyForProcreation()) return;

            // Check population limit for ALL tamed Dvergrs in the area
            string myPrefabName = gameObject.name.Replace("(Clone)", "").Trim();
            int nrOfInstances = 0;
            
            Collider[] popArray = Physics.OverlapSphere(transform.position, 10f, LayerMask.GetMask("character", "character_net", "character_ghost"));
            foreach (Collider collider in popArray)
            {
                Character component = collider.GetComponent<Character>();
                if (component != null && component.IsTamed() && component.GetComponent<DvergrGenetics>() != null)
                {
                    nrOfInstances++;
                }
            }
            
            if (nrOfInstances >= ConfigManager.BreedingLimit.Value)
            {
                ConfigManager.LogBreeding($"[Procreation] {myPrefabName} halted breeding: Population limit reached ({nrOfInstances}/{ConfigManager.BreedingLimit.Value}) tamed Dvergrs within 10m.");
                return;
            }

            // Find partner
            Collider[] array = Physics.OverlapSphere(transform.position, ConfigManager.PartnerCheckRange.Value, LayerMask.GetMask("character", "character_net", "character_ghost"));
            foreach (Collider collider in array)
            {
                Character component = collider.GetComponent<Character>();
                if (component != null && component.gameObject != gameObject)
                {
                    DvergrGenetics partnerGenetics = component.GetComponent<DvergrGenetics>();
                    if (partnerGenetics != null)
                    {
                        // Gender check (must be opposite)
                        if (m_genetics != null && m_genetics.GetGender() != partnerGenetics.GetGender())
                        {
                            DvergrProcreation partnerProcreation = component.GetComponent<DvergrProcreation>();
                            if (partnerProcreation != null && partnerProcreation.ReadyForProcreation())
                            {
                                // We have a valid pair!
                                ConfigManager.LogBreeding($"[Procreation] {myPrefabName} ({m_genetics.GetGender()}) found compatible partner {component.gameObject.name} ({partnerGenetics.GetGender()})");
                                
                                if (UnityEngine.Random.value <= ConfigManager.PregnancyChance.Value)
                                {
                                    string partnerPrefabName = component.gameObject.name.Replace("(Clone)", "").Trim();
                                    int partnerLevel = component.GetLevel();
                                    
                                    if (m_genetics.GetGender() == DvergrGenetics.Gender.Female)
                                    {
                                        MakePregnant(partnerPrefabName, partnerLevel);
                                        partnerProcreation.ResetPregnancy();
                                        ConfigManager.LogBreeding($"[Procreation] {myPrefabName} is now PREGNANT!");
                                    }
                                    else
                                    {
                                        string myPrefabNameForPartner = gameObject.name.Replace("(Clone)", "").Trim();
                                        int myLevel = m_character.GetLevel();
                                        partnerProcreation.MakePregnant(myPrefabNameForPartner, myLevel);
                                        ResetPregnancy();
                                        ConfigManager.LogBreeding($"[Procreation] Partner {partnerPrefabName} is now PREGNANT!");
                                    }
                                }
                                else
                                {
                                    ConfigManager.LogBreeding($"[Procreation] {myPrefabName} failed pregnancy chance roll.");
                                }
                                return; // Stop checking after finding a valid partner
                            }
                        }
                    }
                }
            }
        }

        private IEnumerator BirthingProcess()
        {
            string partnerPrefab = m_nview.GetZDO().GetString("dvergr_partner_prefab", "");
            int partnerLevel = m_nview.GetZDO().GetInt("dvergr_partner_level", 1);
            
            ResetPregnancy();

            string myPrefab = gameObject.name.Replace("(Clone)", "").Trim();
            string comboPrefab = DetermineOffspring(myPrefab, partnerPrefab, out bool wasSpecialCombo);

            ConfigManager.LogBreeding($"[Procreation] BIRTHING PROCESS START: ParentA={myPrefab}(Lvl {m_character.GetLevel()}), ParentB={partnerPrefab}(Lvl {partnerLevel}) -> Outcome={comboPrefab}");

            GameObject prefab = PrefabManager.Instance.GetPrefab(comboPrefab);
            if (prefab != null)
            {
                Vector3 spawnPos = transform.position + transform.forward * m_spawnOffset;
                GameObject offspring = Instantiate(prefab, spawnPos, Quaternion.LookRotation(transform.forward));

                Character childChar = offspring.GetComponent<Character>();
                if (childChar != null && m_character != null)
                {
                    // Both stamps go on BEFORE SetTamed, because SetTamed's postfix
                    // (Patch_Character_SetTamed_OwnerTracking) reads them:
                    //  - the owner tag, so it doesn't overwrite the bloodline with its nearby-player
                    //    guess - nobody may even be standing at the pen when a birth fires;
                    //  - the counted flag, so a birth isn't ALSO tallied as a wild tame.
                    long ownerId = 0L;
                    string ownerName = "Unknown";
                    ZNetView childNview = offspring.GetComponent<ZNetView>();
                    if (childNview != null && childNview.IsValid() && m_nview != null && m_nview.IsValid())
                    {
                        ZDO childZdo = childNview.GetZDO();
                        childZdo.Set(DvergrStatsExporter.TameCountedKey, 1);

                        // Offspring of an unattributed Dvergr stay unattributed - no invented owner.
                        if (DvergrStatsExporter.TryGetOwner(m_nview.GetZDO(), out ownerId, out ownerName))
                            DvergrStatsExporter.StampOwner(childZdo, ownerId, ownerName);
                    }

                    childChar.SetTamed(m_character.IsTamed());

                    // Level logic: Highest of both parents + chance to level up
                    int myLevel = m_character.GetLevel();
                    int baseLevel = Mathf.Max(myLevel, partnerLevel);
                    int finalLevel = baseLevel;
                    bool starUp = false;

                    if (UnityEngine.Random.Range(0, 100) < ConfigManager.LevelUpChance.Value)
                    {
                        finalLevel++;
                        starUp = true;
                        ConfigManager.LogBreeding($"[Procreation] {comboPrefab} offspring triggered LEVEL UP CHANCE! Level increased.");
                    }

                    // Cap to MaxBreedingLevel
                    finalLevel = Mathf.Clamp(finalLevel, 1, ConfigManager.MaxBreedingLevel.Value);
                    childChar.SetLevel(finalLevel);

                    ConfigManager.LogBreeding($"[Procreation] Birthed {comboPrefab} at Level {finalLevel} (Max {ConfigManager.MaxBreedingLevel.Value})");

                    // A birth leaves no trace once the offspring later dies, so it has to be banked now.
                    // starUp reports the roll, not the result: the clamp above can swallow the extra
                    // level at the cap, and "how often did the roll hit" is the interesting figure.
                    DvergrStatsStore.ReportBirth(DvergrStatsSchema.ClassifyPrefabName(comboPrefab),
                        ownerId, ownerName, wasSpecialCombo, starUp, finalLevel);
                }

                if (m_birthEffects != null && m_birthEffects.HasEffects())
                {
                    m_birthEffects.Create(transform.position, Quaternion.identity, null, 1f, -1);
                }
            }

            m_birthingCoroutine = null;
            yield break;
        }

        private string DetermineOffspring(string parentA, string parentB, out bool wasSpecialCombo)
        {
            bool hasFire = parentA.Contains("Fire") || parentB.Contains("Fire");
            bool hasIce = parentA.Contains("Ice") || parentB.Contains("Ice");
            bool hasSupport = parentA.Contains("Support") || parentB.Contains("Support");
            bool hasRogue = parentA.Contains("Rogue") || parentB.Contains("Rogue");
            bool hasWarrior = parentA.Contains("Warrior") || parentB.Contains("Warrior");
            bool hasCleric = parentA.Contains("Cleric") || parentB.Contains("Cleric");

            // True only when a mutation actually came out, NOT merely when the chance roll passed: a
            // successful roll on a pair with no matching combo below still falls through to plain
            // inheritance, and counting that as a mutation would overstate births_special_combo.
            wasSpecialCombo = true;

            if (UnityEngine.Random.Range(0, 100) < ConfigManager.SpecialComboChance.Value)
            {
                // Tier 1 Mutations
                if (hasRogue && !hasFire && !hasIce && !hasSupport && !hasWarrior && !hasCleric) return "AllyDvergrWarrior"; // Rogue + Rogue
                if (hasSupport && !hasRogue && !hasFire && !hasIce && !hasWarrior && !hasCleric) return "AllyDvergrCleric"; // Support + Support
                if (hasFire && hasIce) return "AllyDvergrMageElemental"; // Fire + Ice

                // Tier 2 Mutations
                if (hasRogue && hasFire && !hasIce) return "AllyDvergrSpellswordFire"; // Rogue + Fire
                if (hasRogue && hasIce && !hasFire) return "AllyDvergrSpellswordIce"; // Rogue + Ice
                if (hasWarrior && hasWarrior) return "AllyDvergrBerserker"; // Warrior + Warrior
            }

            wasSpecialCombo = false;
            return UnityEngine.Random.value > 0.5f ? parentA : parentB;
        }
    }
}

