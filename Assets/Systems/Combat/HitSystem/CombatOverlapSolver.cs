using System.Collections.Generic;
using Systems.Combat.Combatant.Behaviour;
using Unity.Mathematics.Geometry;

namespace Systems.Combat.HitSystem
{
    /// <summary>
    /// Resolves hitbox–hurtbox overlaps for one logic tick. Combatants register their world-space
    /// volumes each tick; <see cref="Solve"/> performs an N×M AABB intersection test, deduplicates
    /// using a per-move hit registry, and returns a list of confirmed collision tuples for
    /// <see cref="CombatManager"/> to dispatch as hit events.
    /// </summary>
    public class CombatOverlapSolver
    {
        /// <summary>Hurtbox volumes registered this tick, keyed by the owning combatant.</summary>
        private Dictionary<CombatantBehaviour, MinMaxAABB[]> _hurtboxes = new();

        /// <summary>Hitbox volumes and their hit data registered this tick, keyed by the attacking combatant.</summary>
        private Dictionary<CombatantBehaviour, (HitData, MinMaxAABB[])> _hitboxes = new();

        /// <summary>
        /// Per-move hit registry that prevents the same (attacker, hitId, defender) tuple from
        /// being processed more than once. Cleared when the attacker starts a new move.
        /// </summary>
        private readonly HashSet<(CombatantBehaviour perpetrator, uint hitId, CombatantBehaviour victim)>
            _hitRegistry = new();

        /// <summary>Stores the hurtbox volumes for <paramref name="combatantBehaviour"/> this tick, replacing any previously registered volumes.</summary>
        public void RegisterHurtboxes(CombatantBehaviour combatantBehaviour, MinMaxAABB[] hurtboxes)
        {
            _hurtboxes[combatantBehaviour] = hurtboxes;
        }

        /// <summary>Stores the hitbox volumes and hit data for <paramref name="combatantBehaviour"/> this tick.</summary>
        public void RegisterHitboxes(CombatantBehaviour combatantBehaviour, HitData hitData, MinMaxAABB[] hitboxes)
        {
            _hitboxes[combatantBehaviour] = (hitData, hitboxes);
        }

        /// <summary>Clears all per-tick hitbox and hurtbox registrations. Call at the start of each logic tick.</summary>
        public void ClearFramedata()
        {
            _hurtboxes.Clear();
            _hitboxes.Clear();
        }

        /// <summary>Removes all hit-registry entries for <paramref name="perpetrator"/>. Called when they start a new move.</summary>
        public void ClearHitRegistry(CombatantBehaviour perpetrator) =>
            _hitRegistry.RemoveWhere(e => e.perpetrator == perpetrator);

        /// <summary>
        /// Tests all registered hitboxes against all registered hurtboxes. Returns a list of
        /// <c>(defender, hitData, attacker)</c> tuples for every confirmed, non-deduplicated overlap.
        /// </summary>
        public List<(CombatantBehaviour, HitData, CombatantBehaviour)> Solve()
        {
            var result = new List<(CombatantBehaviour, HitData, CombatantBehaviour)>();

            foreach (var hitboxEntry in _hitboxes)
            {
                var attacker = hitboxEntry.Key;
                var (hitData, hitboxes) = hitboxEntry.Value;

                foreach (var hurtboxEntry in _hurtboxes)
                {
                    var defender = hurtboxEntry.Key;
                    var hurtboxes = hurtboxEntry.Value;

                    if (!TargetMatches(hitData.HitTarget, attacker, defender)) continue;

                    // ── Deduplication ────────────────────────────────────────────────
                    // HitId 0 means the data was set without going through the DSL
                    // (legacy / accidental). Skip deduplication so nothing silently breaks.
                    var registryKey = (attacker, hitData.HitId, defender);
                    if (hitData.HitId != 0 && _hitRegistry.Contains(registryKey)) continue;

                    // ── Overlap check ────────────────────────────────────────────────
                    bool overlapped = false;
                    foreach (var hitbox in hitboxes)
                    {
                        if (overlapped) break;
                        foreach (var hurtbox in hurtboxes)
                        {
                            if (!hitbox.Overlaps(hurtbox)) continue;
                            overlapped = true;
                            break;
                        }
                    }

                    if (!overlapped) continue;

                    // Register before adding to results so simultaneous Solve() calls
                    // (if ever parallelised) cannot double-fire the same key.
                    if (hitData.HitId != 0)
                        _hitRegistry.Add(registryKey);

                    result.Add((defender, hitData, attacker));
                }
            }

            return result;
        }


        /// <summary>Returns true when the hit's <paramref name="target"/> restriction is satisfied for the given attacker–defender pair.</summary>
        private static bool TargetMatches(EHitTarget target, CombatantBehaviour attacker, CombatantBehaviour defender)
            => target switch
            {
                EHitTarget.Enemy => AreEnemies(attacker, defender),
                EHitTarget.Ally => AreAllies(attacker, defender),
                _ => true
            };

        /// <summary>Placeholder: treats any two different combatant instances as enemies.</summary>
        private static bool AreEnemies(CombatantBehaviour a, CombatantBehaviour b)
        {
            return a != b;
        }

        /// <summary>Placeholder: no ally relationship is defined yet; always returns false.</summary>
        private static bool AreAllies(CombatantBehaviour a, CombatantBehaviour b)
        {
            return false;
        }
    }
}