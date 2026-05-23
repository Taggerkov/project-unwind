using System.Collections.Generic;
using JetBrains.Annotations;
using Reflex.Attributes;
using Systems.Combat;
using Systems.Combat.Combatant.Behaviour;
using Systems.Core;
using Systems.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.UI.Dev.InputHistory.Scripts
{
    public class InputHistoryUIList : MonoBehaviour, ITickable<CombatManager>
    {
        [Inject] private readonly CombatManager _combatManager;

        [SerializeField] private VisualTreeAsset rowTemplate;
        [SerializeField] private InputIconsSo iconAtlasSo;
        [SerializeField] private int maxHistoryEntries = 15;
        [SerializeField] private int maxFrameCount = 999;

        [CanBeNull] private IInputProvider _p0PlayerInputProvider;
        [CanBeNull] private IInputProvider _p1PlayerInputProvider;

        private List<InputUtils.CompressedInput> _p0InputHistory = new();
        private List<InputUtils.CompressedInput> _p1InputHistory = new();

        private List<InputHistoryEntry> _p0UIEntries = new();
        private List<InputHistoryEntry> _p1UIEntries = new();

        private VisualElement _rootElement;

        private VisualElement _p0Container;
        private VisualElement _p1Container;

        private bool _started = false; // guard for pre-injection OnEnable calls

        private void Start()
        {
            _rootElement = GetComponent<UIDocument>().rootVisualElement;
            _p0Container = _rootElement.Q<VisualElement>("Player0InputHistory");
            _p1Container = _rootElement.Q<VisualElement>("Player1InputHistory");

            for (int i = 0; i < maxHistoryEntries; i++)
            {
                var row = new InputHistoryEntry(rowTemplate);
                row.style.display = DisplayStyle.None; // Hide until used
                _p0UIEntries.Add(row);
                _p0Container.Add(row);

                var row2 = new InputHistoryEntry(rowTemplate);
                row2.style.display = DisplayStyle.None; // Hide until used
                _p1UIEntries.Add(row2);
                _p1Container.Add(row2);
            }

            _combatManager.OnInputProviderChanged += BindToPlayer;
            _combatManager.OnCombatStarted += OnCombatStarted;
            _combatManager.RegisterTickable(this);

            _started = true;
            
            Hide();
        }

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

        private void OnCombatEnded()
        {
            _combatManager.OnCombatEnded -= OnCombatEnded;
            Hide();
        }

        private void OnEnable()
        {
            if (!_started) return; // Avoid subscribing before Start runs and dependencies are injected
            _combatManager.OnInputProviderChanged += BindToPlayer;
        }

        private void OnDisable()
        {
            if (!_started) return; // Avoid unsubscribing before Start runs and dependencies are injected
            _combatManager.OnInputProviderChanged -= BindToPlayer;
        }

        public void Show()
        {
            _rootElement.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _rootElement.style.display = DisplayStyle.None;
        }

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

        public void InputTick()
        {
            UpdateInputHandlerHistory(_p0PlayerInputProvider, _p0InputHistory);
            UpdateInputHandlerHistory(_p1PlayerInputProvider, _p1InputHistory);

            RefreshVisualElements(_p0InputHistory, _p0UIEntries);
            RefreshVisualElements(_p1InputHistory, _p1UIEntries);
        }

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