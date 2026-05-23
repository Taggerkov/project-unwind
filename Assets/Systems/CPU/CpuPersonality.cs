using UnityEngine;

namespace Systems.CPU
{
    /// <summary>
    /// Character-level AI tuning knobs. Assign one per character in the inspector.
    /// Analogous to ArcSys's per-character cpuWeakenRate / cpuTokuiKyori cluster.
    /// </summary>
    [CreateAssetMenu(fileName = "CPUPersonality", menuName = "Unwind Database/Combat/AI Personality")]
    public class CpuPersonality : ScriptableObject
    {
        // ── Spacing ───────────────────────────────────────────────────────────────────

        [Header("Spacing")]
        [Tooltip("Desired world-unit distance from the opponent.")]
        [Min(0f)] public float PreferredDistance = 2.0f;

        [Tooltip("Dead zone around PreferredDistance. " +
                 "The AI won't reposition until it drifts outside this band. ")]
        [Min(0f)] public float DistanceTolerance = 0.5f;

        // ── Behavior ──────────────────────────────────────────────────────────────────

        [Header("Behavior Weights")]
        [Range(0, 100)]
        [Tooltip("Chance (0–100) to attempt an attack each time a viable move is found. " +
                 "Lower values create a more passive, defensive AI.")]
        public int Aggression = 60;

        [Range(0, 100)]
        [Tooltip("Chance (0–100) to begin guarding when the opponent enters their Active phase. " +
                 "0 = never blocks, 100 = always blocks.")]
        public int GuardSensitivity = 55;

        // ── Timing ────────────────────────────────────────────────────────────────────

        [Header("Timing")]
        [Tooltip("Simulated human reaction lag in ticks. " +
                 "The AI waits this many ticks before acting on a newly spotted threat.")]
        [Range(0, 30)] public int ReactionDelayTicks = 8;

        [Tooltip("Minimum ticks between any two attack attempts, regardless of per-move cooldowns. " +
                 "Prevents the AI from spamming attacks every tick.")]
        [Range(0, 120)] public int GlobalAttackCooldownTicks = 20;
    }
}