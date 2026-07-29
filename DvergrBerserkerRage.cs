using UnityEngine;

namespace DvergrAllies
{
    public class DvergrBerserkerRage : MonoBehaviour
    {
        private Character m_character;

        private void Start()
        {
            m_character = GetComponent<Character>();
            InvokeRepeating(nameof(ApplyRage), 1f, 5f);
        }

        private void ApplyRage()
        {
            if (m_character == null || m_character.IsDead()) return;

            SEMan seMan = m_character.GetSEMan();
            if (seMan != null && !seMan.HaveStatusEffect("BerserkerRage".GetStableHashCode()))
            {
                SE_Stats rage = ScriptableObject.CreateInstance<SE_Stats>();
                rage.name = "BerserkerRage";
                rage.m_name = "Rage";
                rage.m_speedModifier = 1.15f; // 15% movement speed
                rage.m_staminaRegenMultiplier = 1.3f; // Faster attacks, but not infinite
                rage.m_healthRegenMultiplier = 1.2f; // Slight regen
                
                // For damage, we can add a skill level modifier
                rage.m_raiseSkill = Skills.SkillType.Axes;
                rage.m_raiseSkillModifier = 15f; // +15 Axe skill for noticeable but balanced damage

                seMan.AddStatusEffect(rage);
            }
        }
    }
}
