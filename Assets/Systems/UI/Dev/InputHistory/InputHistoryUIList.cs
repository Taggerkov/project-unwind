using System.Collections.Generic;
using JetBrains.Annotations;
using Reflex.Attributes;
using Systems.Combat;
using Systems.Combat.Combatant.Behaviour;
using Systems.Core;
using Systems.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.UI.Dev.InputHistory
{
    /// <summary>
    /// Development-only overlay that renders a scrolling input history for both combatants,
    /// compressing consecutive identical frames into a single labelled row. Subscribes to
    /// <see cref="CombatManager"/> tick and combat lifecycle events.
    /// </summary>
    public class InputHistoryUIList : MonoBehaviour, ITickable<CombatManager>
    {
        /// <summary>Injected combat manager used for event subscriptions and tick registration.</summary>
        [Inject] private readonly CombatManager _combatManager;

        /// <summary>UI Toolkit template cloned for each history row.</summary>
        [SerializeField] private VisualTreeAsset rowTemplate;

        /// <summary>ScriptableObject supplying the direction and button icon sprites.</summary>
        [SerializeField] private InputIconsSo iconAtlasSo;

        /// <summary>Maximum number of compressed input rows retained and displayed per player.</summary>
        [SerializeField] private int maxHistoryEntries = 15;

        /// <summary>Frame-count display cap; prevents the label from overflowing its bounds.</summary>
        [SerializeField] private int maxFrameCount = 999;

        /// <summary>Live input provider for player 0; null when no player is bound to that slot.</summary>
        [CanBeNull] private IInputProvider _p0PlayerInputProvider;

        /// <summary>Live input provider for player 1; null when no player is bound to that slot.</summary>
        [CanBeNull] private IInputProvider _p1PlayerInputProvider;

        /// <summary>Compressed input history ring for player 0, newest entry at index 0.</summary>
        private List<InputUtils.CompressedInput> _p0InputHistory = new();

        /// <summary>Compressed input history ring for player 1, newest entry at index 0.</summary>
        private List<InputUtils.CompressedInput> _p1InputHistory = new();

        /// <summary>Pre-allocated row elements for player 0's history column.</summary>
        private List<InputHistoryEntry> _p0UIEntries = new();

        /// <summary>Pre-allocated row elements for player 1's history column.</summary>
        private List<InputHistoryEntry> _p1UIEntries = new();

        /// <summary>Root visual element of the UIDocument; toggled to show or hide the overlay.</summary>
        private VisualElement _rootElement;

        /// <summary>Column container for player 0's history rows.</summary>
        private VisualElement _p0Container;

        /// <summary>Column container for player 1's history rows.</summary>
        private VisualElement _p1Container;

        /// <summary>Guards against OnEnable subscriptions firing before Start injects dependencies.</summary>
        private bool _started = false;

        /// <summary>
        /// Pre-allocates all row elements, wires combat events, and registers this component
        /// with the combat manager's tick system.
        /// </summary>
        private void Start()
        {
            _rootElement = GetComponent<UIDocument>().rootVisualElement;
            _p0Container = _rootElement.Q<VisualElement>("Player0InputHistory");
            _p1Container = _rootElement.Q<VisualElement>("Player1InputHistory");

            for (int i = 0; i < maxHistoryEntries; i++)
            {
                var row = new InputHistoryEntry(rowTemplate);
                row.style.display = DisplayStyle.None;
                _p0UIEntries.Add(row);
                _p0Container.Add(row);

                var row2 = new InputHistoryEntry(rowTemplate);
                row2.style.display = DisplayStyle.None;
                _p1UIEntries.Add(row2);
                _p1Container.Add(row2);
            }

            _combatManager.OnInputProviderChanged += BindToPlayer;
            _combatManager.OnCombatStarted += OnCombatStarted;
            _combatManager.RegisterTickable(this);

            _started = true;
            
            Hide();
        }

        /// <summary>
        /// Resets both players' bindings and history on combat start, binds to the new
        /// input providers, and shows the overlay.
        /// </summary>
        private void OnCombatStarted(CombatantBehaviour c0, CombatantBehaviour c1)
        {
            // Clear any existing bindings/history when a new combat starts
            _p0PlayerInputProvider = null;
            _p1PlayerInputProvider = null;
            _p0InputHistory.Clear();
            _p1InputHistory.Clear();
            BindToPlayer(CombatantSlot.Combatant0, c0.InputProvider);
            BindToPlayer(CombatantSlot.Combatant1, c1.InputProvider);
            Show();

            _combatManager.OnCombatEnded += OnCombatEnded;
        }

        /// <summary>Hides the overlay and unsubscribes from the combat-ended event.</summary>
        private void OnCombatEnded()
        {
            _combatManager.OnCombatEnded -= OnCombatEnded;
            Hide();
        }

        /// <summary>Resubscribes to provider-changed events when the component re-enables.</summary>
        private void OnEnable()
        {
            if (!_started) return;
            _combatManager.OnInputProviderChanged += BindToPlayer;
        }

        /// <summary>Unsubscribes from provider-changed events when the component disables.</summary>
        private void OnDisable()
        {
            if (!_started) return;
            _combatManager.OnInputProviderChanged -= BindToPlayer;
        }

        /// <summary>Makes the root overlay element visible.</summary>
        public void Show()
        {
            _rootElement.style.display = DisplayStyle.Flex;
        }

        /// <summary>Hides the root overlay element.</summary>
        public void Hide()
        {
            _rootElement.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Reads the current tick from <paramref name="targetProvider"/> and either increments
        /// the frame count of the most recent history entry (if input is unchanged) or inserts
        /// a new compressed entry, keeping the list within <see cref="maxHistoryEntries"/>.
        /// </summary>
        /// <param name="targetProvider">The input provider to sample; no-op when null.</param>
        /// <param name="history">The history list to update in place, newest entry at index 0.</param>
        private void UpdateInputHandlerHistory(IInputProvider targetProvider,
            List<InputUtils.CompressedInput> history)
        {
            if (targetProvider == null) return;

            // 1. PULL the most recent frame from the buffer (Index 0 is the current tick)
            TickInput currentTick = targetProvider.Buffer.GetFrame(0);

            // 2. COMPRESS the data
            if (history.Count > 0 && history[0].FrameCount < maxFrameCount && history[0].Matches(currentTick))
            {
                // Still holding the exact same inputs? Just add to the frame count.
                history[0].FrameCount++;
            }
            else
            {
                // Something changed (pressed a new button, released one, or moved stick)
                InputUtils.CompressedInput newEntry = new InputUtils.CompressedInput(currentTick);
                history.Insert(0, newEntry);

                // Keep the list from growing infinitely
                if (history.Count > maxHistoryEntries)
                {
                    history.RemoveAt(history.Count - 1);
                }
            }
        }

        /// <summary>Called every input tick; samples both providers and refreshes the display.</summary>
        public void InputTick()
        {
            UpdateInputHandlerHistory(_p0PlayerInputProvider, _p0InputHistory);
            UpdateInputHandlerHistory(_p1PlayerInputProvider, _p1InputHistory);

            RefreshVisualElements(_p0InputHistory, _p0UIEntries);
            RefreshVisualElements(_p1InputHistory, _p1UIEntries);
        }

        /// <summary>
        /// Syncs the pre-allocated row elements to the current <paramref name="history"/>,
        /// hiding any rows beyond the history length.
        /// </summary>
        /// <param name="history">The compressed input history to display.</param>
        /// <param name="entries">The pre-allocated UI row pool for the matching player column.</param>
        private void RefreshVisualElements(List<InputUtils.CompressedInput> history, List<InputHistoryEntry> entries)
        {
            for (int i = 0; i < maxHistoryEntries; i++)
            {
                if (i < history.Count)
                {
                    entries[i].Update(history[i], iconAtlasSo);
                }
                else
                {
                    entries[i].style.display = DisplayStyle.None;
                }
            }
        }

        /// <summary>
        /// Binds or unbinds a player column to the given <paramref name="provider"/>.
        /// Dummy or null providers hide the column; real providers show it and begin sampling.
        /// </summary>
        /// <param name="slot">Which combatant slot to update.</param>
        /// <param name="provider">The new input provider, or null to unbind.</param>
        private void BindToPlayer(CombatantSlot slot, IInputProvider provider)
        {
            if (provider == null || provider.ProviderType == EInputProviderType.Dummy)
            {
                switch (slot)
                {
                    case CombatantSlot.Combatant0:
                        _p0Container.style.display = DisplayStyle.None;
                        _p0PlayerInputProvider = null;
                        Debug.Log("InputHistoryUIList: Unbound from Player 0 InputHandler.");
                        break;
                    case CombatantSlot.Combatant1:
                        _p1Container.style.display = DisplayStyle.None;
                        _p1PlayerInputProvider = null;
                        Debug.Log("InputHistoryUIList: Unbound from Player 1 InputHandler.");
                        break;
                }

                return;
            }

            switch (slot)
            {
                case CombatantSlot.Combatant0:
                    _p0Container.style.display = DisplayStyle.Flex;
                    _p0PlayerInputProvider = provider;
                    Debug.Log("InputHistoryUIList: Bound to Player 0 InputHandler.");
                    break;
                case CombatantSlot.Combatant1:
                    _p1Container.style.display = DisplayStyle.Flex;
                    _p1PlayerInputProvider = provider;
                    Debug.Log("InputHistoryUIList: Bound to Player 1 InputHandler.");
                    break;
            }
        }
    }
}