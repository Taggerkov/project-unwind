#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Systems.Combat.Combatant.Behaviour;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Pose = Systems.Combat.Combatant.Behaviour.Pose;

namespace Systems.Combat.Combatant.Animation.Editor
{
    /// <summary>
    /// EditorWindow tool for authoring <see cref="CombatantPoseSheet"/> assets. Provides two tabs:
    /// the Pose Data Editor (manually position a character in scene, capture bone transforms, and
    /// paint hitbox/hurtbox volumes per pose ID) and the Pose Baker (sample every frame of an
    /// <see cref="AnimationClip"/> imported from an FBX and write each frame as a pose into the sheet).
    /// Open via <c>Unwind → Pose Baker</c>.
    /// </summary>
    public class AnimationPoseEditor : EditorWindow
    {
        // ── Source ─────────────────────────────────────────────────────────────────

        /// <summary>Scene GameObject selected as the capture target; must carry a <see cref="CombatantBehaviour"/>.</summary>
        private GameObject _character;

        /// <summary><see cref="CombatantBehaviour"/> resolved from <see cref="_character"/> on selection.</summary>
        private CombatantBehaviour _behaviour;

        /// <summary>Pose sheet asset bound to the selected character; written and read during capture and bake.</summary>
        private CombatantPoseSheet _sheet;

        /// <summary><see cref="PoseAnimator"/> resolved from <see cref="_behaviour"/>; used to apply previewed poses.</summary>
        private PoseAnimator _poseAnimator;

        /// <summary>Fallback pose applied after baking to restore the character to a neutral stance.</summary>
        private Pose _defaultPose;

        /// <summary>Path-keyed bone cache built from the skeleton root; used by ApplyPose and SnapshotBones.</summary>
        private Dictionary<string, Transform> _boneMap;

        /// <summary>Live hurtbox volumes for the currently previewed pose; edited inline and written on Save.</summary>
        private List<MinMaxAABB> _hurtboxes = new();

        /// <summary>Live hitbox volumes for the currently previewed pose; edited inline and written on Save.</summary>
        private List<MinMaxAABB> _hitboxes = new();

        // ── Selection ──────────────────────────────────────────────────────────────

        /// <summary>Identifies which collision-box list a selection or draw call targets.</summary>
        private enum BoxList
        {
            /// <summary>No list is active; selection is cleared.</summary>
            None,
            /// <summary>Targets <see cref="_hurtboxes"/>.</summary>
            Hurtbox,
            /// <summary>Targets <see cref="_hitboxes"/>.</summary>
            Hitbox
        }

        /// <summary>Which box list currently owns the selected element.</summary>
        private BoxList _selectedList = BoxList.None;

        /// <summary>Index of the selected element within <see cref="_selectedList"/>; -1 when nothing is selected.</summary>
        private int _selectedIndex = -1;

        /// <summary>Active tab index: 0 = Pose Data Editor, 1 = Pose Baker.</summary>
        private int _selectedTab = 0;

        /// <summary>True when a valid box element is selected and ready for scene-handle interaction.</summary>
        private bool HasSelection => _selectedList != BoxList.None && _selectedIndex >= 0;

        /// <summary>Scene handle used to drag and resize the selected box in the SceneView.</summary>
        private readonly BoxBoundsHandle _boundsHandle = new();

        // ── ID selection ───────────────────────────────────────────────────────────

        /// <summary>Number of local pose IDs in each collection block (0–99 per block).</summary>
        private const int BlockSize = 100;

        /// <summary>Currently selected collection (block) index; maps to a <see cref="CombatantPoseCollection"/>.</summary>
        private int _blockIndex = 0;

        /// <summary>Local pose offset within the current block (0–99).</summary>
        private int _poseOffset = 0;

        /// <summary>Global pose ID computed as <c>blockIndex * BlockSize + poseOffset</c>.</summary>
        private uint EffectiveId => (uint)(_blockIndex * BlockSize + _poseOffset);

        // ── Bake ───────────────────────────────────────────────────────────────────

        /// <summary>The <see cref="AnimationClip"/> selected for baking; one of the clips in <see cref="_clipsToBake"/>.</summary>
        private AnimationClip _bakeClip;

        /// <summary>FBX asset selected as the clip source; all non-preview clips within it populate <see cref="_clipsToBake"/>.</summary>
        private GameObject _fbxAsset;

        /// <summary>All <see cref="AnimationClip"/> sub-assets found inside <see cref="_fbxAsset"/>.</summary>
        private List<AnimationClip> _clipsToBake = new();

        // ── Styles ─────────────────────────────────────────────────────────────────

        /// <summary>Bold orange mini-label used for overwrite warnings.</summary>
        private GUIStyle _warningStyle;

        /// <summary>Dimmed mini-label used for secondary info rows.</summary>
        private GUIStyle _subtleLabel;

        /// <summary>HelpBox-styled background drawn behind the selected box row.</summary>
        private GUIStyle _rowSelected;

        /// <summary>Empty style drawn behind unselected box rows.</summary>
        private GUIStyle _rowNormal;

        /// <summary>Opens the Pose Editor window from the Unwind menu.</summary>
        [MenuItem("Unwind/Pose Baker")]
        private static void Open() => GetWindow<AnimationPoseEditor>("Pose Editor");

        /// <summary>Subscribes <see cref="OnSceneGUI"/> to the SceneView delegate so box handles render in the scene.</summary>
        private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;

        /// <summary>Unsubscribes <see cref="OnSceneGUI"/> to prevent stale callbacks after the window is closed.</summary>
        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        // ── GUI ────────────────────────────────────────────────────────────────────

        /// <summary>Renders the full editor window: source section, tab bar, and the active tab's content.</summary>
        private void OnGUI()
        {
            InitStyles();

            DrawSourceSection();
            if (!ValidateInputs()) return;

            DrawTabSelection();

            switch (_selectedTab)
            {
                case 0:
                    EditorGUILayout.Space(6);
                    DrawIdSection();
                    EditorGUILayout.Space(6);
                    DrawBoxEditorSection();
                    EditorGUILayout.Space(6);
                    DrawBoxEditSection();
                    break;
                case 1:
                    EditorGUILayout.Space(6);
                    DrawIdSection(true, false, false);
                    EditorGUILayout.Space(6);
                    DrawBakeSection();
                    break;
            }
        }

        // ── Scene GUI ──────────────────────────────────────────────────────────────

        /// <summary>
        /// SceneView delegate that overlays hurtbox and hitbox wireframes and, for the selected box,
        /// a position handle and a resizable <see cref="BoxBoundsHandle"/>.
        /// </summary>
        private void OnSceneGUI(SceneView _)
        {
            if (!_character || !_behaviour || _poseAnimator?.CurrentPose.Bones == null) return;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            DrawSceneBoxList(_hurtboxes, BoxList.Hurtbox,
                new Color(0.2f, 1f, 0.2f, 0.30f),
                new Color(0.2f, 1f, 0.2f, 0.80f));

            DrawSceneBoxList(_hitboxes, BoxList.Hitbox,
                new Color(1f, 0.2f, 0.2f, 0.30f),
                new Color(1f, 0.2f, 0.2f, 0.80f));
        }

        /// <summary>
        /// Draws all boxes in <paramref name="boxes"/> as wireframe cubes in the scene. The selected
        /// box is rendered with an interactive position handle and <see cref="_boundsHandle"/>; changes
        /// are written back to the list immediately so the Inspector reflects them on repaint.
        /// </summary>
        private void DrawSceneBoxList(List<MinMaxAABB> boxes, BoxList type, Color normal, Color selected)
        {
            var origin = (float3)_behaviour.transform.position;

            for (int i = 0; i < boxes.Count; i++)
            {
                bool isSelected = _selectedList == type && _selectedIndex == i;

                Handles.color = isSelected ? selected : normal;

                if (isSelected)
                {
                    var box = boxes[_selectedIndex];

                    _boundsHandle.center = origin + box.Center;
                    _boundsHandle.size = box.Extents;

                    EditorGUI.BeginChangeCheck();
                    Vector3 newWorldCenter = Handles.PositionHandle(_boundsHandle.center, Quaternion.identity);

                    _boundsHandle.center = newWorldCenter;
                    _boundsHandle.size = box.Extents;

                    _boundsHandle.DrawHandle();

                    if (EditorGUI.EndChangeCheck())
                    {
                        float3 finalWorldCenter = _boundsHandle.center;
                        float3 newLocalCenter = finalWorldCenter - origin;
                        float3 newHalf = (float3)_boundsHandle.size * 0.5f;

                        boxes[_selectedIndex] = new MinMaxAABB
                        {
                            Min = newLocalCenter - newHalf,
                            Max = newLocalCenter + newHalf,
                        };

                        Repaint();
                    }
                }
                else
                {
                    Handles.DrawWireCube(
                        origin + boxes[i].Center,
                        boxes[i].Extents);
                }
            }
        }

        // ── Source section ─────────────────────────────────────────────────────────

        /// <summary>Renders the character object field and a read-only display of the resolved pose sheet.</summary>
        private void DrawSourceSection()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _character = (GameObject)EditorGUILayout.ObjectField(
                "Character (scene)", _character, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && _character != null)
                TryBindCharacter();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Pose Sheet", _sheet, typeof(CombatantPoseSheet), false);
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Resolves <see cref="_behaviour"/>, <see cref="_sheet"/>, <see cref="_poseAnimator"/>, and
        /// <see cref="_boneMap"/> from the newly assigned <see cref="_character"/>. Logs a warning and
        /// clears state when the GameObject lacks a <see cref="CombatantBehaviour"/>. Also loads or
        /// synthesises the default pose used to reset the skeleton after baking.
        /// </summary>
        private void TryBindCharacter()
        {
            _behaviour = _character.GetComponent<CombatantBehaviour>();
            if (!_behaviour)
            {
                Debug.LogWarning("[PoseBaker] Selected character has no CombatantBehaviour.");
                _sheet = null;
                _poseAnimator = null;
                _boneMap = null;
                return;
            }

            _sheet = _behaviour.combatantPoseSheet;
            _poseAnimator = _behaviour.Animator;

            _boneMap = new Dictionary<string, Transform>();
            PoseAnimator.BuildBoneCacheRecursive(_boneMap, _poseAnimator.skeletonRoot, "");
            var defaultPoseBoneData = CaptureBones();

            if (_sheet.HasDefaultPose())
            {
                _defaultPose = _sheet.GetDefaultPose();
            }
            else
            {
                _defaultPose = new Pose
                {
                    Bones = defaultPoseBoneData,
                    Hurtboxes = Array.Empty<MinMaxAABB>(),
                    Hitboxes = Array.Empty<MinMaxAABB>(),
                };
            }
        }

        /// <summary>Renders the Pose Data Editor / Pose Baker toolbar that switches <see cref="_selectedTab"/>.</summary>
        private void DrawTabSelection()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Toggle(_selectedTab == 0, "Pose Data Editor", EditorStyles.toolbarButton))
                _selectedTab = 0;

            if (GUILayout.Toggle(_selectedTab == 1, "Pose Baker", EditorStyles.toolbarButton))
                _selectedTab = 1;

            EditorGUILayout.EndHorizontal();
        }

        // ── Bake section ───────────────────────────────────────────────────────────

        /// <summary>
        /// Renders the Pose Baker tab UI: FBX asset picker, clip navigator, frame-count summary,
        /// overwrite warning, and the Bake button. Calls <see cref="BakeClip"/> on confirmation.
        /// </summary>
        private void DrawBakeSection()
        {
            EditorGUILayout.LabelField("Pose Baker", EditorStyles.boldLabel);

            if (_fbxAsset)
            {
                EditorGUILayout.HelpBox(
                    $"Selected FBX: '{_fbxAsset.name}' with {_clipsToBake.Count} clip(s) found.",
                    MessageType.Info);
            }


            var selected = EditorGUILayout.ObjectField(
                "FBX Asset", _fbxAsset, typeof(GameObject), false) as GameObject;

            if (selected != _fbxAsset)
            {
                if (!selected)
                {
                    _fbxAsset = null;
                    _clipsToBake.Clear();
                    _bakeClip = null;
                    return;
                }

                _fbxAsset = selected;
                RefreshClips();

                _bakeClip = _clipsToBake.Count > 0 ? _clipsToBake[0] : null;
            }

            if (_clipsToBake.Count > 0)
            {
                int clipIndex = Mathf.Max(0, Mathf.Min(_clipsToBake.Count - 1, _clipsToBake.IndexOf(_bakeClip)));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("<", GUILayout.Width(28), GUILayout.Height(28)))
                    clipIndex = (clipIndex - 1 + _clipsToBake.Count) % _clipsToBake.Count;
                EditorGUILayout.ObjectField("Clip to Bake", _bakeClip, typeof(AnimationClip), false);
                if (GUILayout.Button(">", GUILayout.Width(28), GUILayout.Height(28)))
                    clipIndex = (clipIndex + 1) % _clipsToBake.Count;
                EditorGUILayout.EndHorizontal();

                _bakeClip = _clipsToBake[clipIndex];
            }

            if (!_bakeClip) return;

            // frameRate is an internal implementation detail of SampleAnimation — it is
            // how we convert a frame index (0, 1, 2 …) into the time value the API needs.
            // We never expose it to the user; the only thing that matters here is frame count.
            int frameCount = Mathf.RoundToInt(_bakeClip.length * _bakeClip.frameRate) + 1;
            int firstId = _blockIndex * BlockSize;
            int lastId = firstId + frameCount - 1;
            int blocksNeeded = Mathf.CeilToInt(frameCount / (float)BlockSize);

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                $"Clip: {_bakeClip.name}\n" +
                $"Frames to bake: {frameCount}  (frames 0 – {frameCount - 1})\n" +
                $"IDs written: {firstId} – {lastId}" +
                (blocksNeeded > 1 ? $"  (spans {blocksNeeded} blocks)" : ""),
                MessageType.None);

            bool anyExist = false;
            for (int i = 0; i < frameCount && !anyExist; i++)
            {
                uint col = (uint)(_blockIndex + i / BlockSize);
                uint pid = (uint)(i % BlockSize);
                if (_sheet.EditorHasId(col, pid)) anyExist = true;
            }

            if (anyExist)
            {
                EditorGUILayout.HelpBox(
                    "⚠ One or more IDs in this range already exist and will be overwritten.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button($"Bake  {frameCount} frames  →  Block {_blockIndex}", GUILayout.Height(28)))
                BakeClip(_bakeClip, frameCount);
        }

        /// <summary>
        /// Samples <paramref name="clip"/> frame-by-frame using Unity's <see cref="AnimationMode"/>,
        /// captures bone transforms at each frame, preserves any existing box data, and writes every
        /// resulting <see cref="Pose"/> into the sheet starting at <see cref="_blockIndex"/>.
        /// Restores the skeleton to <see cref="_defaultPose"/> in the finally block regardless of errors.
        /// </summary>
        private void BakeClip(AnimationClip clip, int frameCount)
        {
            AnimationMode.StartAnimationMode();

            try
            {
                for (int frame = 0; frame < frameCount; frame++)
                {
                    // Convert the integer frame index into the time value SampleAnimation
                    // needs. This is purely an API detail — conceptually we are just
                    // stepping forward one frame at a time.
                    float t = frame / clip.frameRate;

                    AnimationMode.SampleAnimationClip(_behaviour.visualRoot, clip, t);

                    var boneData = CaptureBones();

                    var pose = new Pose
                    {
                        Bones = boneData
                    };

                    if (_sheet.TryGetPose((uint)(_blockIndex + frame / BlockSize), (uint)(frame % BlockSize),
                            out var existingPose))
                    {
                        pose.Hurtboxes = existingPose.Hurtboxes;
                        pose.Hitboxes = existingPose.Hitboxes;
                    }
                    else
                    {
                        pose.Hurtboxes = Array.Empty<MinMaxAABB>();
                        pose.Hitboxes = Array.Empty<MinMaxAABB>();
                    }

                    uint collectionId = (uint)(_blockIndex + frame / BlockSize);
                    uint poseId = (uint)(frame % BlockSize);
                    _sheet.EditorAddOrReplace(collectionId, poseId, pose);
                }

                AnimationMode.StopAnimationMode();


                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"[PoseBaker] Baked {frameCount} frames from '{clip.name}' " +
                    $"into '{_sheet.name}', starting at block {_blockIndex}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PoseBaker] Bake failed: {e}");
            }
            finally
            {
                PoseAnimator.ApplyPose(_boneMap, _defaultPose);
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Clears and repopulates <see cref="_clipsToBake"/> from all <see cref="AnimationClip"/>
        /// sub-assets embedded in <see cref="_fbxAsset"/>, excluding Unity's internal preview clips.
        /// </summary>
        private void RefreshClips()
        {
            _clipsToBake.Clear();
            if (_fbxAsset == null) return;

            string path = AssetDatabase.GetAssetPath(_fbxAsset);

            UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in allAssets)
            {
                if (asset is AnimationClip clip)
                {
                    // Skip Unity's internal __preview__ clips
                    if (!clip.name.StartsWith("__preview__"))
                        _clipsToBake.Add(clip);
                }
            }
        }

        // ── ID section ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Renders the Pose ID panel: optional block-index field, optional pose-offset slider, and
        /// optional Save to Default Pose button. Parameters let the Bake tab reuse this section with
        /// only the block selector visible.
        /// </summary>
        private void DrawIdSection(bool drawBlockSelector = true, bool drawOffsetSelector = true,
            bool drawDefaultSetter = true)
        {
            EditorGUILayout.LabelField("Pose ID", EditorStyles.boldLabel);

            if (drawBlockSelector)
            {
                EditorGUI.BeginChangeCheck();
                int newBlock = EditorGUILayout.IntField(
                    new GUIContent("Move Block", "Each block holds 100 poses. Block 0 = IDs 0–99, …"),
                    _blockIndex);
                if (EditorGUI.EndChangeCheck())
                {
                    _blockIndex = Mathf.Max(0, newBlock);
                    PreviewPose((uint)_blockIndex, (uint)_poseOffset);
                }

                EditorGUILayout.LabelField(
                    $"Block {_blockIndex}  covers IDs  {_blockIndex * BlockSize}  –  {_blockIndex * BlockSize + BlockSize - 1}",
                    _subtleLabel);

                EditorGUILayout.Space(2);
            }

            if (drawOffsetSelector)
            {
                EditorGUI.BeginChangeCheck();
                _poseOffset = EditorGUILayout.IntSlider(
                    new GUIContent("Pose Offset", "Index within the block (0–99)."),
                    _poseOffset, 0, 99);
                if (EditorGUI.EndChangeCheck()) PreviewPose((uint)_blockIndex, (uint)_poseOffset);

                EditorGUILayout.Space(2);

                bool exists = _sheet != null && _sheet.EditorHasId((uint)_blockIndex, (uint)_poseOffset);
                EditorGUILayout.LabelField(
                    exists ? $"ID:  {EffectiveId}   ⚠ already exists — will overwrite" : $"ID:  {EffectiveId}",
                    exists ? _warningStyle : _subtleLabel);
            }

            if (drawDefaultSetter)
            {
                if (!_sheet.HasDefaultPose())
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox(
                        "No default pose found!",
                        MessageType.Warning);
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Save to Default Pose", GUILayout.Height(28)))
                {
                    _defaultPose = CapturePose();
                    _sheet.SetDefaultPose(_defaultPose);
                    Debug.Log("[PoseBaker] Updated default pose to current character pose and cleared all boxes.");
                    RemoveBoxes();
                }
            }
        }

        // ── Box editor section ─────────────────────────────────────────────────────

        /// <summary>
        /// Renders the Collision Boxes panel: hurtbox and hitbox lists with add/remove controls,
        /// and a center/size field for the selected box.
        /// </summary>
        private void DrawBoxEditorSection()
        {
            EditorGUILayout.LabelField("Collision Boxes", EditorStyles.boldLabel);

            DrawBoxList("Hurtboxes", _hurtboxes, BoxList.Hurtbox, new Color(0.25f, 0.85f, 0.25f));
            EditorGUILayout.Space(4);
            DrawBoxList("Hitboxes", _hitboxes, BoxList.Hitbox, new Color(0.85f, 0.25f, 0.25f));

            if (!HasSelection) return;

            var list = ActiveList();
            if (_selectedIndex >= list.Count)
            {
                ClearSelection();
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Selected: {_selectedList} [{_selectedIndex}]", EditorStyles.boldLabel);

            var box = list[_selectedIndex];
            var center = box.Center;
            var size = box.Extents;

            EditorGUI.BeginChangeCheck();
            var newCenter = (float3)EditorGUILayout.Vector3Field("Center", center);
            var newSize = (float3)EditorGUILayout.Vector3Field("Size", size);

            newSize = math.max(newSize, new float3(0.01f));

            if (EditorGUI.EndChangeCheck())
            {
                var half = newSize * 0.5f;
                list[_selectedIndex] = new MinMaxAABB { Min = newCenter - half, Max = newCenter + half };
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Renders a labelled, scrollable list of AABB entries with per-row select and delete controls.
        /// The label's last two characters are assumed to be "es" (e.g. "Hurtboxes") and are stripped
        /// to produce the singular row label ("Hurtbox").
        /// </summary>
        private void DrawBoxList(string label, List<MinMaxAABB> boxes, BoxList type, Color accentColor)
        {
            EditorGUILayout.BeginHorizontal();

            var prev = GUI.color;
            GUI.color = accentColor;
            EditorGUILayout.LabelField($"● {label}", GUILayout.Width(90));
            GUI.color = prev;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Add", GUILayout.Width(54), GUILayout.Height(17)))
            {
                boxes.Add(new MinMaxAABB
                {
                    Min = new float3(-0.25f, 0.00f, -0.10f),
                    Max = new float3(0.25f, 0.50f, 0.10f),
                });
                SelectBox(type, boxes.Count - 1);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            if (boxes.Count == 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("(none)", _subtleLabel);
                EditorGUI.indentLevel--;
                return;
            }

            string singular = label[..^2]; // "Hurtboxes" → "Hurtbox", "Hitboxes" → "Hitbox"

            EditorGUI.indentLevel++;
            for (int i = 0; i < boxes.Count; i++)
            {
                bool isSelected = _selectedList == type && _selectedIndex == i;
                bool deleted = false;

                EditorGUILayout.BeginHorizontal(isSelected ? _rowSelected : _rowNormal);

                if (GUILayout.Button($"{singular} {i}", isSelected ? EditorStyles.boldLabel : EditorStyles.label))
                    SelectBox(type, i);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(16)))
                    deleted = true;

                EditorGUILayout.EndHorizontal();

                if (deleted)
                {
                    boxes.RemoveAt(i);
                    if (isSelected) ClearSelection();
                    else if (_selectedList == type && _selectedIndex > i) _selectedIndex--;
                    SceneView.RepaintAll();
                    break;
                }
            }

            EditorGUI.indentLevel--;
        }

        // ── Save section ───────────────────────────────────────────────────────────

        /// <summary>
        /// Renders the Box Edit Actions toolbar: Select Pose (copy boxes from any pose via
        /// <see cref="SliderPromptWindow"/>), navigate to adjacent poses, Save Boxes, and Delete.
        /// </summary>
        private void DrawBoxEditSection()
        {
            GUILayout.Label("Box Edit Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Select Pose", GUILayout.ExpandWidth(false), GUILayout.Height(28)))
            {
                SliderPromptWindow.ShowWindow((int)EffectiveId, 0, 99999, newValue =>
                {
                    var prevBlock = _blockIndex;
                    var prevOffset = _poseOffset;
                    _blockIndex = newValue / BlockSize;
                    _poseOffset = newValue % BlockSize;
                    CopyBoxes((uint)_blockIndex, (uint)_poseOffset);
                    _blockIndex = prevBlock;
                    _poseOffset = prevOffset;
                    Repaint();
                });
            }

            if (GUILayout.Button("<-", GUILayout.ExpandWidth(false), GUILayout.Height(28)))
                CopyBoxes((uint)_blockIndex, ((uint)_poseOffset + BlockSize - 1) % BlockSize);

            if (GUILayout.Button($"Save Boxes →  ID {EffectiveId}", GUILayout.Height(28)))
            {
                _sheet.EditorAddOrReplace((uint)_blockIndex, (uint)_poseOffset, CapturePose());
                AssetDatabase.SaveAssets();
                Debug.Log($"[PoseBaker] Saved boxes on pose ID {EffectiveId} on '{_sheet.name}'.");
            }

            if (GUILayout.Button("->", GUILayout.ExpandWidth(false), GUILayout.Height(28)))
                CopyBoxes((uint)_blockIndex, ((uint)_poseOffset + 1) % BlockSize);

            if (GUILayout.Button("D", GUILayout.ExpandWidth(false), GUILayout.Height(28)))
            {
                RemoveBoxes();
            }

            EditorGUILayout.EndHorizontal();
        }

        // ── Core ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies the pose at (<paramref name="collectionId"/>, <paramref name="poseId"/>) to the
        /// skeleton via <see cref="PoseAnimator.ApplyPose"/> and loads its box arrays into
        /// <see cref="_hurtboxes"/> and <see cref="_hitboxes"/>. Falls back to the default pose when
        /// the ID does not exist. Clears the current selection and repaints the scene.
        /// </summary>
        private void PreviewPose(uint collectionId, uint poseId)
        {
            _sheet.TryGetPose(collectionId, poseId, out var pose);

            PoseAnimator.ApplyPose(_boneMap, pose);
            _hurtboxes = new List<MinMaxAABB>(pose.Hurtboxes);
            _hitboxes = new List<MinMaxAABB>(pose.Hitboxes);


            ClearSelection();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Replaces the live box lists with those from the pose at (<paramref name="collectionId"/>,
        /// <paramref name="poseId"/>), or clears them when the pose does not exist. Does not change
        /// the skeleton transform, so it can be used to transplant boxes between poses.
        /// </summary>
        private void CopyBoxes(uint collectionId, uint poseId)
        {
            if (_sheet.TryGetPose(collectionId, poseId, out var pose))
            {
                _hurtboxes = new List<MinMaxAABB>(pose.Hurtboxes);
                _hitboxes = new List<MinMaxAABB>(pose.Hitboxes);
            }
            else
            {
                _hurtboxes.Clear();
                _hitboxes.Clear();
            }

            ClearSelection();
            SceneView.RepaintAll();
        }

        /// <summary>Clears all live hurtbox and hitbox volumes and resets the selection state.</summary>
        private void RemoveBoxes()
        {
            _hurtboxes.Clear();
            _hitboxes.Clear();

            ClearSelection();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Captures the current bone transforms plus the live hurtbox/hitbox lists.
        /// Used when manually saving a single pose in the Data Editor tab.
        /// </summary>
        private Pose CapturePose()
        {
            return new Pose
            {
                Bones = SnapshotBones(),
                Hurtboxes = _hurtboxes.ToArray(),
                Hitboxes = _hitboxes.ToArray(),
            };
        }

        /// <summary>
        /// Captures bone transforms only, with empty box arrays.
        /// Used during baking — box data is not encoded in animation clips and
        /// must be authored manually afterwards in the Data Editor tab.
        /// </summary>
        private BoneData[] CaptureBones()
        {
            return SnapshotBones();
        }

        /// <summary>Reads the current local transform of every bone in <see cref="_boneMap"/> into a new <see cref="BoneData"/> array.</summary>
        private BoneData[] SnapshotBones()
        {
            var bones = new BoneData[_boneMap.Count];
            int i = 0;
            foreach (var kvp in _boneMap)
            {
                bones[i++] = new BoneData
                {
                    Name = kvp.Key,
                    LocalPosition = kvp.Value.localPosition,
                    LocalRotation = kvp.Value.localRotation,
                    LocalScale = kvp.Value.localScale,
                };
            }

            return bones;
        }

        // ── Selection helpers ──────────────────────────────────────────────────────

        /// <summary>Returns the live box list that owns the current selection.</summary>
        private List<MinMaxAABB> ActiveList() =>
            _selectedList == BoxList.Hurtbox ? _hurtboxes : _hitboxes;

        /// <summary>Sets the active selection to <paramref name="index"/> in <paramref name="type"/> and requests a repaint.</summary>
        private void SelectBox(BoxList type, int index)
        {
            _selectedList = type;
            _selectedIndex = index;
            Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>Clears the active selection without affecting the box lists.</summary>
        private void ClearSelection()
        {
            _selectedList = BoxList.None;
            _selectedIndex = -1;
        }

        // ── Validation ─────────────────────────────────────────────────────────────

        /// <summary>Returns true when all required references are bound; draws an info box and returns false otherwise.</summary>
        private bool ValidateInputs()
        {
            if (!_character || _sheet == null || !_poseAnimator || _boneMap == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a scene character with a CombatantBehaviour to continue.",
                    MessageType.Info);
                return false;
            }

            return true;
        }

        // ── Styles ─────────────────────────────────────────────────────────────────

        /// <summary>Lazily initialises IMGUI style objects; no-ops after the first call.</summary>
        private void InitStyles()
        {
            if (_warningStyle != null) return;

            _warningStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.6f, 0.1f) },
            };
            _subtleLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.65f, 0.65f, 0.65f) },
            };
            _rowSelected = new GUIStyle(EditorStyles.helpBox);
            _rowNormal = GUIStyle.none;
        }
    }
}
#endif