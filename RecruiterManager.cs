using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using System.Collections.Generic;

namespace DvergrAllies
{
    public static class RecruiterManager
    {
        public static void Setup()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Setup;

            CreateContractItem();
        }

        private static void CreateContractItem()
        {
            GameObject baseItem = PrefabManager.Instance.GetPrefab("Amber");
            if (baseItem == null) return;

            CustomItem contract = new CustomItem("DvergrContract", "Amber");
            ItemDrop drop = contract.ItemDrop;
            
            drop.m_itemData.m_shared.m_name = "Dvergr Contract";
            drop.m_itemData.m_shared.m_description = "A binding contract. Consume to summon a loyal Dvergr Rogue.";
            drop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            
            SummonDvergrStatusEffect effect = ScriptableObject.CreateInstance<SummonDvergrStatusEffect>();
            effect.name = "SE_SummonDvergr";
            effect.m_name = "Summoning";
            effect.m_ttl = 0.1f;

            // Register the custom status effect with Jotunn to ensure ObjectDB has it, 
            // otherwise adding the item to inventory might fail or throw an exception during RPCs!
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect, false));
            
            drop.m_itemData.m_shared.m_consumeStatusEffect = effect;
            drop.m_itemData.m_shared.m_food = 0f;
            drop.m_itemData.m_shared.m_foodStamina = 0f;
            drop.m_itemData.m_shared.m_foodRegen = 0f;
            drop.m_itemData.m_shared.m_value = 0; // Prevent selling it back for 5 coins!
            drop.m_itemData.m_dropPrefab = contract.ItemPrefab; // Explicitly set to prevent null ref inside BuyItem RPC!

            ItemManager.Instance.AddItem(contract);
        }
    }

    public class SummonDvergrStatusEffect : StatusEffect
    {
        private readonly string[] baseTypes = { 
            "AllyDvergrRogue", 
            "AllyDvergrMage", 
            "AllyDvergrMageFire", 
            "AllyDvergrMageIce", 
            "AllyDvergrMageSupport" 
        };

        public override void Setup(Character character)
        {
            base.Setup(character);
            if (character != null && character.IsPlayer())
            {
                // Build complete pool of all possible Dvergrs
                List<string> spawnPool = new List<string>(baseTypes);
                if (AllyPrefabManager.CustomVariants != null)
                {
                    foreach (var variant in AllyPrefabManager.CustomVariants)
                    {
                        spawnPool.Add(variant.PrefabName);
                    }
                }

                // Randomly select a type! (Gacha mechanic)
                string randomType = spawnPool[UnityEngine.Random.Range(0, spawnPool.Count)];
                GameObject dvergr = PrefabManager.Instance.GetPrefab(randomType);
                if (dvergr != null)
                {
                    Vector3 spawnPos = character.transform.position + character.transform.forward * 2f;
                    GameObject clone = UnityEngine.Object.Instantiate(dvergr, spawnPos, character.transform.rotation);

                    Player summoner = character as Player;
                    long ownerId = summoner != null ? summoner.GetPlayerID() : 0L;
                    string ownerName = summoner != null ? summoner.GetPlayerName() : "Unknown";

                    // Stamped before Tame() so the SetTamed postfix credits the buyer instead of
                    // whoever happens to be standing closest, and so this lands in the contract
                    // counters only - a summon is a purchase, not a tame, and double-counting it would
                    // make tamed_alltime drift up every time somebody buys from Haldor.
                    ZNetView cloneNview = clone.GetComponent<ZNetView>();
                    if (cloneNview != null && cloneNview.IsValid())
                    {
                        ZDO cloneZdo = cloneNview.GetZDO();
                        cloneZdo.Set(DvergrStatsExporter.TameCountedKey, 1);
                        DvergrStatsExporter.StampOwner(cloneZdo, ownerId, ownerName);
                    }

                    Tameable tame = clone.GetComponent<Tameable>();
                    if (tame != null) tame.Tame();

                    DvergrStatsStore.ReportContract(
                        DvergrStatsSchema.ClassifyPrefabName(randomType), ownerId, ownerName);

                    string niceName = randomType.Replace("AllyDvergr", "");
                    if (niceName == "Rogue") niceName = "Rogue";
                    
                    character.Message(MessageHud.MessageType.Center, $"Summoned an Ally Dvergr {niceName}!");
                }
            }
        }
    }

    [HarmonyPatch(typeof(Trader))]
    public static class Patches_Trader
    {
        [HarmonyPatch(nameof(Trader.Start))]
        [HarmonyPostfix]
        public static void TraderStartPostfix(Trader __instance)
        {
            if (__instance.gameObject.name.Contains("Haldor"))
            {
                GameObject contract = PrefabManager.Instance.GetPrefab("DvergrContract");
                if (contract != null)
                {
                    // Avoid duplicate adds if Awake is called multiple times somehow
                    foreach (var item in __instance.m_items)
                    {
                        if (item.m_prefab != null && item.m_prefab.gameObject.name == "DvergrContract") return;
                    }

                    Trader.TradeItem tradeItem = new Trader.TradeItem
                    {
                        m_prefab = contract.GetComponent<ItemDrop>(),
                        m_price = ConfigManager.ContractCost.Value,
                        m_stack = 1,
                        m_requiredGlobalKey = "" // Essential! Prevents StoreGui.UpdateRecipe() from throwing a silent null reference exception!
                    };

                    // Bog Witch update added new string fields (like m_requiredPlayerKey)
                    // If they remain null, StoreGui or Trader RPCs crash silently.
                    foreach (var field in typeof(Trader.TradeItem).GetFields())
                    {
                        if (field.FieldType == typeof(string) && field.GetValue(tradeItem) == null)
                        {
                            field.SetValue(tradeItem, "");
                        }
                    }

                    __instance.m_items.Add(tradeItem);
                }
            }
        }
    }
}

