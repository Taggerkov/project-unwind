using Systems.Combat.Combatant.Data;
using Systems.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.ScriptableAnimationEditor
{
    public class ScriptableAnimationEditorWindow : EditorWindow
    {
        private PreviewRenderUtility _previewRenderUtility;

        [SerializeField] private CombatantDataSO previewedCombatantSo;

        private GameObject _previewInstance;


        [SerializeField] private Vector2 orbitAngles = new(45, -45);
        [SerializeField] private float orbitDistance = 5.0f;

        [MenuItem("Unwind/Scriptable Animation Editor")]
        public static void ShowWindow()
        {
            ScriptableAnimationEditorWindow window = GetWindow<ScriptableAnimationEditorWindow>();
            window.titleContent = new GUIContent("Scriptable Animation Editor");
        }

        private void OnEnable()
        {
            SetupPreview();
        }

        private void OnDisable()
        {
            CleanupPreview();
        }

        public void CreateGUI()
        {
            // Ensure preview is ready on load
            SetupPreview();

            VisualElement root = rootVisualElement;

            // --- Header Section ---
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingBottom = 5 } };
    
            // 1. The SO Selector
            var objectField = new UnityEditor.UIElements.ObjectField("Combatant")
            {
                objectType = typeof(CombatantDataSO),
                value = previewedCombatantSo,
                allowSceneObjects = false,
                style = { flexGrow = 1 }
            };

            objectField.RegisterValueChangedCallback(evt =>
            {
                previewedCombatantSo = evt.newValue as CombatantDataSO;
                SetupPreview();
            });

            // 2. The Refresh Button
            var refreshButton = new Button(SetupPreview) 
            { 
                text = "Refresh",
                style = { width = 60 }
            };

            header.Add(objectField);
            header.Add(refreshButton);
            root.Add(header);

            // --- Preview Section ---
            IMGUIContainer previewContainer = new IMGUIContainer(OnPreviewGUI)
            {
                style = { flexGrow = 1 }
            };
            root.Add(previewContainer);
        }

        private void OnPreviewGUI()
        {
            if (_previewRenderUtility == null) return;

            Rect r = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            HandleOrbit(r);

            if (Event.current.type != EventType.Repaint) return;


            // 1. Calculate the rotation
            Quaternion rotation = Quaternion.Euler(orbitAngles.x, orbitAngles.y, 0);

            // 2. Calculate the position (Rotate a backward vector and multiply by distance)
            Vector3 pos = rotation * new Vector3(0, 0, -orbitDistance);

            // 3. Apply to the PreviewRenderUtility camera
            _previewRenderUtility.camera.transform.rotation = rotation;
            _previewRenderUtility.camera.transform.position = pos;

            _previewRenderUtility.BeginPreview(r, GUIStyle.none);

            _previewRenderUtility.camera.Render();

            var previewTexture = _previewRenderUtility.EndPreview();

            GUI.DrawTexture(r, previewTexture, ScaleMode.StretchToFill);
        }

        private void CleanupPreview()
        {
            Debug.Log("Cleaning up preview for object: " + (previewedCombatantSo ? previewedCombatantSo.name : "None"));

            if (_previewInstance)
            {
                DestroyImmediate(_previewInstance);
                _previewInstance = null;
            }

            _previewRenderUtility?.Cleanup();
            _previewRenderUtility = null;
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void SetupPreview()
        {
            CleanupPreview();

            Debug.Log("Setting up new preview for object: " + (previewedCombatantSo ? previewedCombatantSo.name : "None"));

            _previewRenderUtility = new PreviewRenderUtility
            {
                cameraFieldOfView = 60f,
                camera =
                {
                    farClipPlane = 1000,
                    nearClipPlane = 0.1f,
                    cameraType = CameraType.Preview,
                    backgroundColor = Color.yellow,
                    transform =
                    {
                        position = new Vector3(0, 0, -5)
                    }
                }
            };
            _previewRenderUtility.camera.transform.LookAt(Vector3.zero);

            if (previewedCombatantSo)
            {
                // _previewInstance = Instantiate(previewedCombatantSo.combatantPrefab);
                _previewRenderUtility.AddSingleGO(_previewInstance);

                _previewInstance.transform.position = Vector3.zero;
            }
        }

        private void HandleOrbit(Rect rect)
        {
            Event e = Event.current;

            // Use the right mouse button or Alt+Left Click (standard Unity shortcut)
            if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition))
            {
                // Sensitivity usually feels best around 0.5f
                orbitAngles.y += e.delta.x * 0.5f;
                orbitAngles.x += e.delta.y * 0.5f;

                // Clamp pitch to avoid flipping the camera upside down
                orbitAngles.x = Mathf.Clamp(orbitAngles.x, -89f, 89f);

                e.Use();
                Repaint();
            }
        }
    }
}