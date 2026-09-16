using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DvergrAllies
{
    // NEVER TAME INGVAR.
    //
    // Valkyrie's Cargo (RavenIron-Games/ValkyriesCargo, GUID below) does not ship a merchant prefab. Ingvar
    // is a bare engine Dverger - `Server.BodyPrefab`, "Dverger" by default, any creature prefab by config -
    // whose ZDO carries the int key "VCargo_ingvar" (= the visit id, never 0; Core/Keys.cs over there). The
    // server authors that ZDO straight into ZDOMan (Server/Spawner.cs), every client instantiates it through
    // ZNetScene.CreateObject like any other object, and VC's Patch_Humanoid_Awake sees the key and
    // AddComponents CargoMerchant. So he is instantiated from the very prefab Patch_ZNetScene_Awake_Dvergrs
    // has already injected DvergrTameable / DvergrProcreation / DvergrGenetics into, and nothing about the
    // prefab or the object name ("Dverger(Clone)") tells him apart from a wild Mistlands Dvergr.
    //
    // What that costs without this file, in the order a player notices:
    //  1. Tameable is an Interactable. Ours sits on the prefab; CargoMerchant (also Interactable) is added
    //     at runtime and so sorts LAST, and Player.Interact takes the FIRST match of
    //     GetComponentInParent<Interactable>() (assembly_valheim.decompiled.cs:13722, 1.0.7). He is tamed
    //     (point 2) and MakePrefabTamable sets m_commandable, so [E] on Ingvar is Tameable.Interact's
    //     Command branch (:19170-19180) - follow me / stay - Shift+E opens the rename box, and the trade
    //     never opens.
    //  2. VC tames him itself: CargoMerchant.Awake -> ReassertOwned calls Character.SetTamed(true) (and
    //     MonsterAI.MakeTame from its 0.5 s stagger). That FIRST SetTamed runs inside VC's Humanoid.Awake
    //     postfix, i.e. BEFORE any of our components has reached Awake and while DvergrGenetics is still on
    //     him - so the two Character.SetTamed postfixes cannot rely on "our component is gone" and carry an
    //     explicit IsIngvar gate each; without it the pilot's client stamps SoMStealthExempt=1, an owner tag
    //     and a tame count onto a ZDO VC owns and reports a tame to BarrkBOT. Once tamed, DvergrProcreation
    //     would breed him with the player's Dvergr, and DvergrGenetics.Awake would write dvergr_gender.
    //  3. The census in DvergrStatsExporter walks ZDOs by prefab hash and would count him as a tamed Dvergr.
    //  4. m_consumeItems on the shared prefab sends him wandering after dropped coins.
    //
    // The strip removes EVERY component this assembly put on him, not a named three: `Server.BodyPrefab` is
    // any registered prefab hash, so an admin can point it at one of our Ally prefabs, and then it is
    // DvergrCombatAI / DvergrWeaponScaler that would be re-arming him every few seconds against
    // CargoMerchant's UnequipAllItems.
    //
    // Detection is by the ZDO key - the same test VC itself uses - and is deliberately NOT gated on the
    // plugin being loaded: the merchant ZDO is persistent, and one stranded in a world after VC is removed
    // is still not ours to tame. The prefab-level injection is left exactly as it is, because every other
    // Dverger from that prefab is a wild Dvergr that must stay tameable; the INSTANCE is stripped as it
    // wakes, from the first of our three components to reach Awake (Unity runs them in serialized order,
    // which is the order MakePrefabTamable added them, but that is an observation and not a guarantee, so
    // each of the three guards itself and the strip is idempotent).
    public static class ValkyriesCargoCompat
    {
        public const string PluginGuid = "com.raveniron.valkyriescargo";
        public const string IngvarKey = "VCargo_ingvar";
        public static readonly int IngvarHash = IngvarKey.GetStableHashCode();

        // Informational only (one log line at startup); see the header for why detection does not use it.
        public static bool HasValkyriesCargo;

        // Tameable.Awake registers these on the creature's ZNetView (assembly_valheim.decompiled.cs:19067-19069,
        // 1.0.7; identical on the dedicated-server build). A handler left behind on a destroyed component still
        // runs when the RPC arrives. Nothing on a same-version peer sends them (the only senders are a live
        // Tameable's Interact/UseItem, and every peer has stripped his), and NetworkCompatibility is
        // VersionStrictness.Patch precisely so a pre-1.0.8 client with a live Tameable on him cannot join - but
        // a stale handler that would make Ingvar follow someone is not something to leave lying around on the
        // strength of a version gate. Unregistering makes such an RPC drop ("Failed to find rpc method").
        // Unregister of a name that was never registered is a plain Dictionary.Remove.
        private static readonly string[] TameableRpcs = { "Command", "SetName", "RPC_UnSummon" };

        public static bool IsIngvar(ZDO zdo)
        {
            return zdo != null && zdo.GetInt(IngvarHash, 0) != 0;
        }

        // Awake-order fallback, the same one VC's Patch_Humanoid_Awake and Patch_Valkyrie_Awake use and with
        // the same proof: ZNetScene.CreateObject sets the static ZNetView.m_initZDO immediately before its
        // synchronous Instantiate, ZNetView.Awake consumes-and-nulls it as its first act, and CreateObject nulls
        // it itself if nothing claimed it (decompiled.cs:82240-82248). Awake runs for every component of the
        // new object before Instantiate returns, on one thread, so a non-null m_initZDO seen from a component's
        // Awake is always THAT object's ZDO. Outside that window it is null and this simply answers false.
        public static bool IsIngvar(ZNetView nview)
        {
            if (nview == null) return false;
            ZDO zdo = nview.GetZDO();
            if (zdo == null) zdo = ZNetView.m_initZDO;
            return IsIngvar(zdo);
        }

        public static bool IsIngvar(GameObject go)
        {
            return go != null && IsIngvar(go.GetComponent<ZNetView>());
        }

        // Cheap per-frame test for the hot paths (Patch_MonsterAI_UpdateAI_Dvergrs): one GetComponent, no ZDO read.
        public static bool IsExcluded(GameObject go)
        {
            return go != null && go.GetComponent<DvergrAlliesExcluded>() != null;
        }

        // Called from the Awake of each of our three per-creature components. Returns true when the object is
        // Ingvar, in which case every DvergrAllies component on it - the caller included - has been destroyed
        // and the caller must return without touching its own fields.
        public static bool ExcludeIfIngvar(GameObject go, string caller)
        {
            if (go == null) return false;
            if (IsExcluded(go)) return true;          // an earlier component already did the work
            if (!IsIngvar(go)) return false;

            Exclude(go, caller);
            return true;
        }

        private static void Exclude(GameObject go, string caller)
        {
            ZNetView nview = go.GetComponent<ZNetView>();
            Character character = go.GetComponent<Character>();
            MonsterAI monsterAI = go.GetComponent<MonsterAI>();

            // Marker first, so a second component reaching Awake while this runs sees the job as taken.
            DvergrAlliesExcluded marker = go.AddComponent<DvergrAlliesExcluded>();
            marker.Reason = "Valkyrie's Cargo merchant (ZDO " + IngvarKey + ")";

            DvergrTameable tameable = go.GetComponent<DvergrTameable>();
            if (tameable != null)
            {
                // Undo everything Tameable.Awake wires up (decompiled.cs:19049-19086), in case it has already
                // run - it has, when we arrive from its own postfix. Each step is a no-op if it has not.
                tameable.CancelInvoke();                                                  // TamingUpdate
                if (character != null)
                    character.m_onDeath = (Action)RemoveTarget(character.m_onDeath, tameable);
                if (monsterAI != null)
                    monsterAI.m_onConsumedItem = (Action<ItemDrop>)RemoveTarget(monsterAI.m_onConsumedItem, tameable);
                if (nview != null && nview.IsValid())
                    foreach (string rpc in TameableRpcs) nview.Unregister(rpc);

                // Immediate, not deferred: a deferred Destroy leaves the component findable by
                // GetComponent<Tameable>() / GetComponentInParent<Interactable>() until the end of the frame,
                // and the point of this file is that it is never found. BaseAI.m_tamable (cached in
                // BaseAI.Awake, which ran before us) reads as absent from here on - every use of it in
                // BaseAI/MonsterAI goes through Unity's (bool)/! operators (decompiled.cs:26027-28160).
                UnityEngine.Object.DestroyImmediate(tameable);
            }

            // Everything else of ours, by assembly rather than by name: DvergrProcreation and DvergrGenetics on
            // a wild body, and DvergrCombatAI / DvergrWeaponScaler / DvergrBerserkerRage / DvergrHoT too when
            // Server.BodyPrefab names one of our Ally prefabs (it is any registered prefab hash; Spawner.cs:116)
            // - those would otherwise re-equip him with staves and fight CargoMerchant's UnequipAllItems every
            // few seconds. None of them subscribes to a delegate or registers an RPC (Invoke/InvokeRepeating and
            // FixedUpdate only), so destroying them is the whole undo. A component added to this mod later is
            // covered without anyone remembering this file.
            Assembly ours = typeof(ValkyriesCargoCompat).Assembly;
            List<string> stripped = new List<string>();
            if (tameable != null) stripped.Add(nameof(DvergrTameable));
            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb is DvergrAlliesExcluded) continue;      // null: a missing-script slot
                if (mb.GetType().Assembly != ours) continue;
                stripped.Add(mb.GetType().Name);
                mb.CancelInvoke();
                UnityEngine.Object.DestroyImmediate(mb);
            }

            // The instance's own list (Instantiate copies serialized fields), but replaced rather than
            // cleared all the same - never mutate a list that might be the prefab's. VC's own Reassert does
            // this too, at its 0.5 s stagger; doing it here closes the gap before that.
            if (monsterAI != null && monsterAI.m_consumeItems != null && monsterAI.m_consumeItems.Count > 0)
                monsterAI.m_consumeItems = new List<ItemDrop>();

            int visitId = 0;
            ZDO zdo = nview != null ? nview.GetZDO() : null;
            if (zdo == null) zdo = ZNetView.m_initZDO;
            if (zdo != null) visitId = zdo.GetInt(IngvarHash, 0);

            Jotunn.Logger.LogInfo(
                $"[Compat] Valkyrie's Cargo merchant (visit #{visitId}, {go.name}) woke on this machine; " +
                $"stripped {string.Join(", ", stripped.ToArray())} (seen first by {caller}). " +
                "He cannot be tamed, bred, petted, renamed, commanded or counted.");
        }

        // Drops every subscription whose target is `target` from a multicast delegate. Tameable.OnDeath and
        // Tameable.OnConsumedItem are private, so this matches on the receiver rather than on the method.
        private static Delegate RemoveTarget(Delegate del, object target)
        {
            if (del == null) return null;
            foreach (Delegate d in del.GetInvocationList())
            {
                if (ReferenceEquals(d.Target, target)) del = Delegate.Remove(del, d);
            }
            return del;
        }
    }

    // Left on a creature DvergrAllies has stepped away from. Carries no behaviour; it exists so the per-frame
    // patches can ask "is this one ours?" with one GetComponent, and so `cargo prefab` / component dumps show
    // why the creature has no DvergrTameable.
    public sealed class DvergrAlliesExcluded : MonoBehaviour
    {
        public string Reason;
    }

    // DvergrTameable cannot see its own base Awake: Tameable.Awake is private and non-virtual, and a `new`
    // Awake on the subclass would REPLACE it for Unity's message dispatch rather than wrap it. A postfix runs
    // after the base has fully wired itself up, so the strip in ExcludeIfIngvar can undo exactly that wiring.
    // Filtered to our own subclass: a Tameable some other mod put on a creature is not ours to remove.
    [HarmonyPatch(typeof(Tameable), "Awake")]
    public static class Patch_Tameable_Awake_IgnoreIngvar
    {
        [HarmonyPostfix]
        public static void Postfix(Tameable __instance)
        {
            if (!(__instance is DvergrTameable)) return;
            ValkyriesCargoCompat.ExcludeIfIngvar(__instance.gameObject, nameof(DvergrTameable));
        }
    }
}
