using System;
using Reflex.Attributes;
using Systems.Combat;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.HitSystem;
using Systems.Core;
using Systems.UI.Overlay.HealthBar;
using Systems.UI.Combat.RoundCounter;
using TMPro;
using UnityEngine;

namespace Systems.UI.Overlay
{
    public class CombatUIController : MonoBehaviour, ITickable<CombatManager>
    {
        [Inject] private readonly CombatManager _combatManager;

        [SerializeField] private TMP_Text roundTimerText;

        [Header("Health Bars")] [SerializeField]
        private CombatUIHealthBarController combatant0HealthBar;

        [SerializeField] private CombatUIHealthBarController combatant1HealthBar;

        [Header("Round Counter")] [SerializeField]
        private CombatUIRoundCounter combatant0RoundCounter;

        [SerializeField] private CombatUIRoundCounter combatant1RoundCounter;

        private bool _initialized;

        // ── Public API ────────────────────────────────────────────────────────────

        public void Show()
        {
            gameObject.SetActive(true);
            Initialize();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void ResetForNewMatch()
        {
            combatant0RoundCounter.ResetRounds();
            combatant1RoundCounter.ResetRounds();
        }

        public void ResetForNewRound()
        {
            combatant0HealthBar.SetInstantHealthFraction(1f);
            combatant1HealthBar.SetInstantHealthFraction(1f);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Subscribes to the three <see cref="CombatManager"/> events this controller needs.
        /// Guarded by <see cref="_initialized"/> so it runs exactly once per lifetime even if
        /// <see cref="Show"/> is called more than once.
        /// </summary>
        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _combatManager.OnCombatStarted += OnCombatStarted;
            _combatManager.OnHitResolved += OnHitResolved;
            _combatManager.OnRoundEnded += OnRoundEnded;
        }

        /// <summary>Unsubscribes all three <see cref="CombatManager"/> events to prevent dangling delegates after the object is destroyed.</summary>
        private void OnDestroy()
        {
            if (!_initialized) return;
            _combatManager.OnCombatStarted -= OnCombatStarted;
            _combatManager.OnHitResolved -= OnHitResolved;
            _combatManager.OnRoundEnded -= OnRoundEnded;
        }

        // ── Combat events ─────────────────────────────────────────────────────────

        private void OnCombatStarted(CombatantBehaviour c0, CombatantBehaviour c1)
        {
            c0.OnHitstunEnded += combatant0HealthBar.OnHitstunEnded;
            c1.OnHitstunEnded += combatant1HealthBar.OnHitstunEnded;
        }

        private void OnHitResolved(HitResult result)
        {
            // Blocked hits deal no HP damage — skip
            if (result.Resolution == EHitResolution.Blocked) return;

            var bar = result.Victim == _combatManager.Combatant0Behaviour
                ? combatant0HealthBar
                : combatant1HealthBar;

            // Stats.HPFraction is already clamped 0–1 by CombatantStats
            bar.OnHealthChanged(result.Victim.Stats.HPFraction);
        }

        private void OnRoundEnded(CombatantSlot winner, int nOfWins)
        {
            if (winner == CombatantSlot.Combatant0)
            {
                switch (nOfWins)
                {
                    case 1:
                        combatant0RoundCounter.SetRound1Won();
                        break;
                    case 2:
                        combatant0RoundCounter.SetRound2Won();
                        break;
                }
            }
            else
            {
                switch (nOfWins)
                {
                    case 1:
                        combatant1RoundCounter.SetRound1Won();
                        break;
                    case 2:
                        combatant1RoundCounter.SetRound2Won();
                        break;
                }
            }
        }

        // ── ITickable ─────────────────────────────────────────────────────────────

        public void LogicTick()
        {
            roundTimerText.text = Math.Ceiling(_combatManager.RoundTimer).ToString();
        }
    }
}