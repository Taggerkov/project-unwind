using System.Collections.Generic;
using Systems.Combat.Combatant.Behaviour;
using Unity.Mathematics.Geometry;

namespace Systems.Combat.HitSystem
{
    public class CombatOverlapSolver
    {
        private Dictionary<CombatantBehaviour, MinMaxAABB[]> _hurtboxes = new();
        private Dictionary<CombatantBehaviour, (HitData, MinMaxAABB[])> _hitboxes = new();

        private readonly HashSet<(CombatantBehaviour perpetrator, uint hitId, CombatantBehaviour victim)>
            _hitRegistry = new();

        public void RegisterHurtboxes(CombatantBehaviour combatantBehaviour, MinMaxAABB[] hurtboxes)
        {
            _hurtboxes[combatantBehaviour] = hurtboxes;
        }

        public void RegisterHitboxes(CombatantBehaviour combatantBehaviour, HitData hitData, MinMaxAABB[] hitboxes)
        {
            _hitboxes[combatantBehaviour] = (hitData, hitboxes);
        }

        public void ClearFramedata()
        {
            _hurtboxes.Clear();
            _hitboxes.Clear();
        }

        public void ClearHitRegistry(CombatantBehaviour perpetrator) =>
            _hitRegistry.RemoveWhere(e => e.perpetrator == perpetrator);

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


        private static bool TargetMatches(EHitTarget target, CombatantBehaviour attacker, CombatantBehaviour defender)
            => target switch
            {
                EHitTarget.Enemy => AreEnemies(attacker, defender),
                EHitTarget.Ally => AreAllies(attacker, defender),
                _ => true
            };

        private static bool AreEnemies(CombatantBehaviour a, CombatantBehaviour b)
        {
            return a != b; // TODO: Placeholder: consider all different combatants as enemies
        }

        private static bool AreAllies(CombatantBehaviour a, CombatantBehaviour b)
        {
            return false; // TODO: Placeholder: no allies in this simple implementation
        }
    }
}