using System.Collections.Generic;
using System.Linq;
using Systems.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.UI.Core
{
    /// <summary>
    /// Owns cursor placement and selection for the active screen, in either of two models.
    /// In <see cref="Contracts.CursorMode.Shared"/> there are no cursor objects: a single selection is mirrored
    /// onto every controller's event system so all controllers drive one logical cursor. In
    /// <see cref="Contracts.CursorMode.PerPlayer"/> each player has a cursor object positioned on its own
    /// selectable, replaced by a single shared cursor while two players rest on the same selectable.
    /// Per-player selection state is kept across detach so a reconnecting controller is restored.
    /// </summary>
    public sealed class CursorController
    {
        /// <summary>How far, in cursor-local units, the border sits beyond the selectable on each side.
        /// A small fixed offset keeps a thin, even frame regardless of the button's aspect ratio.</summary>
        private const float CursorOverhang = 4f;

        /// <summary>Resources path of the shared cursor prefab (P0Cursor, P1Cursor, SharedCursor).</summary>
        private const string CursorPrefabResource = "CursorGroup";

        /// <summary>Scratch buffer reused by <see cref="PlaceCursor"/> for world-corner queries.</summary>
        private static readonly Vector3[] WorldCorners = new Vector3[4];

        /// <summary>Shader colour properties on the border material.</summary>
        private static readonly int Player0ColorId = Shader.PropertyToID("_Player0Color");
        private static readonly int Player1ColorId = Shader.PropertyToID("_Player1Color");

        /// <summary>Border colour applied to player 0's single-player cursor via vertex colour.</summary>
        private readonly Color _player0Colour;

        /// <summary>Border colour applied to player 1's single-player cursor via vertex colour.</summary>
        private readonly Color _player1Colour;

        /// <summary>Runtime material instance for the two-tone shared cursor, destroyed on <see cref="Clear"/>.</summary>
        private Material _sharedMaterial;

        /// <summary>
        /// Creates the controller with the per-player cursor colours. Single-player cursors are tinted via
        /// their Image vertex colour; the shared cursor's two colours are pushed onto a material instance.
        /// </summary>
        /// <param name="player0Colour">Border colour for player 0.</param>
        /// <param name="player1Colour">Border colour for player 1.</param>
        public CursorController(Color player0Colour, Color player1Colour)
        {
            _player0Colour = player0Colour;
            _player1Colour = player1Colour;
        }

        /// <summary>Per-player cursor state, retained across disconnects to support reconnect.</summary>
        private sealed class PlayerCursorState
        {
            /// <summary>The selectable this player's cursor rests on.</summary>
            public Selectable Selectable;

            /// <summary>Whether this player's UI input and cursor are active.</summary>
            public bool Enabled;
        }

        /// <summary>The cursor model the current screen uses; set by <see cref="Configure"/> and read throughout.</summary>
        private Contracts.CursorMode _mode;

        /// <summary>Prefab loaded once from Resources; shared across all Configure calls.</summary>
        private GameObject _cursorPrefab;

        /// <summary>The live instance of the cursor prefab, parented under the active screen's canvas.</summary>
        private GameObject _cursorInstance;

        /// <summary>Per-player cursor objects inside the live instance (index 0 = P0, index 1 = P1).</summary>
        private GameObject[] _playerCursors;

        /// <summary>The shared (dual-colour) cursor object inside the live instance, shown on overlap or in shared mode with two controllers.</summary>
        private GameObject _sharedCursor;

        /// <summary>Controllers attached to the active screen, in join order.</summary>
        private readonly List<PlayerLinker> _linkers = new();

        /// <summary>Shared-mode selection, mirrored onto every attached controller.</summary>
        private Selectable _sharedSelection;

        /// <summary>Per-player-mode selection and enabled state, keyed by player id.</summary>
        private readonly Dictionary<int, PlayerCursorState> _states = new();

        /// <summary>
        /// Switches the controller to a screen's cursor model, resets all transient state, and
        /// instantiates the shared cursor prefab under the screen's canvas. Call once per screen
        /// activation, before any controller is attached. If the prefab cannot be loaded the controller
        /// still drives selection, just without visible cursors.
        /// </summary>
        /// <param name="mode">The cursor model the screen uses.</param>
        /// <param name="cursorParent">The transform (the screen's canvas) to parent the cursors under.</param>
        public void Configure(Contracts.CursorMode mode, Transform cursorParent)
        {
            Clear();
            _mode = mode;

            if (!_cursorPrefab) _cursorPrefab = Resources.Load<GameObject>(CursorPrefabResource);
            if (!cursorParent || !_cursorPrefab) return;

            _cursorInstance = Object.Instantiate(_cursorPrefab, cursorParent, false);
            // The cursors are hollow shader-drawn borders, so they render on top of the canvas content
            // without occluding it. Keep them last so they sit above every selectable and background.
            _cursorInstance.transform.SetAsLastSibling();
            _playerCursors = new[]
            {
                FindChild(_cursorInstance.transform, "P0Cursor"),
                FindChild(_cursorInstance.transform, "P1Cursor")
            };
            _sharedCursor = FindChild(_cursorInstance.transform, "SharedCursor");

            ApplyColours();
        }

        /// <summary>
        /// Applies the per-player colours: the single-player cursors are tinted through their Image vertex
        /// colour (the border shader multiplies it in), while the shared cursor gets a material instance
        /// carrying both player colours for its two-tone split.
        /// </summary>
        private void ApplyColours()
        {
            SetImageColour(_playerCursors?[0], _player0Colour);
            SetImageColour(_playerCursors?[1], _player1Colour);

            if (!_sharedCursor || !_sharedCursor.TryGetComponent(out Image sharedImage)) return;

            _sharedMaterial = new Material(sharedImage.material);
            _sharedMaterial.SetColor(Player0ColorId, _player0Colour);
            _sharedMaterial.SetColor(Player1ColorId, _player1Colour);
            sharedImage.material = _sharedMaterial;
            sharedImage.color = Color.white;
        }

        /// <summary>Sets an Image's vertex colour, used to tint a single-player cursor's border.</summary>
        private static void SetImageColour(GameObject cursor, Color colour)
        {
            if (cursor && cursor.TryGetComponent(out Image image)) image.color = colour;
        }

        /// <summary>
        /// Attaches a controller and focuses it. In shared mode it joins the single mirrored selection
        /// (seeding it from <paramref name="focus"/> if none exists). In per-player mode it restores any
        /// held selection for that player, or starts from <paramref name="focus"/>.
        /// </summary>
        /// <param name="linker">The controller to attach.</param>
        /// <param name="focus">The selectable to focus when no selection is already held.</param>
        public void AttachPlayer(PlayerLinker linker, Selectable focus)
        {
            if (!linker || _linkers.Contains(linker)) return;
            _linkers.Add(linker);

            if (_mode == Contracts.CursorMode.Shared)
            {
                _sharedSelection = _sharedSelection ? _sharedSelection : focus;
                SelectOn(linker, _sharedSelection);
                Refresh();
                return;
            }

            int id = linker.PlayerId;
            if (!_states.TryGetValue(id, out var state))
            {
                state = new PlayerCursorState { Selectable = focus, Enabled = true };
                _states[id] = state;
            }

            if (state.Enabled) SelectOn(linker, state.Selectable);
            Refresh();
        }

        /// <summary>
        /// Detaches a controller. Shared selection and per-player state are preserved so a reconnecting
        /// controller resumes where it left off; only the controller's own focus and cursor are cleared.
        /// </summary>
        /// <param name="linker">The controller to detach.</param>
        public void DetachPlayer(PlayerLinker linker)
        {
            if (!_linkers.Remove(linker)) return;

            if (linker) linker.MultiplayerEventSystem.SetSelectedGameObject(null);
            if (_mode == Contracts.CursorMode.PerPlayer && linker) HideCursor(linker.PlayerId);
            Refresh();
        }

        /// <summary>Updates state and visuals when an attached controller navigates.</summary>
        /// <param name="linker">The controller that navigated.</param>
        /// <param name="current">The newly focused selectable.</param>
        public void HandleNavigate(PlayerLinker linker, Selectable current)
        {
            if (!current) return;

            if (_mode == Contracts.CursorMode.Shared)
            {
                SetSharedSelection(current);
                return;
            }

            if (_states.TryGetValue(linker.PlayerId, out var state) && state.Enabled)
            {
                state.Selectable = current;
                Refresh();
            }
        }

        /// <summary>
        /// Moves the shared selection and mirrors it onto every attached controller, skipping any
        /// already focused on it. Shared mode only.
        /// </summary>
        /// <param name="selectable">The selectable to focus everywhere.</param>
        public void SetSharedSelection(Selectable selectable)
        {
            _sharedSelection = selectable;
            if (!selectable) return;

            var target = selectable.gameObject;
            foreach (var linker in _linkers.Where(l => l)
                         .Where(l => l.MultiplayerEventSystem.currentSelectedGameObject != target))
            {
                linker.MultiplayerEventSystem.SetSelectedGameObject(target);
            }

            Refresh();
        }

        /// <summary>
        /// Moves one player's selection and event-system focus, then refreshes cursors. Per-player mode only.
        /// </summary>
        /// <param name="playerId">The player to move.</param>
        /// <param name="selectable">The selectable to focus for that player.</param>
        public void SetSelection(int playerId, Selectable selectable)
        {
            var state = StateFor(playerId);
            state.Selectable = selectable;

            var linker = LinkerFor(playerId);
            if (linker && state.Enabled) SelectOn(linker, selectable);

            Refresh();
        }

        /// <summary>
        /// Sets whether a player's cursor is shown and tracked. Disabling hides the cursor and excludes
        /// the player from overlap and navigation visuals. Per-player mode only; action-map switching is
        /// handled by <see cref="UIManager"/>.
        /// </summary>
        /// <param name="playerId">The player to enable or disable.</param>
        /// <param name="enabled">True to show and track the cursor; false to hide it.</param>
        public void SetPlayerEnabled(int playerId, bool enabled)
        {
            var state = StateFor(playerId);
            state.Enabled = enabled;
            if (!enabled) HideCursor(playerId);
            Refresh();
        }

        /// <summary>Returns whether a player's UI input should be active. True for shared mode and unknown players.</summary>
        /// <param name="playerId">The player to query.</param>
        public bool IsPlayerEnabled(int playerId) =>
            _mode != Contracts.CursorMode.PerPlayer || !_states.TryGetValue(playerId, out var state) || state.Enabled;

        /// <summary>Returns the selectable currently held for a player, or the shared selection in shared mode.</summary>
        /// <param name="playerId">The player to query.</param>
        public Selectable GetSelection(int playerId) =>
            _mode == Contracts.CursorMode.Shared
                ? _sharedSelection
                : _states.TryGetValue(playerId, out var state) ? state.Selectable : null;

        /// <summary>Recomputes cursor visibility and placement for the active mode.</summary>
        public void Refresh()
        {
            if (_mode == Contracts.CursorMode.PerPlayer) RefreshPerPlayer();
            else RefreshShared();
        }

        /// <summary>
        /// Shared mode: all controllers share one selection, so a single controller shows its own
        /// coloured cursor while two or more show the shared (dual) cursor.
        /// </summary>
        private void RefreshShared()
        {
            HideAllPlayerCursors();
            if (_sharedCursor) _sharedCursor.SetActive(false);
            if (!_sharedSelection) return;

            var active = _linkers.Where(l => l).ToList();
            if (active.Count >= 2)
                PlaceCursor(_sharedCursor, _sharedSelection);
            else if (active.Count == 1)
                PlaceCursor(CursorFor(active[0].PlayerId), _sharedSelection);
        }

        /// <summary>Per-player mode: each enabled player's cursor sits on its selectable, replaced by the shared cursor on overlap.</summary>
        private void RefreshPerPlayer()
        {
            if (TryGetOverlap(out var overlap))
            {
                HideAllPlayerCursors();
                PlaceCursor(_sharedCursor, overlap);
                return;
            }

            if (_sharedCursor) _sharedCursor.SetActive(false);

            foreach (var entry in _states)
            {
                var cursor = CursorFor(entry.Key);
                if (!cursor) continue;

                var state = entry.Value;
                if (state.Enabled && state.Selectable) PlaceCursor(cursor, state.Selectable);
                else cursor.SetActive(false);
            }
        }

        /// <summary>Destroys the cursor instance and discards all attached controllers and selection state.</summary>
        public void Clear()
        {
            if (_cursorInstance) Object.Destroy(_cursorInstance);
            if (_sharedMaterial) Object.Destroy(_sharedMaterial);
            _cursorInstance = null;
            _sharedMaterial = null;
            _playerCursors = null;
            _sharedCursor = null;

            _linkers.Clear();
            _states.Clear();
            _sharedSelection = null;
        }

        /// <summary>Returns the named child's game object under a root, or null when absent.</summary>
        private static GameObject FindChild(Transform root, string name)
        {
            var child = root.Find(name);
            return child ? child.gameObject : null;
        }

        /// <summary>Returns the existing per-player state for a player, creating an enabled one if absent.</summary>
        private PlayerCursorState StateFor(int playerId)
        {
            if (!_states.TryGetValue(playerId, out var state))
            {
                state = new PlayerCursorState { Enabled = true };
                _states[playerId] = state;
            }

            return state;
        }

        /// <summary>True when two enabled players rest on the same selectable, returning that selectable.</summary>
        private bool TryGetOverlap(out Selectable overlap)
        {
            overlap = null;

            var active = _states.Values
                .Where(s => s.Enabled && s.Selectable)
                .Select(s => s.Selectable)
                .ToList();

            if (active.Count < 2 || active[0] != active[1]) return false;

            overlap = active[0];
            return true;
        }

        /// <summary>Hides one player's cursor object.</summary>
        private void HideCursor(int playerId)
        {
            var cursor = CursorFor(playerId);
            if (cursor) cursor.SetActive(false);
        }

        /// <summary>Hides every per-player cursor object.</summary>
        private void HideAllPlayerCursors()
        {
            if (_playerCursors == null) return;
            foreach (var cursor in _playerCursors)
                if (cursor) cursor.SetActive(false);
        }

        /// <summary>Returns the cursor object for a player, or null when out of range.</summary>
        private GameObject CursorFor(int playerId) =>
            _playerCursors != null && playerId >= 0 && playerId < _playerCursors.Length
                ? _playerCursors[playerId]
                : null;

        /// <summary>Returns the attached controller for a player id, or null.</summary>
        private PlayerLinker LinkerFor(int playerId) =>
            _linkers.FirstOrDefault(l => l && l.PlayerId == playerId);

        /// <summary>
        /// Shows a cursor and overlays it on a selectable using world-space corners, so it is correct
        /// regardless of the selectable's pivot or any parent/canvas scale (e.g. a scaled panel or a
        /// canvas in a different scale mode). The cursor sits at the selectable's centre and is sized to
        /// the selectable plus a proportional overhang.
        /// </summary>
        private static void PlaceCursor(GameObject cursor, Selectable selectable)
        {
            if (!cursor || !selectable) return;

            cursor.SetActive(true);

            var selectableRect = selectable.GetComponent<RectTransform>();
            var cursorRect = cursor.GetComponent<RectTransform>();
            if (!selectableRect || !cursorRect) return;

            selectableRect.GetWorldCorners(WorldCorners);
            var center = (WorldCorners[0] + WorldCorners[2]) * 0.5f;
            var worldSize = WorldCorners[2] - WorldCorners[0]; // top-right minus bottom-left

            cursorRect.position = center;

            // Convert the target world size into the cursor's own local space (divide out its lossy
            // scale), then add a small fixed overhang on every side.
            var lossy = cursorRect.lossyScale;
            float w = Mathf.Abs(lossy.x) > 1e-5f ? worldSize.x / lossy.x : worldSize.x;
            float h = Mathf.Abs(lossy.y) > 1e-5f ? worldSize.y / lossy.y : worldSize.y;
            cursorRect.sizeDelta = new Vector2(w, h) + Vector2.one * (2f * CursorOverhang);
        }

        /// <summary>Focuses a selectable on a controller's event system, if both are present.</summary>
        private static void SelectOn(PlayerLinker linker, Selectable selectable)
        {
            if (linker && selectable) linker.MultiplayerEventSystem.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
