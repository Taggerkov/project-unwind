using System.Collections.Generic;
using System.Linq;
using Systems.Combat.Combatant.Data;
using Systems.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace Editor.ScriptableAnimationEditor
{
    [CustomEditor(typeof(CameraSequenceTrack))]
    public class CameraTrackEditor : UnityEditor.Editor
    {
        public CombatantDataSO combatantDataSo;

        private DropdownField _sequenceSelector;
        private int _selectedSequenceIndex;
        private TextField _sequenceName;
        private Button _bakeButton;

        // Keys for saving state persistently across selections based on the specific track instance
        private string SoGuidKey => $"CameraTrack_{target.GetInstanceID()}_SO";
        private string SelectedIndexKey => $"CameraTrack_{target.GetInstanceID()}_Index";

        private bool IsNewSelection => _selectedSequenceIndex == 0;
        private int DataIndex => _selectedSequenceIndex - 1;

        public override VisualElement CreateInspectorGUI()
        {
            CameraSequenceTrack track = (CameraSequenceTrack)target;

            VisualElement root = new VisualElement();

            ObjectField objectField = new ObjectField("Combatant")
            {
                objectType = typeof(CombatantDataSO),
                allowSceneObjects = false
            };
            root.Add(objectField);

            _sequenceSelector = new DropdownField("Camera Sequence");
            root.Add(_sequenceSelector);

            _sequenceName = new TextField("Sequence Name");
            root.Add(_sequenceName);

            _bakeButton = new Button { text = "Bake" };
            root.Add(_bakeButton);

            // 1. Load persisted state
            LoadState(objectField);

            // 2. Register callbacks
            objectField.RegisterValueChangedCallback(OnCombatantDataFieldChanged);

            _sequenceSelector.RegisterValueChangedCallback(evt =>
            {
                _selectedSequenceIndex = _sequenceSelector.index;
                SessionState.SetInt(SelectedIndexKey, _selectedSequenceIndex);

                // Update the name field to match the newly selected sequence
                UpdateNameFieldFromSelection();
            });

            _bakeButton.clicked += () => BakeIntoCinematicCameraSequence(combatantDataSo, track);

            // 3. Initialize UI
            RefreshUI();

            return root;
        }

        private void UpdateNameFieldFromSelection()
        {
            if (combatantDataSo == null) return;

            if (IsNewSelection)
            {
                _sequenceName.value = "NewCameraSequence";
            }
            else if (combatantDataSo.cinematicCameraSequences != null &&
                     DataIndex < combatantDataSo.cinematicCameraSequences.Length)
            {
                _sequenceName.value = combatantDataSo.cinematicCameraSequences[DataIndex].sequenceName;
            }
        }

        private void LoadState(ObjectField objectField)
        {
            // Restore the ScriptableObject reference via its GUID
            string soGuid = SessionState.GetString(SoGuidKey, "");
            if (!string.IsNullOrEmpty(soGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(soGuid);
                combatantDataSo = AssetDatabase.LoadAssetAtPath<CombatantDataSO>(path);
                objectField.value = combatantDataSo;
            }

            // Restore the selected dropdown index
            _selectedSequenceIndex = SessionState.GetInt(SelectedIndexKey, 0);
        }

        private void OnCombatantDataFieldChanged(ChangeEvent<Object> evt)
        {
            combatantDataSo = evt.newValue as CombatantDataSO;

            // Save the new ScriptableObject reference to SessionState
            if (combatantDataSo != null)
            {
                string path = AssetDatabase.GetAssetPath(combatantDataSo);
                string guid = AssetDatabase.AssetPathToGUID(path);
                SessionState.SetString(SoGuidKey, guid);
            }
            else
            {
                SessionState.EraseString(SoGuidKey);
            }

            RefreshUI();
        }

        private void RefreshUI()
        {
            // We only hide the UI if the ScriptableObject itself is missing
            if (!combatantDataSo)
            {
                _sequenceSelector.style.display = DisplayStyle.None;
                _bakeButton.style.display = DisplayStyle.None;
                return;
            }

            _sequenceSelector.style.display = DisplayStyle.Flex;
            _bakeButton.style.display = DisplayStyle.Flex;

            // 1. Rebuild Choices
            var choices = new List<string> { " + Bake into New Sequence" };
            if (combatantDataSo.cinematicCameraSequences != null)
            {
                choices.AddRange(combatantDataSo.cinematicCameraSequences
                    .Select((s, i) => $"[{i}] {s.sequenceName}"));
            }

            _sequenceSelector.choices = choices;

            // 2. Clamp and Sync Index
            _selectedSequenceIndex = Mathf.Clamp(_selectedSequenceIndex, 0, choices.Count - 1);
            _sequenceSelector.index = _selectedSequenceIndex;

            // 3. Update the Name Field based on selection
            if (IsNewSelection)
            {
                // Don't overwrite the name field if the user is typing a new name, 
                // but maybe provide a default if it's empty
                if (string.IsNullOrEmpty(_sequenceName.value))
                    _sequenceName.value = "NewCameraSequence";
            }
            else
            {
                _sequenceName.value = combatantDataSo.cinematicCameraSequences[DataIndex].sequenceName;
            }
        }

        private void BakeIntoCinematicCameraSequence(CombatantDataSO so, CameraSequenceTrack track)
        {
            Undo.RecordObject(so, "Bake Cinematic Camera Sequence");

            var clips = track.GetClips().OrderBy(c => c.start).ToList();

            // 2. Map Keyframes and Inject frameIndex
            CameraKeyframe[] bakedKeyframes = new CameraKeyframe[clips.Count];
            for (int i = 0; i < clips.Count; i++)
            {
                var tClip = clips[i];
                var clipAsset = tClip.asset as CameraKeyframeClip;

                // Copy the data from the clip
                CameraKeyframe data = clipAsset!.keyframeData;

                // Calculate the frame index based on the clip's start time in Timeline
                data.frameIndex = Mathf.RoundToInt((float)tClip.start * 60f);

                bakedKeyframes[i] = data;
            }

            float duration = clips.Count > 0 ? (float)clips.Last().end : 0f;

            CinematicCameraSequence bakedSeq = new CinematicCameraSequence
            {
                sequenceName = _sequenceName.value,
                keyframes = bakedKeyframes,
                totalFrames = Mathf.RoundToInt(duration * 60f),
                sequenceAnchor = CameraAnchor.StageCenter
            };

            // 3. Save to array
            if (IsNewSelection)
            {
                ArrayUtility.Add(ref so.cinematicCameraSequences, bakedSeq);
                _selectedSequenceIndex = so.cinematicCameraSequences.Length; // Select the new item
                SessionState.SetInt(SelectedIndexKey, _selectedSequenceIndex);
            }
            else
            {
                so.cinematicCameraSequences[DataIndex] = bakedSeq;
            }

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssetIfDirty(so);
            RefreshUI();
        }
    }
}