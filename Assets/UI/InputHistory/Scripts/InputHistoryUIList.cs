using System.Collections.Generic;
using FixedLogic;
using JetBrains.Annotations;
using Reflex.Attributes;
using Systems.Core;
using Systems.Input;
using UI.Icons;
using UI.Scripts;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.InputHistory.Scripts
{
    public class InputHistoryUIList : MonoBehaviour, ITickable
    {
        [Inject] private readonly PlayerRegistry _playerRegistry;

        [SerializeField] private VisualTreeAsset rowTemplate;
        [SerializeField] private InputIconsSo iconAtlasSo;
        [SerializeField] private int maxHistoryEntries = 15;
        [SerializeField] private int maxFrameCount = 999;

        [CanBeNull] private PlayerInputProvider _p0PlayerInputProvider;
        [CanBeNull] private PlayerInputProvider _p1PlayerInputProvider;

        private List<InputUtils.CompressedInput> _p0InputHistory = new();
        private List<InputUtils.CompressedInput> _p1InputHistory = new();

        private List<InputHistoryEntry> _p0UIEntries = new();
        private List<InputHistoryEntry> _p1UIEntries = new();

        private VisualElement _p0Container;
        private VisualElement _p1Container;

        private void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _p0Container = root.Q<VisualElement>("Player0InputHistory");
            _p1Container = root.Q<VisualElement>("Player1InputHistory");

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
        }

        private void OnEnable()
        {
            _playerRegistry.OnPlayerJoined += BindToPlayer;
        }

        private void OnDisable()
        {
            _playerRegistry.OnPlayerJoined -= BindToPlayer;
        }

        private void UpdateInputHandlerHistory(PlayerInputProvider targetProvider,
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

        private void BindToPlayer(PlayerLinker playerLinker)
        {
            if (!playerLinker)
            {
                Debug.LogError("InputHistoryUIList: Cannot bind to null PlayerLinker!");
                return;
            }

            if (_p0PlayerInputProvider != null && playerLinker.PlayerId == 0 ||
                _p1PlayerInputProvider != null && playerLinker.PlayerId == 1)
            {
                Debug.LogWarning(
                    $"InputHistoryUIList: Player {playerLinker.PlayerId} already bound. Ignoring additional join.");
                return;
            }

            switch (playerLinker.PlayerId)
            {
                case 0:
                    _p0PlayerInputProvider = playerLinker.PlayerInputProvider;
                    Debug.Log("InputHistoryUIList: Bound to Player 0 InputHandler.");
                    break;
                case 1:
                    _p1PlayerInputProvider = playerLinker.PlayerInputProvider;
                    Debug.Log("InputHistoryUIList: Bound to Player 1 InputHandler.");
                    break;
            }
        }
    }
}