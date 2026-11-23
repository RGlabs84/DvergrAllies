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
            if (baseItem == null)
            {
                Jotunn.Logger.LogError("[Contract] Amber prefab not found, so the Dvergr Contract was never " +
                                       "created and cannot appear in Haldor's stock this session.");
                return;
            }

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

            if (ItemManager.Instance.AddItem(contract))
                Jotunn.Logger.LogInfo(
                    $"[Contract] Dvergr Contract registered at {ConfigManager.ContractCost.Value} coins.");
            else
                Jotunn.Logger.LogError("[Contract] Jotunn refused the Dvergr Contract item, so it will not " +
                                       "appear in Haldor's stock this session.");
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
        // Every exit from here says which one it took. Trader.Start is a popular target - Yggdrasil's
        // Reckoning sells its Tear here, Valheim Runestone sells its trader cards here - and HarmonyX runs
        // all of a method's postfixes as one chain, so an exception thrown in somebody else's postfix skips
        // every later one, ours included, without a word in LogOutput.log. That left "the contract is not in
        // Haldor's stock" with four indistinguishable causes: not Haldor, no prefab, already added, or this
        // method never ran. It is now readable off the log, and the absence of ANY [Contract] line for a
        // trader who woke is itself the answer: something ahead of us in the chain threw.
        [HarmonyPatch(nameof(Trader.Start))]
        [HarmonyPostfix]
        public static void TraderStartPostfix(Trader __instance)
        {
            string traderName = __instance.gameObject.name;

            if (!traderName.Contains("Haldor"))
            {
                Jotunn.Logger.LogDebug($"[Contract] Trader '{traderName}' is not Haldor; stock unchanged.");
                return;
            }

            GameObject contract = PrefabManager.Instance.GetPrefab("DvergrContract");
            if (contract == null)
            {
                Jotunn.Logger.LogError($"[Contract] {traderName} woke but the DvergrContract prefab is missing, " +
                                       "so his stock is unchanged. The registration line at startup says why.");
                return;
            }

            // Avoid duplicate adds if Awake is called multiple times somehow
            foreach (var item in __instance.m_items)
            {
                if (item.m_prefab != null && item.m_prefab.gameObject.name == "DvergrContract")
                {
                    Jotunn.Logger.LogInfo($"[Contract] Already in {traderName}'s stock; not added twice.");
                    return;
                }
            }

            Trader.TradeItem tradeItem = new Trader.TradeItem
            {
                m_prefab = contract.GetComponent<ItemDrop>(),
                m_price = ConfigManager.ContractCost.Value,
                m_stack = 1,
                m_requiredGlobalKey = "", // Essential! Prevents StoreGui.UpdateRecipe() from throwing a silent null reference exception!

                // Valheim 1.0 added this one, and it is NOT a string, so the backfill loop below cannot reach
                // it. StoreGui.BuySelectedItem calls m_buyPlayerEffects.Create() unconditionally once a
                // purchase has gone through, so leaving it null throws AFTER the coins are taken and the
                // contract handed over - the buy effects never play and the store list never refreshes,
                // because FillList() is the statement after it. A fresh EffectList makes Create() a no-op:
                // its m_effectPrefabs already defaults to an empty array.
                m_buyPlayerEffects = new EffectList()
            };

            // Bog Witch update added new string fields (like m_requiredPlayerKey)
            // If they remain null, StoreGui or Trader RPCs crash silently. StoreGui.FillList reads
            // m_tooltip.Length on every row it draws, so a null one of those takes down the whole list.
            foreach (var field in typeof(Trader.TradeItem).GetFields())
            {
                if (field.FieldType == typeof(string) && field.GetValue(tradeItem) == null)
                {
                    field.SetValue(tradeItem, "");
                }
            }

            int before = __instance.m_items.Count;
            __instance.m_items.Add(tradeItem);
            Jotunn.Logger.LogInfo($"[Contract] Added to {traderName}'s stock at {tradeItem.m_price} coins " +
                                  $"({before} entries before, {__instance.m_items.Count} after).");
        }
    }
}

