using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace DvergrAllies
{
    // 1.0.14: THE LEASH THAT HOLDS, AND THE FEAR THAT LETS GO. One prefix on MonsterAI.UpdateAI for every Dvergr that
    // carries our Tameable (Ally prefabs AND wild Dvergr the player tamed), on the AI owner's machine.
    //
    // THE LEASH NEVER WORKED BEFORE THIS. DvergrCombatAI's leash called m_monsterAI.SetTarget(null), and vanilla's
    // SetTarget (1.0.31 MonsterAI.cs:192-194) only ever ASSIGNS a target: `if (attacker != null && m_targetCreature == null
    // ...)`. A null was a silent no-op, so the Dvergr kept chasing while the log said "dropping aggro" every frame. It
    // also lived only on Ally prefabs, so a tamed wild Dvergr had no leash at all. Here the target is cleared for real
    // (m_targetCreature / m_targetStatic, alert off), and re-acquiring is held off (m_updateTargetTimer) until the
    // Dvergr is back well inside the leash - otherwise MonsterAI.UpdateTarget's FindEnemy re-picks the same enemy within
    // two seconds and the chase resumes.
    //
    // The leash is measured from the player the Dvergr FOLLOWS, or - told to stay - from its POST (vanilla's "stay" is
    // Tameable.RPC_Command -> SetFollowTarget(null) + SetPatrolPoint(), BaseAI.cs:271). Before 1.0.14 a posted Dvergr
    // had no leash and chased anything vanilla let it (tamed + patrol point: targets within m_alertRange of the post,
    // MonsterAI.cs:280-286) as far as that target ran.
    //
    // THE FEAR. Vanilla's m_fleeIfHurtWhenTargetCantBeReached (on for the Dverger prefab; MonsterAI.cs:436) makes a
    // creature flee whenever it has a target, has not attacked for 30 s and was hurt in the last 20 s. Fleeing means it
    // never attacks, so m_timeSinceAttacking keeps climbing and it keeps fleeing until it has gone 20 s unhurt: a
    // caster with a 15-30 s attack interval or a Dvergr that cannot path to its target spends whole fights running.
    // The mod switched off every OTHER flee flag (low health, not alerted, fire) but never this one. A tamed Dvergr now
    // has it off unless the server turns "Flee When Hurt And Stuck" back on; an untamed one keeps vanilla's.
    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
    public static class DvergrAllyLeash
    {
        // Back inside this fraction of the leash before it may pick a fight again (hysteresis, so a Dvergr on the
        // line does not flip between chasing and returning every frame).
        private const float ReengageFraction = 0.6f;
        // How long target-finding is held off each frame the Dvergr is still returning.
        private const float HoldOffSeconds = 1f;

        private static readonly ConditionalWeakTable<MonsterAI, object> Ours = new ConditionalWeakTable<MonsterAI, object>();
        private static readonly ConditionalWeakTable<MonsterAI, object> NotOurs = new ConditionalWeakTable<MonsterAI, object>();
        private static readonly object Mark = new object();
        private static readonly HashSet<int> Returning = new HashSet<int>();

        private static bool IsOurs(MonsterAI ai)
        {
            if (Ours.TryGetValue(ai, out _)) return true;
            if (NotOurs.TryGetValue(ai, out _)) return false;
            bool ours = ai.GetComponent<DvergrTameable>() != null;
            (ours ? Ours : NotOurs).Add(ai, Mark);
            return ours;
        }

        [HarmonyPrefix]
        public static void Prefix(MonsterAI __instance)
        {
            if (__instance == null) return;
            Character c = __instance.m_character;
            if (c == null || !IsOurs(__instance)) return;

            bool tamed = c.IsTamed();
            __instance.m_fleeIfHurtWhenTargetCantBeReached = !tamed || ConfigManager.FleeWhenHurtAndStuck.Value;
            if (!tamed) return;

            Leash(__instance, c);
        }

        private static void Leash(MonsterAI ai, Character c)
        {
            int id = ai.GetInstanceID();
            float limit = ConfigManager.MaxFollowLeash.Value;
            Vector3 center;
            GameObject follow = ai.GetFollowTarget();
            if (limit <= 0f || !TryCenter(ai, follow, out center))
            {
                Returning.Remove(id);
                return;
            }

            float dist = Utils.DistanceXZ(c.transform.position, center);
            bool returning = Returning.Contains(id);
            if (!returning && dist > limit)
            {
                Returning.Add(id);
                returning = true;
                ConfigManager.LogAI($"[Leash] {ai.gameObject.name} is {dist:F1} m from its {(follow != null ? "leader" : "post")} " +
                                    $"(leash {limit:F0} m): dropping its target and going back.");
            }
            else if (returning && dist < limit * ReengageFraction)
            {
                Returning.Remove(id);
                ConfigManager.LogAI($"[Leash] {ai.gameObject.name} is back ({dist:F1} m); free to fight again.");
                return;
            }

            if (!returning) return;
            if (ai.m_targetCreature != null || ai.m_targetStatic != null || ai.IsAlerted())
            {
                ai.m_targetCreature = null;
                ai.m_targetStatic = null;
                ai.SetAlerted(false);
                ai.m_timeSinceAttacking = 0f;
            }
            if (ai.m_updateTargetTimer < HoldOffSeconds) ai.m_updateTargetTimer = HoldOffSeconds;
        }

        private static bool TryCenter(MonsterAI ai, GameObject follow, out Vector3 center)
        {
            if (follow != null)
            {
                center = follow.transform.position;
                return true;
            }
            return ai.GetPatrolPoint(out center);
        }
    }
}
