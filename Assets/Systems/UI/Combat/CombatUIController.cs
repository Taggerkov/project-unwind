using System;
using Reflex.Attributes;
using Systems.Combat;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.HitSystem;
using Systems.Core;
using Systems.UI.Combat.HealthBar;
using TMPro;
using UnityEngine;

namespace Systems.UI.Combat
{
    public class CombatUIController : MonoBehaviour, ITickable<CombatManager>
    {
        [Inject] private readonly CombatManager _combatManager;

        [SerializeField] private TMP_Text roundTimerText;

        [Header("Health Bars")] [SerializeField]
        private CombatUIHealthBarController combatant0HealthBar;

        [SerializeField] private CombatUIHealthBarController combatant1HealthBar;
        
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
        
        public void ResetForNewRound()
        {
            combatant0HealthBar.SetInstantHealthFraction(1f);
            combatant1HealthBar.SetInstantHealthFraction(1f);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _combatManager.OnCombatStarted += OnCombatStarted;
            _combatManager.OnHitResolved   += OnHitResolved;
        }

        private void OnDestroy()
        {
            if (!_initialized) return;
            _combatManager.OnCombatStarted -= OnCombatStarted;
            _combatManager.OnHitResolved   -= OnHitResolved;
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

        // ── ITickable ─────────────────────────────────────────────────────────────

        public void LogicTick()
        {
            roundTimerText.text = Math.Ceiling(_combatManager.RoundTimer).ToString();
        }
    }
}