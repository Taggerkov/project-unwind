#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using Systems.Audio.Contracts;
using UnityEditor;
using UnityEngine;

namespace Systems.Audio.Editor
{
    /// <summary>
    /// Editor window for runtime <see cref="AudioManager"/> inspection and control.
    /// Resolves its <see cref="AudioManager"/> dependency from the Reflex root container on play mode
    /// entry, mirroring the injection pattern used by other editor tools in the project.
    /// Per-category volumes and speeds are tracked locally and reset to 1 each time play mode is
    /// entered — they are never read back from the backend.
    /// Open via Unwind → Audio → Manager.
    /// </summary>
    public sealed class AudioManagerWindow : EditorWindow
    {
        private enum Tab { Home, Live, Settings }

        private static readonly string[] TabLabels = { "Home", "Live", "Settings" };
        private static readonly Color SeparatorColor = new(0.5f, 0.5f, 0.5f, 0.5f);

        /// <summary>
        /// Resolved from the Reflex root container on play mode entry. Null outside play mode.
        /// </summary>
        [Inject] private AudioManager _audioManager;

        /// <summary>
        /// True once <see cref="Inject"/> has successfully resolved dependencies from the container.
        /// Guards all play-mode-only GUI paths against accessing a null manager.
        /// </summary>
        private bool _injected;

        /// <summary>
        /// Loaded from the project via <see cref="AssetDatabase"/> in <see cref="OnEnable"/>.
        /// Available in both edit and play mode, so the Home tab always renders.
        /// </summary>
        private AudioSettings _audioSettings;

        private Tab _currentTab = Tab.Live;

        /// <summary>Scroll state for the Live tab handle list.</summary>
        private Vector2 _scrollPosition;

        /// <summary>
        /// Per-handle volume overrides keyed by UUID. Entries are added at 1 on first encounter
        /// and removed when the UUID leaves <see cref="AudioManager.ActiveUuids"/>.
        /// </summary>
        private readonly Dictionary<Guid, float> _handleVolumes = new();

        /// <summary>
        /// Per-handle speed overrides keyed by UUID. Lifecycle mirrors <see cref="_handleVolumes"/>.
        /// </summary>
        private readonly Dictionary<Guid, float> _handleSpeeds = new();

        /// <summary>
        /// Master volume per <see cref="AudioCategory"/>, tracked locally.
        /// Initialised to 1 on play mode entry and pushed to <see cref="AudioManager"/> on change.
        /// </summary>
        private readonly Dictionary<AudioCategory, float> _categoryVolumes = new();

        /// <summary>
        /// Master speed per <see cref="AudioCategory"/>, tracked locally.
        /// Lifecycle and intent mirror <see cref="_categoryVolumes"/>.
        /// </summary>
        private readonly Dictionary<AudioCategory, float> _categorySpeeds = new();

        /// <summary>Opens the Audio Manager window via the Unity menu.</summary>
        [MenuItem("Unwind/Audio/Manager")]
        public static void Open() => GetWindow<AudioManagerWindow>("Audio").Show();

        /// <summary>
        /// Subscribes to per-frame repaints and play mode transitions, loads <see cref="_audioSettings"/>,
        /// and injects immediately if the window is opened while already in play mode.
        /// </summary>
        private void OnEnable()
        {
            EditorApplication.update += Repaint;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            LoadSettings();

            if (Application.isPlaying) Inject();
        }

        /// <summary>Unsubscribes from per-frame repaints and play mode transition events.</summary>
        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        /// <summary>
        /// Routes play mode transitions: injects on <see cref="PlayModeStateChange.EnteredPlayMode"/>,
        /// resets on <see cref="PlayModeStateChange.ExitingPlayMode"/>.
        /// </summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Inject();
            if (state == PlayModeStateChange.ExitingPlayMode) ResetState();
        }

        /// <summary>
        /// Resolves all <see cref="InjectAttribute"/>-marked fields via the Reflex root container
        /// and initialises per-category tracking.
        /// Uses <see cref="AttributeInjector"/> rather than constructor injection because
        /// <see cref="EditorWindow"/> instances are created by Unity, not the container.
        /// </summary>
        private void Inject()
        {
            AttributeInjector.Inject(this, Container.RootContainer);
            _injected = true;
            InitCategoryDicts();
        }

        /// <summary>
        /// Nulls the manager reference and clears all runtime tracking to prevent access to a
        /// disposed instance during the brief window between
        /// <see cref="PlayModeStateChange.ExitingPlayMode"/> and container teardown.
        /// </summary>
        private void ResetState()
        {
            _audioManager = null;
            _injected = false;
            _handleVolumes.Clear();
            _handleSpeeds.Clear();
            _categoryVolumes.Clear();
            _categorySpeeds.Clear();
            Repaint();
        }

        /// <summary>
        /// Loads <see cref="AudioSettings"/> via <see cref="AssetDatabase"/> rather than the container
        /// so the Home tab can render in edit mode without requiring play mode.
        /// No-op if no <see cref="AudioSettings"/> asset exists in the project.
        /// </summary>
        private void LoadSettings()
        {
            var guids = AssetDatabase.FindAssets("t:AudioSettings");
            if (guids.Length == 0) return;
            _audioSettings = AssetDatabase.LoadAssetAtPath<AudioSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>
        /// Seeds <see cref="_categoryVolumes"/> and <see cref="_categorySpeeds"/> to 1 for every
        /// <see cref="AudioCategory"/> value, matching the initial state set by
        /// <see cref="Runtime.BuiltIn.BuiltInAudio"/> at construction.
        /// Does not read back current values from the backend.
        /// </summary>
        private void InitCategoryDicts()
        {
            foreach (AudioCategory cat in Enum.GetValues(typeof(AudioCategory)))
            {
                _categoryVolumes[cat] = 1f;
                _categorySpeeds[cat] = 1f;
            }
        }

        /// <summary>Main GUI entry point. Draws the header, tab bar, and the active tab body.</summary>
        private void OnGUI()
        {
            DrawHeader();
            DrawTabs();

            switch (_currentTab)
            {
                case Tab.Home:     DrawHomeTab();     break;
                case Tab.Live:     DrawLiveTab();     break;
                case Tab.Settings: DrawSettingsTab(); break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        // ── Header ────────────────────────────────────────────────────────────

        /// <summary>Draws the window title and descriptive subtitle.</summary>
        private static void DrawHeader()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Audio Manager", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Runtime audio inspection and control.", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
        }

        // ── Tabs ──────────────────────────────────────────────────────────────

        /// <summary>Draws the tab toolbar and updates <see cref="_currentTab"/> on selection.</summary>
        private void DrawTabs()
        {
            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab, TabLabels);
            DrawSeparator();
        }

        // ── Home tab ──────────────────────────────────────────────────────────

        /// <summary>
        /// Draws the Home tab. Displays the active <see cref="AudioSettings"/> configuration as
        /// read-only fields and provides a button to ping the asset in the Project window.
        /// Available in both edit and play modes.
        /// </summary>
        private void DrawHomeTab()
        {
            if (_audioSettings == null)
            {
                EditorGUILayout.HelpBox("No AudioSettings asset found in the project.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Backend", _audioSettings.Backend);
                EditorGUILayout.IntField("Pool Size", _audioSettings.PoolSize);
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Select AudioSettings Asset"))
                Selection.activeObject = _audioSettings;
        }

        // ── Live tab ──────────────────────────────────────────────────────────

        /// <summary>
        /// Draws the Live tab. Guards against edit-mode access and a missing manager,
        /// then delegates to <see cref="DrawActiveHandles"/>.
        /// </summary>
        private void DrawLiveTab()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect active sounds.", MessageType.Info);
                return;
            }

            if (!_injected || _audioManager == null)
            {
                EditorGUILayout.HelpBox("AudioManager not found.", MessageType.Warning);
                return;
            }

            DrawActiveHandles();
        }

        /// <summary>
        /// Snapshots <see cref="AudioManager.ActiveUuids"/> via <c>ToList</c> to avoid
        /// modification-during-enumeration if a handle is released mid-frame, syncs
        /// <see cref="_handleVolumes"/> and <see cref="_handleSpeeds"/> against that snapshot
        /// (adding new entries at 1, removing stale ones), then draws a scrollable row per
        /// active instance via <see cref="DrawHandleRow"/>.
        /// </summary>
        private void DrawActiveHandles()
        {
            var uuids = _audioManager.ActiveUuids.ToList();

            foreach (var uuid in uuids)
            {
                _handleVolumes.TryAdd(uuid, 1f);
                _handleSpeeds.TryAdd(uuid, 1f);
            }

            foreach (var stale in _handleVolumes.Keys.Except(uuids).ToList())
            {
                _handleVolumes.Remove(stale);
                _handleSpeeds.Remove(stale);
            }

            EditorGUILayout.LabelField(
                $"{uuids.Count} active sound{(uuids.Count == 1 ? "" : "s")}",
                EditorStyles.miniLabel);
            DrawSeparator();

            if (uuids.Count == 0)
            {
                EditorGUILayout.LabelField("No active sounds.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var uuid in uuids)
            {
                DrawHandleRow(uuid);
                DrawSeparator();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws a single handle row: a truncated UUID, playing status, volume and speed sliders,
        /// and Stop / Pause / Resume buttons.
        /// Volume and speed changes are applied to <see cref="AudioManager"/> immediately so the
        /// live sound is affected in the same frame the slider is dragged.
        /// </summary>
        /// <param name="uuid">The UUID of the active playback instance to render.</param>
        private void DrawHandleRow(Guid uuid)
        {
            var isPlaying = _audioManager.IsPlaying(uuid);
            var shortId = uuid.ToString()[..8];

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(shortId, EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField(
                isPlaying ? "● Playing" : "○ Stopped",
                isPlaying ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();

            DrawFloatSlider("Vol", _handleVolumes[uuid], 0f, 2f, v =>
            {
                _handleVolumes[uuid] = v;
                _audioManager.SetVolume(uuid, v);
            });

            DrawFloatSlider("Spd", _handleSpeeds[uuid], 0f, 3f, v =>
            {
                _handleSpeeds[uuid] = v;
                _audioManager.SetSpeed(uuid, v);
            });

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Stop",   EditorStyles.miniButton)) _audioManager.Stop(uuid);
            if (GUILayout.Button("Pause",  EditorStyles.miniButton)) _audioManager.Pause(uuid);
            if (GUILayout.Button("Resume", EditorStyles.miniButton)) _audioManager.Resume(uuid);
            EditorGUILayout.EndHorizontal();
        }

        // ── Settings tab ──────────────────────────────────────────────────────

        /// <summary>
        /// Draws the Settings tab. Provides per-<see cref="AudioCategory"/> master volume and speed
        /// sliders and bulk Stop / Pause / Resume actions.
        /// Slider values are pushed to <see cref="AudioManager"/> on change and mirrored in
        /// <see cref="_categoryVolumes"/> and <see cref="_categorySpeeds"/> for display continuity
        /// across repaints.
        /// </summary>
        private void DrawSettingsTab()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to control audio categories.", MessageType.Info);
                return;
            }

            if (!_injected || _audioManager == null)
            {
                EditorGUILayout.HelpBox("AudioManager not found.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Category Volumes", EditorStyles.boldLabel);
            foreach (AudioCategory cat in Enum.GetValues(typeof(AudioCategory)))
            {
                DrawFloatSlider(cat.ToString(), _categoryVolumes[cat], 0f, 2f, v =>
                {
                    _categoryVolumes[cat] = v;
                    _audioManager.SetCategoryVolume(cat, v);
                });
            }

            DrawSeparator();

            EditorGUILayout.LabelField("Category Speeds", EditorStyles.boldLabel);
            foreach (AudioCategory cat in Enum.GetValues(typeof(AudioCategory)))
            {
                DrawFloatSlider(cat.ToString(), _categorySpeeds[cat], 0f, 3f, v =>
                {
                    _categorySpeeds[cat] = v;
                    _audioManager.SetCategorySpeed(cat, v);
                });
            }

            DrawSeparator();

            EditorGUILayout.LabelField("Category Actions", EditorStyles.boldLabel);
            foreach (AudioCategory cat in Enum.GetValues(typeof(AudioCategory)))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(cat.ToString(), GUILayout.Width(60));
                if (GUILayout.Button("Stop All",   EditorStyles.miniButton)) _audioManager.StopAll(cat);
                if (GUILayout.Button("Pause All",  EditorStyles.miniButton)) _audioManager.PauseAll(cat);
                if (GUILayout.Button("Resume All", EditorStyles.miniButton)) _audioManager.ResumeAll(cat);
                EditorGUILayout.EndHorizontal();
            }
        }

        // ── Shared drawing utilities ──────────────────────────────────────────

        /// <summary>
        /// Draws a labeled horizontal slider and invokes <paramref name="onChange"/> only when the
        /// value has actually changed, avoiding redundant callbacks to <see cref="AudioManager"/>
        /// on every repaint while the slider is idle.
        /// </summary>
        /// <param name="label">Display label rendered to the left of the slider.</param>
        /// <param name="current">The current value used to seed the slider position.</param>
        /// <param name="min">The minimum value the slider can produce.</param>
        /// <param name="max">The maximum value the slider can produce.</param>
        /// <param name="onChange">Invoked with the new value when it differs from <paramref name="current"/>.</param>
        private static void DrawFloatSlider(string label, float current, float min, float max, Action<float> onChange)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(50));
            var next = EditorGUILayout.Slider(current, min, max);
            EditorGUILayout.EndHorizontal();

            if (!Mathf.Approximately(next, current))
                onChange(next);
        }

        /// <summary>Draws a 1 px horizontal separator line followed by a small vertical gap.</summary>
        private static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, SeparatorColor);
            EditorGUILayout.Space(4);
        }
    }
}
#endif
