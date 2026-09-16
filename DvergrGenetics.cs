using HarmonyLib;
using UnityEngine;

namespace DvergrAllies
{
    public class DvergrGenetics : MonoBehaviour
    {
        // Shadows of Midgard's opt-out flag. SoM reads this off the ZDO (ShadowsOfMidgard.StealthExemption)
        // and falls back to vanilla Valheim AI when it is 1, skipping its stealth brain entirely.
        //
        // ONLY OUR ALLIES MAY CARRY THIS. DvergrGenetics is injected into every wild Dvergr prefab too
        // (so they can be tamed and bred), so an unconditional stamp here would exempt every hostile
        // Mistlands Dvergr in the world and break sneaking past them for SoM users.
        //
        // The flag is written explicitly to 0 rather than merely skipped, because a ZDO write persists and
        // replicates: builds before this fix stamped 1 on wild Dvergr, and those saves need clearing.
        public const string StealthExemptKey = "SoMStealthExempt";

        // Tamed covers wild Dvergr we recruited and their offspring (see DvergrProcreation); the Players
        // faction covers our custom ally prefabs, which are allies from the moment they spawn.
        public static void RefreshStealthExemption(Character c, ZNetView nview)
        {
            if (c == null) return;
            RefreshStealthExemption(c, nview, c.IsTamed());
        }

        // Takes the tamed state explicitly because Character.SetTamed does NOT update m_tamed itself - it
        // fires RPC_SetTamed, and only the handler assigns the field. IsTamed() is therefore not reliable
        // for a caller sitting right after SetTamed unless that caller happens to be the ZDO owner (owner
        // targeted RPCs dispatch synchronously). Callers that already know the answer should pass it and
        // not depend on that.
        public static void RefreshStealthExemption(Character c, ZNetView nview, bool tamed)
        {
            if (c == null || nview == null || !nview.IsValid()) return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null) return;

            bool isAlly = tamed || c.m_faction == Character.Faction.Players;
            zdo.Set(StealthExemptKey, isAlly ? 1 : 0);
        }

        private ZNetView m_nview;
        private Humanoid m_humanoid;

        public enum Gender { Male, Female }

        private void Awake()
        {
            // Before the owner-side writes below: Valkyrie's Cargo's merchant rides the shared Dverger prefab,
            // and his ZDO is VC's to write, not ours - no gender, no stealth flag. ExcludeIfIngvar has
            // destroyed this component when it returns true. See ValkyriesCargoCompat.
            if (ValkyriesCargoCompat.ExcludeIfIngvar(gameObject, nameof(DvergrGenetics))) return;

            m_nview = GetComponent<ZNetView>();
            m_humanoid = GetComponent<Humanoid>();

            if (m_nview == null || !m_nview.IsValid()) return;

            // Only the owner should initialize the gender initially
            if (m_nview.IsOwner())
            {
                RefreshStealthExemption(GetComponent<Character>(), m_nview);

                int currentGender = m_nview.GetZDO().GetInt("dvergr_gender", -1);
                if (currentGender == -1)
                {
                    // Randomly assign gender
                    Gender newGender = Random.value > 0.5f ? Gender.Male : Gender.Female;
                    m_nview.GetZDO().Set("dvergr_gender", (int)newGender);
                    ConfigManager.LogBreeding($"[Genetics] {gameObject.name} randomly assigned gender: {newGender}");
                }
            }
        }

        private void LateUpdate()
        {
            if (m_nview == null || !m_nview.IsValid()) return;
            if (Player.m_localPlayer != null && Player.m_localPlayer.GetHoverObject() != null)
            {
                GameObject hovered = Player.m_localPlayer.GetHoverObject();
                if (hovered == this.gameObject || hovered.transform.IsChildOf(this.transform))
                {
                    if (Hud.instance != null && Hud.instance.m_hoverName != null)
                    {
                        int genderInt = m_nview.GetZDO().GetInt("dvergr_gender", -1);
                        if (genderInt != -1)
                        {
                            Gender gender = (Gender)genderInt;
                            string extraText = $"\n({gender})";

                            DvergrProcreation procreation = GetComponent<DvergrProcreation>();
                            if (procreation != null && procreation.IsPregnant())
                            {
                                int timeLeft = procreation.GetPregnancyTimeLeft();
                                string timeString = timeLeft > 0 ? $"{timeLeft}s" : "Imminent";
                                extraText += $"\n<color=yellow>Pregnant: {timeString}</color>";
                            }

                            DvergrTameable tameable = GetComponent<DvergrTameable>();
                            if (tameable != null && tameable.IsHungry())
                            {
                                extraText += "\n<color=red>Hungry</color>";
                            }

                            Hud.instance.m_hoverName.text += extraText;
                        }
                    }
                }
            }
        }

        public Gender GetGender()
        {
            if (m_nview == null || !m_nview.IsValid()) return Gender.Male;
            return (Gender)m_nview.GetZDO().GetInt("dvergr_gender", 0);
        }
    }

    // Awake alone cannot maintain the exemption: a wild Dvergr tamed mid-session already ran it while still
    // hostile. DvergrTameable is a bare Tameable subclass with no tame hook, so we ride vanilla's own
    // tame transition instead of polling per-frame.
    [HarmonyPatch(typeof(Character), nameof(Character.SetTamed))]
    public static class Patch_Character_SetTamed_StealthExemption
    {
        // Reads the tamed argument rather than calling IsTamed() again - see RefreshStealthExemption. The
        // owner gate below happens to make IsTamed() correct here, but relying on that would mean a future
        // edit to the gate could silently start clearing the exemption on the very tick a Dvergr is tamed.
        [HarmonyPostfix]
        public static void Postfix(Character __instance, bool tamed)
        {
            if (__instance == null || __instance.GetComponent<DvergrGenetics>() == null) return;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
            // LOAD-BEARING, not belt-and-braces: Valkyrie's Cargo's CargoMerchant.Awake calls SetTamed(true)
            // from inside VC's Humanoid.Awake postfix, BEFORE our components have reached Awake, so on the
            // pilot's (owning) client this postfix runs while DvergrGenetics is still on Ingvar and the check
            // above passes. Without this line he gets SoMStealthExempt=1 written onto a ZDO VC owns.
            if (ValkyriesCargoCompat.IsIngvar(nview)) return;

            DvergrGenetics.RefreshStealthExemption(__instance, nview, tamed);
        }
    }
}

