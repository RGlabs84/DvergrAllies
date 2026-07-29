using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;

namespace DvergrAllies
{
    /// <summary>
    /// Handles tier scaling for starred Dvergr allies.
    /// Base weapons are set at prefab time via m_defaultItems in AllyPrefabManager.
    /// This component only runs for starred (level 2+) units to upgrade Bronze -> Iron -> Blackmetal.
    /// </summary>
    public class DvergrWeaponScaler : MonoBehaviour
    {
        private Humanoid m_humanoid;
        private Character m_character;
        private ZNetView m_nview;
        private bool m_scaled = false;

        private void Start()
        {
            m_humanoid = GetComponent<Humanoid>();
            m_character = GetComponent<Character>();
            m_nview = GetComponent<ZNetView>();

            if (m_nview != null && m_nview.IsValid() && m_nview.IsOwner())
            {
                // Delay to ensure GiveDefaultItems has run and populated inventory
                Invoke(nameof(DoScaleWeapons), 1.0f);
            }
        }

        private void DoScaleWeapons()
        {
            if (m_scaled) return;
            if (m_humanoid == null || m_character == null) return;
            m_scaled = true;

            int level = m_character.GetLevel();
            if (level <= 1) return; // No stars = base tier, nothing to scale

            string prefabName = Utils.GetPrefabName(gameObject);
            ConfigManager.LogEquipment($"[WeaponScaler] Scaling {prefabName} (level {level})");

            // Build replacement map: old prefab name -> new prefab name
            Dictionary<string, string> replacements = new Dictionary<string, string>();

            if (level == 2) // 1-star: Iron tier
            {
                replacements["AllyDvergr_SwordBronze"] = "AllyDvergr_SwordIron";
                replacements["AllyDvergr_MaceBronze"] = "AllyDvergr_MaceIron";
                replacements["ShieldBanded"] = "ShieldSilver";
                replacements["AllyDvergr_Battleaxe"] = "AllyDvergr_BattleaxeBlackmetal"; // Battleaxe skips to Blackmetal (no iron variant)
            }
            else // 2-star+: Blackmetal tier
            {
                replacements["AllyDvergr_SwordBronze"] = "AllyDvergr_SwordBlackmetal";
                replacements["AllyDvergr_MaceBronze"] = "AllyDvergr_MaceBlackmetal";
                replacements["ShieldBanded"] = "ShieldBlackmetal";
                replacements["AllyDvergr_Battleaxe"] = "AllyDvergr_BattleaxeBlackmetal";
            }

            // Unequip hands before modifying inventory
            ItemDrop.ItemData rightHand = m_humanoid.GetRightItem();
            if (rightHand != null) m_humanoid.UnequipItem(rightHand);
            ItemDrop.ItemData leftHand = m_humanoid.GetLeftItem();
            if (leftHand != null) m_humanoid.UnequipItem(leftHand);

            // Scan inventory and replace scalable items
            var currentItems = m_humanoid.GetInventory().GetAllItems();
            List<int> toRemove = new List<int>();
            List<string> toAdd = new List<string>();

            for (int i = 0; i < currentItems.Count; i++)
            {
                var item = currentItems[i];
                string itemPrefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
                if (string.IsNullOrEmpty(itemPrefab)) continue;

                if (replacements.ContainsKey(itemPrefab))
                {
                    toRemove.Add(i);
                    toAdd.Add(replacements[itemPrefab]);
                    ConfigManager.LogEquipment($"[WeaponScaler] Upgrading {itemPrefab} -> {replacements[itemPrefab]}");
                }
            }

            // Remove old items (backward to preserve indices)
            for (int i = toRemove.Count - 1; i >= 0; i--)
            {
                m_humanoid.GetInventory().RemoveItem(toRemove[i]);
            }

            // Keep track of the new GameObject prefabs to update m_defaultItems if necessary
            List<GameObject> newPrefabs = new List<GameObject>();

            // Add replacement items using the vanilla method
            foreach (string newName in toAdd)
            {
                // If it's an AllyDvergr_ prefixed item, we need to ensure it's cloned with the melee attack!
                GameObject newPrefab = AllyPrefabManager.GetOrCloneWeaponWithMeleeAnim(newName);
                
                if (newPrefab != null)
                {
                    newPrefabs.Add(newPrefab);
                    bool added = m_humanoid.GetInventory().AddItem(newPrefab, 1);
                    ConfigManager.LogEquipment($"[WeaponScaler] Added {newName} to inventory: {added}");

                    // Crucial Step: Force the AI to actually equip the new weapon so they aren't stuck holding an invisible crossbow!
                    if (added)
                    {
                        foreach (ItemDrop.ItemData invItem in m_humanoid.GetInventory().GetAllItems())
                        {
                            if (invItem.m_dropPrefab != null && invItem.m_dropPrefab.name == newName)
                            {
                                m_humanoid.EquipItem(invItem);
                                ConfigManager.LogEquipment($"[WeaponScaler] Equipped {newName} successfully!");
                                break;
                            }
                        }
                    }
                }
                else
                {
                    Jotunn.Logger.LogWarning($"[WeaponScaler] Could not find replacement prefab: {newName}");
                }
            }

            // Optionally, update m_defaultItems array so the AI remembers to re-equip it if they unequip
            if (newPrefabs.Count > 0)
            {
                List<GameObject> defaults = new List<GameObject>(m_humanoid.m_defaultItems);
                foreach (var go in newPrefabs)
                {
                    if (!defaults.Contains(go)) defaults.Add(go);
                }
                m_humanoid.m_defaultItems = defaults.ToArray();
            }
        }
    }
}
