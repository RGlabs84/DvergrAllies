using UnityEngine;

namespace DvergrAllies
{
    public class DvergrHoT : MonoBehaviour
    {
        private Character m_target;
        private int m_ticksRemaining = 5;
        private float m_healAmountPerTick = 10f;
        private float m_tickTimer = 0f;

        public void Setup(int clericLevel)
        {
            m_target = GetComponent<Character>();
            
            // Formula: 10 base + 5 per extra star (level 1 = 10, level 2 = 15, level 3 = 20)
            m_healAmountPerTick = 5f + (5f * clericLevel);
            
            m_ticksRemaining = 5; // Reset duration if already active
            m_tickTimer = 1f;     // Force first tick immediately
            
            ConfigManager.LogAI($"[Cleric] Applied HoT to {m_target?.name} (Lvl {clericLevel}): {m_healAmountPerTick} HP/s for {m_ticksRemaining}s");
        }

        private void FixedUpdate()
        {
            if (m_target == null || m_target.IsDead())
            {
                Destroy(this);
                return;
            }

            m_tickTimer += Time.fixedDeltaTime;
            if (m_tickTimer >= 1f)
            {
                m_tickTimer = 0f;
                m_ticksRemaining--;

                m_target.Heal(m_healAmountPerTick, true);

                if (ZNetScene.instance != null)
                {
                    // Optional VFX on every tick if desired, "vfx_Potion_health_medium"
                    GameObject healVfx = ZNetScene.instance.GetPrefab("vfx_Potion_health_medium");
                    if (healVfx != null) Instantiate(healVfx, m_target.transform.position, Quaternion.identity);
                }

                if (m_ticksRemaining <= 0)
                {
                    Destroy(this);
                }
            }
        }
    }
}
