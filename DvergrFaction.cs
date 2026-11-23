using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace DvergrAllies
{
    // A tamed Dvergr fights for the player, so the game has to read it as one of the player's own.
    //
    // What vanilla does: nothing. Tameable / MonsterAI.MakeTame only flip Character.m_tamed (ZDO key "tamed");
    // m_faction is never touched, so a Dvergr we made tameable at load time (MakePrefabTamable) stays
    // Character.Faction.Dverger for life. Our own Ally prefabs were already Players from the moment they spawn
    // (CreateAllyVariant), which is why a contract Dvergr behaved and a fed-and-tamed wild one did not.
    //
    // The faction is a plain field on the instance, not a ZDO value, so setting it on the owner alone would leave
    // every other peer's copy of the same Dvergr on Dverger. Character.IsTamed() IS replicated (non-owners
    // re-read the ZDO about once a second), so each peer derives the faction from it for itself.
    //
    // Faction alone does not stop the player hitting it. Attack, Projectile and Aoe (1.0.21) all let a PLAYER's
    // hit land on anything whose BaseAI.IsAggravatable() is true, enemy or not - that is how a player can strike a
    // neutral Dvergr and set its camp off. Vanilla Dverger are aggravatable, so a tamed one stayed hittable, and
    // every hit made it Frightened, raised the red "!" and queued its "Kill the Norseman!" lines. Ally prefabs never
    // had this because DvergrCombatAI forces m_aggravatable off. So while tamed, this body is not aggravatable
    // either. The hit check runs on the attacker's machine and m_aggravatable is a plain field, so it is derived
    // per peer exactly like the faction. Not aggravatable also keeps BaseAI.AggravateAllInArea (a player hitting
    // a wild Dvergr nearby) from dragging a tame one into the fight.
    public class DvergrFactionSync : MonoBehaviour
    {
        private const float PollSeconds = 1f;

        private Character m_character;
        private BaseAI m_baseAI;
        private ZNetView m_nview;
        private Character.Faction m_untamedFaction;
        private bool m_untamedAggravatable;
        private float m_nextPoll;

        private void Awake()
        {
            // Valkyrie's Cargo's merchant is a plain Dverger and VC marks him tamed; he must stay Dverger.
            // When this returns true every DvergrAllies component on him, this one included, is destroyed.
            if (ValkyriesCargoCompat.ExcludeIfIngvar(gameObject, nameof(DvergrFactionSync))) return;

            m_character = GetComponent<Character>();
            if (m_character == null)
            {
                enabled = false;
                return;
            }

            // What this body is while wild (Dverger on the retrofitted vanilla prefabs, Players on our Ally
            // prefabs, which makes this component a no-op for them). Read here and not from a constant so a
            // modded Dvergr with some other faction gets it back if it is ever untamed.
            m_untamedFaction = m_character.m_faction;

            m_baseAI = GetComponent<BaseAI>();
            m_nview = GetComponent<ZNetView>();
            m_untamedAggravatable = m_baseAI != null && m_baseAI.m_aggravatable;
        }

        // Start, not Awake: Character.IsTamed() dereferences Character's own ZNetView, which is only assigned in
        // Character.Awake, and Unity does not promise that runs before ours.
        private void Start()
        {
            Apply();
            m_nextPoll = Time.time + Random.Range(0f, PollSeconds);
        }

        private void Update()
        {
            if (m_character == null || Time.time < m_nextPoll) return;
            m_nextPoll = Time.time + PollSeconds;
            Apply();
        }

        private void Apply()
        {
            bool ally = ConfigManager.TamedJoinPlayerFaction.Value && m_character.IsTamed();

            Character.Faction want = ally ? Character.Faction.Players : m_untamedFaction;
            if (m_character.m_faction != want)
            {
                ConfigManager.LogAI($"[Faction] {gameObject.name}: {m_character.m_faction} -> {want} " +
                                    $"(tamed: {m_character.IsTamed()}).");
                m_character.m_faction = want;
            }

            if (m_baseAI == null) return;

            bool wantAggravatable = !ally && m_untamedAggravatable;
            if (m_baseAI.m_aggravatable != wantAggravatable)
            {
                ConfigManager.LogAI($"[Faction] {gameObject.name}: aggravatable {m_baseAI.m_aggravatable} -> " +
                                    $"{wantAggravatable} (tamed: {m_character.IsTamed()}).");
                m_baseAI.m_aggravatable = wantAggravatable;
            }

            // Taming does not clear the saved "aggravated" flag (MonsterAI.MakeTame resets alert and target only),
            // so a Dvergr tamed while angry would wake up angry again if it were ever untamed. Owner writes it.
            if (ally && m_nview != null && m_nview.IsValid() && m_nview.IsOwner() &&
                m_nview.GetZDO().GetBool(ZDOVars.s_aggravated))
            {
                m_nview.GetZDO().Set(ZDOVars.s_aggravated, false);
                ConfigManager.LogAI($"[Faction] {gameObject.name}: cleared saved aggravation on taming.");
            }
        }
    }

    // The player's ballista (Turret) never looks at a faction. It asks BaseAI.FindClosestCreature for "everything
    // that is not a Player and, depending on the turret's mode, not tamed", so a Dvergr is a target to it for one
    // of two reasons: it is not tamed, or the turret is in a mode that shoots tamed creatures too. Faction
    // cannot fix either, so this patch does: a Dvergr of ours is passed over by ballistas a player built.
    //
    // Turret is the ONLY caller of FindClosestCreature in 1.0.21, and the postfix is gated on the caller being a
    // Turret, so nothing else's targeting changes. A turret that is not player-built (a Dvergr outpost's) is
    // left alone on purpose: it is hostile to the player's side and should stay that way.
    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindClosestCreature))]
    public static class Patch_BaseAI_FindClosestCreature_SpareAllies
    {
        [HarmonyPostfix]
        public static void Postfix(
            ref Character __result, Transform me, Vector3 eyePoint, float hearRange, float viewRange,
            float viewAngle, bool alerted, bool mistVision, bool passiveAggresive, bool includePlayers,
            bool includeTamed, bool includeEnemies, List<Character> onlyTargets)
        {
            if (__result == null || me == null) return;
            if (!ConfigManager.SpareAlliesFromBallistas.Value) return;
            if (!IsSparedAlly(__result)) return;

            Turret turret = me.GetComponent<Turret>();
            if (turret == null) return;
            Piece piece = turret.GetComponentInParent<Piece>();
            if (piece == null || !piece.IsPlacedByPlayer()) return;

            // The closest target is one of ours, so the turret would shoot at it. Vanilla returns only the single
            // closest, so there is no list to filter: repeat its scan, minus our allies, rather than return null
            // and leave the turret blind to a real enemy standing behind one of them.
            Character best = null;
            float bestDistance = 99999f;
            foreach (Character item in Character.GetAllCharacters())
            {
                if (IsSparedAlly(item)) continue;

                // Everything below is BaseAI.FindClosestCreature (1.0.21) verbatim.
                bool isPlayer = item is Player;
                if ((!includePlayers && isPlayer) || (!includeEnemies && !isPlayer) ||
                    (!includeTamed && item.IsTamed()))
                    continue;

                if (onlyTargets != null && onlyTargets.Count > 0)
                {
                    bool listed = false;
                    foreach (Character wanted in onlyTargets)
                    {
                        if (item.m_name == wanted.m_name) { listed = true; break; }
                    }
                    if (!listed) continue;
                }

                if (item.IsDead()) continue;

                BaseAI ai = item.GetBaseAI();
                if (ai != null && ai.IsSleeping()) continue;

                if (!BaseAI.CanSenseTarget(me, eyePoint, hearRange, viewRange, viewAngle, alerted, mistVision,
                        item, passiveAggresive, isTamed: false))
                    continue;

                float distance = Vector3.Distance(item.transform.position, me.position);
                if (distance < bestDistance || best == null)
                {
                    best = item;
                    bestDistance = distance;
                }
            }

            ConfigManager.LogAI($"[Ballista] Passed over {__result.gameObject.name}; " +
                                $"target is now {(best != null ? best.gameObject.name : "none")}.");
            __result = best;
        }

        // Ours = carries our Tameable subclass (every retrofitted wild Dvergr and every Ally prefab; Valkyrie's
        // Cargo's merchant has had it stripped, so he is NOT spared). Spared when it is tamed, or when it is a
        // Players-faction Ally that was spawned without being tamed (console spawn, another mod's spawner).
        // A wild, untamed Dvergr is still a legitimate target.
        internal static bool IsSparedAlly(Character c)
        {
            if (c == null || c is Player) return false;
            if (c.GetComponent<DvergrTameable>() == null) return false;
            return c.IsTamed() || c.m_faction == Character.Faction.Players;
        }
    }
}
