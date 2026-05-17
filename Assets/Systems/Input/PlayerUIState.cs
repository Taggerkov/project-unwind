using UnityEngine;
using UnityEngine.UI;

namespace Systems.Input
{
    public class PlayerUIHandler
    {
        public PlayerLinker PlayerLinker;
        public PlayerUIState UIState;

        public void EnableUI()
        {
            if (UIState.Enabled) return;

            UIState.Enabled = true;
            UIState.Cursor.SetActive(true);
            PlayerLinker.PlayerInput.SwitchCurrentActionMap("UI");
            PlayerLinker.MultiplayerEventSystem.SetSelectedGameObject(UIState.CurrentSelectable.gameObject);
        }

        public void DisableUI()
        {
            if (!UIState.Enabled) return;

            UIState.Enabled = false;
            UIState.Cursor.SetActive(false);
            PlayerLinker.PlayerInput.SwitchCurrentActionMap("Game");
        }
    }

    public class PlayerUIState
    {
        public bool Enabled;
        public GameObject Cursor;
        public RectTransform CursorRectTransform;
        public Selectable CurrentSelectable;
        public RectTransform CurrentSelectableRectTransform;
    }
}