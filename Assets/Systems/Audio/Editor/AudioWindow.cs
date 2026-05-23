#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using Systems.Audio.Contracts;
using Systems.Audio.Music;
using Systems.Audio.Shared;
using Systems.Audio.Voiceline;
using UnityEditor;
using UnityEngine;

namespace Systems.Audio.Editor
{
    /// <summary>
    /// Runtime control centre for the audio system. Inspects and drives the <see cref="AudioManager"/>,
    /// <see cref="MusicManager"/>, and <see cref="VoicelineManager"/>, surfaces backend diagnostics, and
    /// auditions audio assets. Dependencies are resolved from the Reflex root container on play mode entry.
    /// Open via Unwind, Audio, Manager.
    /// </summary>
    public sealed class AudioManagerWindow : EditorWindow
    {
        /// <summary>Identifies which tab is currently active in the window toolbar.</summary>
        private enum Tab
        {
            /// <summary>Configuration readout and backend diagnostics.</summary>
            Home,
            /// <summary>Live playback handles with per-handle controls.</summary>
            Live,
            /// <summary>Playlist transport and music volume control.</summary>
            Music,
            /// <summary>Voiceline queue, transport, and volume control.</summary>
            Voice,
            /// <summary>Per-category volume, speed, mute, and bulk actions.</summary>
            Settings,
            /// <summary>Preview playback of AudioEvent and AudioSheet assets.</summary>
            Audition
        }

        /// <summary>Display strings for the tab toolbar, ordered to match <see cref="Tab"/>.</summary>
        private static readonly string[] TabLabels = { "Home", "Live", "Music", "Voice", "Settings", "Audition" };

        /// <summary>SessionState key under which the active tab persists across domain reloads.</summary>
        private const string TabStateKey = "Unwind.AudioWindow.Tab";

        /// <summary>Semi-transparent grey used to draw 1 px horizontal separator lines.</summary>
        private static readonly Color SeparatorColor = new(0.5f, 0.5f, 0.5f, 0.5f);

        /// <summary>Resolved from the Reflex root container on play mode entry. Null outside play mode.</summary>
        [Inject] private AudioManager _audioManager;

        /// <summary>Resolved from the Reflex root container on play mode entry. Null outside play mode.</summary>
        [Inject] private MusicManager _musicManager;

        /// <summary>Resolved from the Reflex root container on play mode entry. Null outside play mode.</summary>
        [Inject] private VoicelineManager _voicelineManager;

        /// <summary>True once <see cref="Inject"/> has resolved dependencies. Guards play-mode-only paths.</summary>
        private bool _injected;

        /// <summary>True while a per-frame repaint is subscribed, so play mode meters update smoothly.</summary>
        private bool _repainting;

        /// <summary>Loaded from the project via <see cref="AssetDatabase"/> so the Home tab renders in edit mode.</summary>
        private Shared.AudioSettings _audioSettings;

        /// <summary>The tab currently selected in the toolbar.</summary>
        private Tab _currentTab = Tab.Live;

        /// <summary>Scroll state for the Live tab handle list.</summary>
        private Vector2 _scrollPosition;

        /// <summary>Case-insensitive name filter for the Live tab.</summary>
        private string _handleSearch = string.Empty;

        /// <summary>Pre-mute category volumes, keyed by the categories currently muted from this window.</summary>
        private readonly Dictionary<AudioCategory, float> _mutedVolumes = new();

        /// <summary>Fade target and duration for the Music tab fade control.</summary>
        private float _musicFadeTarget = 1f, _musicFadeDuration = 1f;

        /// <summary>Fade target and duration for the Voice tab fade control.</summary>
        private float _voiceFadeTarget = 1f, _voiceFadeDuration = 1f;

        /// <summary>The asset selected for AudioEvent audition, and the UUID of the last audition started.</summary>
        private AudioEvent _auditionEvent;
        private Guid _auditionId;

        /// <summary>Clips this window has preloaded, so audition and the voice tester never re-request a cached key.</summary>
        private readonly HashSet<AudioEvent> _preloaded = new();

        /// <summary>The sheet selected for AudioSheet audition, and the chosen entry index within it.</summary>
        private AudioSheet _auditionSheet;
        private int _auditionSheetIndex;

        /// <summary>Test voiceline asset and priority for the Voice tab enqueue control.</summary>
        private VoicelineEvent _voiceTestEvent;
        private VoicelinePriority _voiceTestPriority = VoicelinePriority.Normal;

        /// <summary>Opens the Audio control centre via the Unity menu.</summary>
        [MenuItem("Unwind/Audio/Manager")]
        public static void Open() => GetWindow<AudioManagerWindow>("Audio").Show();

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _currentTab = (Tab)SessionState.GetInt(TabStateKey, (int)Tab.Live);
            LoadSettings();

            if (Application.isPlaying)
            {
                Inject();
                EnableRepaint();
            }
        }

        private void OnDisable()
        {
            DisableRepaint();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SessionState.SetInt(TabStateKey, (int)_currentTab);
        }

        /// <summary>Injects on entering play mode, resets on leaving it.</summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Inject();
                EnableRepaint();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                DisableRepaint();
                ResetState();
            }
        }

        /// <summary>Subscribes per-frame repaint so live meters update. Idempotent.</summary>
        private void EnableRepaint()
        {
            if (_repainting) return;
            EditorApplication.update += Repaint;
            _repainting = true;
        }

        /// <summary>Unsubscribes per-frame repaint. Idempotent.</summary>
        private void DisableRepaint()
        {
            if (!_repainting) return;
            EditorApplication.update -= Repaint;
            _repainting = false;
        }

        /// <summary>
        /// Resolves all <see cref="InjectAttribute"/> fields via the Reflex root container.
        /// Uses <see cref="AttributeInjector"/> because <see cref="EditorWindow"/> instances are created by Unity.
        /// </summary>
        private void Inject()
        {
            AttributeInjector.Inject(this, Container.RootContainer);
            _injected = true;
        }

        /// <summary>Drops manager references and runtime tracking to avoid touching disposed instances.</summary>
        private void ResetState()
        {
            _audioManager = null;
            _musicManager = null;
            _voicelineManager = null;
            _injected = false;
            _mutedVolumes.Clear();
            _preloaded.Clear();
            _auditionId = Guid.Empty;
            Repaint();
        }

        /// <summary>Loads the first <see cref="AudioSettings"/> asset so the Home tab renders without play mode.</summary>
        private void LoadSettings()
        {
            var guids = AssetDatabase.FindAssets("t:AudioSettings");
            if (guids.Length == 0) return;
            _audioSettings = AssetDatabase.LoadAssetAtPath<Shared.AudioSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ── GUI root ────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawHeader();
            DrawTabs();

            switch (_currentTab)
            {
                case Tab.Home:     DrawHomeTab();     break;
                case Tab.Live:     DrawLiveTab();     break;
                case Tab.Music:    DrawMusicTab();    break;
                case Tab.Voice:    DrawVoiceTab();    break;
                case Tab.Settings: DrawSettingsTab(); break;
                case Tab.Audition: DrawAuditionTab(); break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private static void DrawHeader()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Audio Control Centre", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Runtime audio inspection and control.", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
        }

        private void DrawTabs()
        {
            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab, TabLabels);
            DrawSeparator();
        }

        /// <summary>
        /// Returns true when the managers are available, otherwise draws an explanatory box.
        /// Guards every play-mode-only tab.
        /// </summary>
        private bool RequireRuntime()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use this tab.", MessageType.Info);
                return false;
            }
            if (!_injected || _audioManager == null)
            {
                EditorGUILayout.HelpBox("Audio services not found.", MessageType.Warning);
                return false;
            }
            return true;
        }

        // ── Home tab ──────────────────────────────────────────────────────────

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

            if (GUILayout.Button("Select AudioSettings Asset"))
                Selection.activeObject = _audioSettings;

            if (!Application.isPlaying || !_injected || _audioManager == null) return;

            DrawSeparator();
            EditorGUILayout.LabelField("Backend Diagnostics", EditorStyles.boldLabel);

            if (!_audioManager.TryGetBackendStats(out var stats))
            {
                EditorGUILayout.LabelField("Active backend reports no statistics.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EditorGUILayout.LabelField("Active sources", stats.ActiveSources.ToString());
            EditorGUILayout.LabelField("Created sources", $"{stats.CreatedSources} (configured {stats.ConfiguredPoolSize})");
            EditorGUILayout.LabelField("Cached clips", stats.CachedClips.ToString());
            EditorGUILayout.LabelField("In-flight loads", stats.InFlightLoads.ToString());

            if (stats.PoolGrew)
                EditorGUILayout.HelpBox(
                    "The source pool grew past its configured size. Raise AudioSettings.PoolSize to cover peak concurrent sounds.",
                    MessageType.Warning);
        }

        // ── Live tab ──────────────────────────────────────────────────────────

        private void DrawLiveTab()
        {
            if (!RequireRuntime()) return;

            var snaps = new List<AudioPlaybackSnapshot>();
            foreach (var uuid in _audioManager.ActiveUuids.ToList())
                if (_audioManager.TryGetSnapshot(uuid, out var snap)) snaps.Add(snap);

            _handleSearch = EditorGUILayout.TextField("Search", _handleSearch);
            EditorGUILayout.LabelField($"{snaps.Count} active ({CategoryCounts(snaps)})", EditorStyles.miniLabel);
            DrawSeparator();

            var filtered = string.IsNullOrEmpty(_handleSearch)
                ? snaps
                : snaps.Where(s => s.Name.IndexOf(_handleSearch, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            if (filtered.Count == 0)
            {
                EditorGUILayout.LabelField("No matching sounds.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (var snap in filtered)
            {
                DrawHandleRow(snap);
                DrawSeparator();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHandleRow(AudioPlaybackSnapshot s)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(s.Name, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                s.IsLooping ? $"{s.Category} (loop)" : s.Category.ToString(),
                EditorStyles.miniLabel, GUILayout.Width(110));
            EditorGUILayout.LabelField(StatusLabel(s), EditorStyles.miniLabel, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            DrawFloatSlider("Vol", s.Volume, 0f, 2f, v => _audioManager.SetVolume(s.Uuid, v));
            DrawFloatSlider("Spd", s.Speed, 0f, 3f, v => _audioManager.SetSpeed(s.Uuid, v));

            if (!s.IsLooping && s.Length > 0f)
            {
                var rect = EditorGUILayout.GetControlRect(false, 14);
                EditorGUI.ProgressBar(rect, Mathf.Clamp01(s.Time / s.Length), $"{s.Time:0.0} / {s.Length:0.0}s");
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Stop", EditorStyles.miniButton)) _audioManager.Stop(s.Uuid);
            if (GUILayout.Button("Pause", EditorStyles.miniButton)) _audioManager.Pause(s.Uuid);
            if (GUILayout.Button("Resume", EditorStyles.miniButton)) _audioManager.Resume(s.Uuid);
            EditorGUILayout.EndHorizontal();
        }

        private static string StatusLabel(AudioPlaybackSnapshot s) =>
            s.IsPaused ? "Paused" : s.IsPlaying ? "Playing" : "Stopped";

        private static string CategoryCounts(List<AudioPlaybackSnapshot> snaps) =>
            snaps.Count == 0
                ? "none"
                : string.Join(", ", snaps.GroupBy(s => s.Category).Select(g => $"{g.Key}:{g.Count()}"));

        // ── Music tab ───────────────────────────────────────────────────────

        private void DrawMusicTab()
        {
            if (!RequireRuntime()) return;
            if (_musicManager == null)
            {
                EditorGUILayout.HelpBox("MusicManager not found.", MessageType.Warning);
                return;
            }

            var track = _musicManager.CurrentTrack;
            EditorGUILayout.LabelField("Playlist", _musicManager.ActivePlaylist.ToString());
            EditorGUILayout.LabelField("Track",
                track != null
                    ? $"{track.name}  ({_musicManager.CurrentTrackIndex + 1}/{_musicManager.TrackCount})"
                    : "none");
            EditorGUILayout.LabelField("State", StateLabel(_musicManager.IsPlaying, _musicManager.IsPaused));
            DrawSeparator();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Prev")) _musicManager.PreviousTrack();
            if (GUILayout.Button(_musicManager.IsPlaying ? "Pause" : "Play")) _musicManager.TogglePause();
            if (GUILayout.Button("Next")) _musicManager.NextTrack();
            if (GUILayout.Button("Restart")) _musicManager.Restart();
            if (GUILayout.Button("Stop")) _musicManager.Stop();
            EditorGUILayout.EndHorizontal();

            var shuffle = EditorGUILayout.Toggle("Shuffle", _musicManager.ShuffleEnabled);
            if (shuffle != _musicManager.ShuffleEnabled) _musicManager.SetShuffle(shuffle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Activate", GUILayout.Width(60));
            if (GUILayout.Button("Menu")) _musicManager.ActivatePlaylist(PlaylistType.Menu).Forget();
            if (GUILayout.Button("Combat")) _musicManager.ActivatePlaylist(PlaylistType.Combat).Forget();
            if (GUILayout.Button("None")) _musicManager.ActivatePlaylist(PlaylistType.None).Forget();
            EditorGUILayout.EndHorizontal();

            DrawSeparator();
            DrawFloatSlider("Vol", _musicManager.Volume, 0f, 1f, v => _musicManager.SetVolume(v));
            DrawFadeControl(ref _musicFadeTarget, ref _musicFadeDuration,
                (t, d) => _musicManager.FadeVolumeToAsync(t, d).Forget());
        }

        // ── Voice tab ───────────────────────────────────────────────────────

        private void DrawVoiceTab()
        {
            if (!RequireRuntime()) return;
            if (_voicelineManager == null)
            {
                EditorGUILayout.HelpBox("VoicelineManager not found.", MessageType.Warning);
                return;
            }

            var current = _voicelineManager.CurrentVoiceline;
            EditorGUILayout.LabelField("Current", current != null ? current.name : "none");
            EditorGUILayout.LabelField("Priority",
                _voicelineManager.CurrentPriority?.ToString() ?? "none");
            EditorGUILayout.LabelField("Subtitle key", current != null ? current.SubtitleKey : "none");
            EditorGUILayout.LabelField("State", StateLabel(_voicelineManager.IsPlaying, _voicelineManager.IsPaused));
            EditorGUILayout.LabelField("Queued", _voicelineManager.QueueCount.ToString());
            DrawSeparator();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_voicelineManager.IsPlaying ? "Pause" : "Resume")) _voicelineManager.TogglePause();
            if (GUILayout.Button("Skip")) _voicelineManager.Skip();
            if (GUILayout.Button("Restart")) _voicelineManager.Restart();
            if (GUILayout.Button("Clear")) _voicelineManager.Clear();
            if (GUILayout.Button("Stop")) _voicelineManager.Stop();
            EditorGUILayout.EndHorizontal();

            DrawSeparator();
            EditorGUILayout.LabelField("Queue", EditorStyles.boldLabel);
            var queued = _voicelineManager.GetQueuedVoicelines();
            if (queued.Count == 0)
                EditorGUILayout.LabelField("Empty.", EditorStyles.centeredGreyMiniLabel);
            foreach (var ev in queued)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(ev != null ? ev.name : "(null)", EditorStyles.miniLabel);
                if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(70)))
                    _voicelineManager.RemoveFromQueue(ev);
                EditorGUILayout.EndHorizontal();
            }

            DrawSeparator();
            EditorGUILayout.LabelField("Test", EditorStyles.boldLabel);
            _voiceTestEvent = (VoicelineEvent)EditorGUILayout.ObjectField("Voiceline", _voiceTestEvent, typeof(VoicelineEvent), false);
            _voiceTestPriority = (VoicelinePriority)EditorGUILayout.EnumPopup("Priority", _voiceTestPriority);
            using (new EditorGUI.DisabledScope(_voiceTestEvent == null))
                if (GUILayout.Button("Preload and Play"))
                    PlayVoicelineAsync(_voiceTestEvent, _voiceTestPriority).Forget();

            DrawSeparator();
            DrawFloatSlider("Vol", _voicelineManager.Volume, 0f, 1f, v => _voicelineManager.SetVolume(v));
            DrawFadeControl(ref _voiceFadeTarget, ref _voiceFadeDuration,
                (t, d) => _voicelineManager.FadeVolumeToAsync(t, d).Forget());
        }

        private async UniTaskVoid PlayVoicelineAsync(VoicelineEvent ev, VoicelinePriority priority)
        {
            if (ev == null || ev.AudioEvent == null) return;
            if (!_preloaded.Contains(ev.AudioEvent))
            {
                await _voicelineManager.PreloadAsync(ev);
                _preloaded.Add(ev.AudioEvent);
            }
            _voicelineManager.Play(ev, priority);
        }

        // ── Settings tab ──────────────────────────────────────────────────────

        private void DrawSettingsTab()
        {
            if (!RequireRuntime()) return;

            EditorGUILayout.LabelField("Category Volumes", EditorStyles.boldLabel);
            foreach (AudioCategory cat in Enum.GetValues(typeof(AudioCategory)))
                DrawCategoryVolumeRow(cat);

            DrawSeparator();
            EditorGUILayout.LabelField("Category Speeds", EditorStyles.boldLabel);
            foreach (AudioCategory cat in Enum.GetValues(typeof(AudioCategory)))
                DrawFloatSlider(cat.ToString(), _audioManager.GetCategorySpeed(cat), 0f, 3f,
                    v => _audioManager.SetCategorySpeed(cat, v));

            DrawSeparator();
            if (GUILayout.Button("Reset All To 1")) ResetCategories();

            DrawSeparator();
            EditorGUILayout.LabelField("Category Actions", EditorStyles.boldLabel);
            foreach (AudioCategory cat in Enum.GetValues(typeof(AudioCategory)))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(cat.ToString(), GUILayout.Width(60));
                if (GUILayout.Button("Stop All", EditorStyles.miniButton)) _audioManager.StopAll(cat);
                if (GUILayout.Button("Pause All", EditorStyles.miniButton)) _audioManager.PauseAll(cat);
                if (GUILayout.Button("Resume All", EditorStyles.miniButton)) _audioManager.ResumeAll(cat);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCategoryVolumeRow(AudioCategory cat)
        {
            var muted = _mutedVolumes.ContainsKey(cat);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(muted))
                DrawFloatSlider(cat.ToString(), _audioManager.GetCategoryVolume(cat), 0f, 2f,
                    v => _audioManager.SetCategoryVolume(cat, v));

            var nextMuted = GUILayout.Toggle(muted, "M", EditorStyles.miniButton, GUILayout.Width(24));
            if (nextMuted != muted) SetMuted(cat, nextMuted);
            if (GUILayout.Button("Solo", EditorStyles.miniButton, GUILayout.Width(44))) Solo(cat);
            EditorGUILayout.EndHorizontal();
        }

        private void SetMuted(AudioCategory cat, bool muted)
        {
            if (muted)
            {
                _mutedVolumes[cat] = _audioManager.GetCategoryVolume(cat);
                _audioManager.SetCategoryVolume(cat, 0f);
            }
            else if (_mutedVolumes.Remove(cat, out var previous))
            {
                _audioManager.SetCategoryVolume(cat, previous);
            }
        }

        private void Solo(AudioCategory soloed)
        {
            foreach (AudioCategory cat in Enum.GetValues(typeof(AudioCategory)))
                SetMuted(cat, cat != soloed);
        }

        private void ResetCategories()
        {
            _mutedVolumes.Clear();
            foreach (AudioCategory cat in Enum.GetValues(typeof(AudioCategory)))
            {
                _audioManager.SetCategoryVolume(cat, 1f);
                _audioManager.SetCategorySpeed(cat, 1f);
            }
        }

        // ── Audition tab ──────────────────────────────────────────────────────

        private void DrawAuditionTab()
        {
            if (!RequireRuntime()) return;

            EditorGUILayout.LabelField("AudioEvent", EditorStyles.boldLabel);
            _auditionEvent = (AudioEvent)EditorGUILayout.ObjectField("Event", _auditionEvent, typeof(AudioEvent), false);
            DrawAuditionControls(_auditionEvent);

            DrawSeparator();
            EditorGUILayout.LabelField("AudioSheet", EditorStyles.boldLabel);
            _auditionSheet = (AudioSheet)EditorGUILayout.ObjectField("Sheet", _auditionSheet, typeof(AudioSheet), false);

            if (_auditionSheet == null || _auditionSheet.AudioEvents.Count == 0)
            {
                EditorGUILayout.LabelField("Assign a sheet with entries to audition by ID.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var ids = _auditionSheet.AudioEvents.Keys.ToArray();
            var labels = ids.Select(id =>
            {
                var ev = _auditionSheet.AudioEvents[id];
                return $"{id}: {(ev != null ? ev.name : "(null)")}";
            }).ToArray();

            _auditionSheetIndex = Mathf.Clamp(_auditionSheetIndex, 0, ids.Length - 1);
            _auditionSheetIndex = EditorGUILayout.Popup("Entry", _auditionSheetIndex, labels);

            DrawAuditionControls(_auditionSheet.Get(ids[_auditionSheetIndex]));
        }

        /// <summary>
        /// Draws the Preload, Play, Stop, and Unload row for <paramref name="ev"/>.
        /// Preload runs only when the clip is not already tracked as preloaded, so re-auditioning a clip
        /// never re-requests an already-cached key. Play stays disabled until the clip is preloaded.
        /// </summary>
        /// <param name="ev">The event to audition, or null when nothing is selected.</param>
        private void DrawAuditionControls(AudioEvent ev)
        {
            var preloaded = ev != null && _preloaded.Contains(ev);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(ev == null || preloaded))
                if (GUILayout.Button("Preload")) PreloadAuditionAsync(ev).Forget();
            using (new EditorGUI.DisabledScope(!preloaded))
                if (GUILayout.Button("Play")) _auditionId = _audioManager.Play(ev);
            using (new EditorGUI.DisabledScope(_auditionId == Guid.Empty))
                if (GUILayout.Button("Stop")) StopAudition();
            using (new EditorGUI.DisabledScope(!preloaded))
                if (GUILayout.Button("Unload")) UnloadAudition(ev);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Preloads <paramref name="ev"/> once and tracks it. No-op when null or already preloaded.</summary>
        private async UniTaskVoid PreloadAuditionAsync(AudioEvent ev)
        {
            if (ev == null || _preloaded.Contains(ev)) return;
            await _audioManager.PreloadAsync(ev);
            _preloaded.Add(ev);
        }

        /// <summary>Stops the active audition playback, if any.</summary>
        private void StopAudition()
        {
            if (_auditionId == Guid.Empty) return;
            _audioManager.Stop(_auditionId);
            _auditionId = Guid.Empty;
        }

        /// <summary>Releases <paramref name="ev"/> and drops it from the preloaded set.</summary>
        private void UnloadAudition(AudioEvent ev)
        {
            if (ev == null) return;
            _audioManager.Unload(ev);
            _preloaded.Remove(ev);
        }

        // ── Shared drawing utilities ──────────────────────────────────────────

        private static string StateLabel(bool playing, bool paused) =>
            paused ? "Paused" : playing ? "Playing" : "Stopped";

        /// <summary>Draws a fade target slider, duration field, and a Fade button invoking <paramref name="fade"/>.</summary>
        private static void DrawFadeControl(ref float target, ref float duration, Action<float, float> fade)
        {
            EditorGUILayout.BeginHorizontal();
            target = EditorGUILayout.Slider("Fade to", target, 0f, 1f);
            duration = EditorGUILayout.FloatField(duration, GUILayout.Width(40));
            if (GUILayout.Button("Fade", GUILayout.Width(50))) fade(target, Mathf.Max(0f, duration));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Draws a labelled slider and invokes <paramref name="onChange"/> only when the value changes.</summary>
        private static void DrawFloatSlider(string label, float current, float min, float max, Action<float> onChange)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(50));
            var next = EditorGUILayout.Slider(current, min, max);
            EditorGUILayout.EndHorizontal();

            if (!Mathf.Approximately(next, current))
                onChange(next);
        }

        private static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, SeparatorColor);
            EditorGUILayout.Space(4);
        }
    }
}
#endif
