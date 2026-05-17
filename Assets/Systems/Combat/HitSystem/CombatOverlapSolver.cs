using System.Collections.Generic;
using Systems.Combat.Combatant.Behaviour;
using Unity.Mathematics.Geometry;

namespace Systems.Combat.HitSystem
{
    public class CombatOverlapSolver
    {
        private Dictionary<CombatantBehaviour, MinMaxAABB[]> _hurtboxes = new();
        private Dictionary<CombatantBehaviour, (HitData, MinMaxAABB[])> _hitboxes = new();

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

        public List<(CombatantBehaviour, HitData, CombatantBehaviour)> Solve()
        {
            var result = new List<(CombatantBehaviour, HitData, CombatantBehaviour)>();
            foreach (var hitboxEntry in _hitboxes)
            {
                var attacker = hitboxEntry.Key;
                var (hitData, hitboxes) = hitboxEntry.Value;
                var hitTarget = hitData.HitTarget;

                foreach (var hurtboxEntry in _hurtboxes)
                {
                    var defender = hurtboxEntry.Key;
                    var hurtboxes = hurtboxEntry.Value;

                    // Check if the hit target matches the defender's relation to the attacker
                    if (hitTarget == EHitTarget.Any ||
                        (hitTarget == EHitTarget.Enemy && AreEnemies(attacker, defender)) ||
                        (hitTarget == EHitTarget.Ally && AreAllies(attacker, defender)))
                    {
                        // Check for overlaps between hitboxes and hurtboxes
                        foreach (var hitbox in hitboxes)
                        {
                            foreach (var hurtbox in hurtboxes)
                            {
                                if (hitbox.Overlaps(hurtbox))
                                {
                                    result.Add((defender, hitData, attacker));
                                    break; // Stop checking hurtboxes for this hitbox after the first overlap
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

        private bool AreEnemies(CombatantBehaviour a, CombatantBehaviour b)
        {
            // Implement logic to determine if a and b are enemies
            return a != b; // Placeholder: consider all different combatants as enemies
        }

        private bool AreAllies(CombatantBehaviour a, CombatantBehaviour b)
        {
            // Implement logic to determine if a and b are allies
            return false; // Placeholder: no allies in this simple implementation
        }
    }
}