using Reflex.Attributes;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.HitSystem;
using Systems.Core;
using UnityEngine;

namespace Systems.Combat.Camera
{
    /// <summary>
    /// Fighting-game camera with target-group framing, stage bounds, dynamic walls,
    /// and Perlin-noise screen shake.
    ///
    /// ── Tick / Update split ───────────────────────────────────────────────────────
    ///   LogicTick  → walls repositioned BEFORE KCC runs (game-tick time).
    ///   LateUpdate → camera smoothed and shake applied AFTER everything moves
    ///                (real time, unscaled — shake is never frozen by hitstop).
    ///
    /// ── Shake ────────────────────────────────────────────────────────────────────
    ///   Call Shake(strength, duration) from anywhere.
    ///   The camera subscribes to CombatManager.OnHitConfirmed and automatically
    ///   picks a shake profile from the per-HitLevel inspector array.
    ///   Because shake runs in LateUpdate with Time.unscaledDeltaTime it is
    ///   completely immune to hitstop — hitstop only skips game ticks.
    ///
    /// ── Override API ─────────────────────────────────────────────────────────────
    ///   TakeOver() — freeze follow logic (supers, win poses, cutscenes).
    ///   Release()  — resume smooth tracking, no velocity lurch.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CombatCamera : MonoBehaviour, ITickable<CombatManager>
    {
        // ── Dependencies ──────────────────────────────────────────────────────────

        [Inject] private readonly CombatManager _combatManager;

        [field: SerializeField] public UnityEngine.Camera Camera { get; private set; }

        // ── Inspector: fight plane ────────────────────────────────────────────────

        [Header("Fight Plane")]
        [Tooltip("World-space Z where the characters move. " +
                 "The camera will be placed at fightPlaneZ − distance, looking toward +Z.")]
        [SerializeField]
        private float _fightPlaneZ = -5f;

        [Tooltip("Fixed world-space Y the camera is held at.")] [SerializeField]
        private float _cameraY = 3f;

        // ── Inspector: zoom ───────────────────────────────────────────────────────

        [Header("Zoom — distance from fight plane along Z")] [SerializeField]
        private float _minDistance = 6f;

        [SerializeField] private float _maxDistance = 16f;

        [Tooltip("Extra horizontal margin added on each side of the outermost character.")] [SerializeField]
        private float _horizontalPadding = 1.5f;

        [SerializeField] private float _positionSmoothTime = 0.08f;
        [SerializeField] private float _zoomSmoothTime = 0.12f;

        // ── Inspector: stage bounds ───────────────────────────────────────────────

        [Header("Stage Bounds")] [SerializeField]
        private float _stageLeft = -20f;

        [SerializeField] private float _stageRight = 20f;

        // ── Inspector: camera walls ───────────────────────────────────────────────

        [Header("Camera Walls")] [SerializeField]
        private string _wallLayer = "Default";

        [SerializeField] private float _wallInset = 0.3f;
        [SerializeField] private float _wallThickness = 2f;
        [SerializeField] private float _wallHeight = 40f;
        [SerializeField] private float _wallDepth = 10f;

        // ── Inspector: shake ──────────────────────────────────────────────────────

        [System.Serializable]
        private struct HitShakeProfile
        {
            [Tooltip("Max displacement in world units at the start of the shake.")]
            public float Strength;

            [Tooltip("Shake duration in real-time seconds (unaffected by hitstop).")]
            public float Duration;
        }

        [Header("Screen Shake")]
        [Tooltip("One entry per EHitLevel (index 0 = Level One … 4 = Level Five). " +
                 "Shake is triggered automatically when CombatManager fires OnHitConfirmed.")]
        [SerializeField]
        private HitShakeProfile[] _shakeProfiles = new HitShakeProfile[]
        {
            new() { Strength = 0.04f, Duration = 0.10f }, // Level One
            new() { Strength = 0.08f, Duration = 0.14f }, // Level Two
            new() { Strength = 0.13f, Duration = 0.18f }, // Level Three
            new() { Strength = 0.20f, Duration = 0.22f }, // Level Four
            new() { Strength = 0.30f, Duration = 0.28f }, // Level Five
        };

        [Tooltip("Perlin noise sampling speed. Higher = more chaotic shake.")] [SerializeField]
        private float _shakeFrequency = 25f;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private CombatantBehaviour _combatant0;
        private CombatantBehaviour _combatant1;

        private bool _active;
        private bool _overridden;

        private Vector3 _camPosVelocity;
        private float _camDistVelocity;
        private float _currentDistance;

        private BoxCollider _leftWall;
        private BoxCollider _rightWall;

        // Shake state — all in real time (unscaled)
        private float _shakeStrength;
        private float _shakeDuration;
        private float _shakeTimeRemaining;
        private float _shakeElapsed; // monotonically increasing seed for Perlin

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Start()
        {
            if (!Camera) Camera = GetComponent<UnityEngine.Camera>();
            _currentDistance = _minDistance;
            SpawnWalls();
            _combatManager.OnCombatStarted += OnCombatStarted;
            _combatManager.OnHitResolved += OnHitResolved;
        }

        private void OnDestroy()
        {
            if (_combatManager != null)
            {
                _combatManager.OnCombatStarted -= OnCombatStarted;
                _combatManager.OnHitResolved -= OnHitResolved;
            }

            if (_leftWall) Destroy(_leftWall.gameObject);
            if (_rightWall) Destroy(_rightWall.gameObject);
        }

        private void OnCombatStarted(CombatantBehaviour c0, CombatantBehaviour c1)
        {
            _combatManager.OnCombatEnded += OnCombatEnded;
            _combatant0 = c0;
            _combatant1 = c1;
            _combatManager.RegisterTickable(this);
            _active = true;
            SnapInstant();
        }
        
        private void OnCombatEnded()
        {
            _combatManager.OnCombatEnded -= OnCombatEnded;
            _combatManager.UnregisterTickable(this);
            _active = false;
        }

        private void OnHitResolved(HitResult result)
        {
            if (result.Resolution == EHitResolution.Blocked) return;

            int levelIndex = (int)result.HitData.Level; // EHitLevel.One = 0 … Five = 4
            if (levelIndex >= 0 && levelIndex < _shakeProfiles.Length)
            {
                var p = _shakeProfiles[levelIndex];
                Shake(p.Strength, p.Duration);
            }
        }

        // ── ITickable<CombatManager> ──────────────────────────────────────────────

        /// <summary>Repositions walls before KCC simulates.</summary>
        public void LogicTick()
        {
            if (!_active || _overridden) return;
            RepositionWalls();
        }

        // ── Unity update ──────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_active || _overridden) return;
            SmoothCamera();
            ApplyShake(); // additive, after smooth damp — never frozen by hitstop
        }

        // ── Public shake API ──────────────────────────────────────────────────────

        /// <summary>
        /// Triggers a screen shake. Safe to call from anywhere at any time.
        /// Overlapping calls keep the strongest strength and the longest remaining time
        /// so hits never cancel each other's shake out.
        /// </summary>
        /// <param name="strength">Maximum displacement in world units.</param>
        /// <param name="duration">Duration in real-time seconds (unaffected by hitstop).</param>
        public void Shake(float strength, float duration)
        {
            // Preserve the worse (more intense) of the two overlapping shakes.
            _shakeStrength = Mathf.Max(_shakeStrength, strength);
            _shakeDuration = duration;
            _shakeTimeRemaining = Mathf.Max(_shakeTimeRemaining, duration);
        }

        // ── Override API ──────────────────────────────────────────────────────────

        public void TakeOver() => _overridden = true;

        public void Release()
        {
            _overridden = false;
            _camPosVelocity = Vector3.zero;
            _camDistVelocity = 0f;
        }

        // ── Camera ────────────────────────────────────────────────────────────────

        private void SmoothCamera()
        {
            ComputeTargetFrame(out float targetX, out float targetDist);

            _currentDistance = Mathf.SmoothDamp(
                _currentDistance, targetDist, ref _camDistVelocity, _zoomSmoothTime);

            // Camera sits in -Z of the fight plane, looking toward +Z
            float targetZ = _fightPlaneZ - _currentDistance;
            var targetPos = new Vector3(targetX, _cameraY, targetZ);

            Camera.transform.position = Vector3.SmoothDamp(
                Camera.transform.position, targetPos, ref _camPosVelocity, _positionSmoothTime);

            Camera.transform.LookAt(
                new Vector3(Camera.transform.position.x, _cameraY, _fightPlaneZ));
        }

        private void SnapInstant()
        {
            ComputeTargetFrame(out float targetX, out float targetDist);
            _currentDistance = targetDist;
            Camera.transform.position =
                new Vector3(targetX, _cameraY, _fightPlaneZ - _currentDistance);
            Camera.transform.LookAt(new Vector3(targetX, _cameraY, _fightPlaneZ));
            _camPosVelocity = Vector3.zero;
            _camDistVelocity = 0f;
            RepositionWalls();
        }

        /// <summary>
        /// Core framing — equivalent to a Cinemachine Target Group shot.
        /// Computes the Z distance needed to fit both characters in frame,
        /// then clamps the midpoint X so the frustum never exits the stage.
        /// </summary>
        private void ComputeTargetFrame(out float targetX, out float targetDist)
        {
            float x0 = _combatant0.transform.position.x;
            float x1 = _combatant1.transform.position.x;

            float leftEdge = Mathf.Min(x0, x1) - _horizontalPadding;
            float rightEdge = Mathf.Max(x0, x1) + _horizontalPadding;
            float halfSpan = (rightEdge - leftEdge) * 0.5f;
            float rawMidX = (leftEdge + rightEdge) * 0.5f;

            // distance = halfSpan / (tan(halfFOV) × aspect)
            float tanHalfFov = Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float required = halfSpan / (tanHalfFov * Camera.aspect);
            targetDist = Mathf.Clamp(required, _minDistance, _maxDistance);

            float halfFrustum = tanHalfFov * Camera.aspect * targetDist;
            float clampLo = _stageLeft + halfFrustum;
            float clampHi = _stageRight - halfFrustum;

            targetX = clampLo <= clampHi
                ? Mathf.Clamp(rawMidX, clampLo, clampHi)
                : (_stageLeft + _stageRight) * 0.5f;
        }

        // ── Shake ─────────────────────────────────────────────────────────────────

        private void ApplyShake()
        {
            if (_shakeTimeRemaining <= 0f) return;

            _shakeTimeRemaining -= Time.unscaledDeltaTime;
            _shakeElapsed += Time.unscaledDeltaTime;

            // Linear decay: full strength at start, zero at end
            float t = Mathf.Clamp01(_shakeTimeRemaining / _shakeDuration);
            float currentStrength = _shakeStrength * t;

            // Two independent Perlin channels → smooth but unpredictable XY offset
            float seed = _shakeElapsed * _shakeFrequency;
            float dx = (Mathf.PerlinNoise(seed, 0f) - 0.5f) * 2f * currentStrength;
            float dy = (Mathf.PerlinNoise(0f, seed) - 0.5f) * 2f * currentStrength;

            // Additive world-space offset — does not disturb smooth-damp state
            Camera.transform.position += new Vector3(dx, dy, 0f);

            if (_shakeTimeRemaining <= 0f)
                _shakeStrength = 0f; // reset peak so next Shake() starts fresh
        }

        // ── Camera walls ──────────────────────────────────────────────────────────

        private void SpawnWalls()
        {
            int layer = LayerMask.NameToLayer(_wallLayer);
            _leftWall = CreateWall("CameraWall_Left", layer);
            _rightWall = CreateWall("CameraWall_Right", layer);
        }

        private BoxCollider CreateWall(string name, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(_wallThickness, _wallHeight, _wallDepth);
            return col;
        }

        private void RepositionWalls()
        {
            float camX = Camera.transform.position.x;
            float tanHalfFov = Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfFrustum = tanHalfFov * Camera.aspect * _maxDistance;

            float idealLeft = camX - halfFrustum + _wallInset;
            float idealRight = camX + halfFrustum - _wallInset;

            float finalLeft = Mathf.Max(idealLeft, _stageLeft);
            float finalRight = Mathf.Min(idealRight, _stageRight);

            _leftWall.transform.position =
                new Vector3(finalLeft - _wallThickness * 0.5f, 0f, _fightPlaneZ);
            _rightWall.transform.position =
                new Vector3(finalRight + _wallThickness * 0.5f, 0f, _fightPlaneZ);
        }

        // ── Editor gizmos ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Camera) Camera = GetComponent<UnityEngine.Camera>();
            if (!Camera) return;

            float gizmoMidX = (_stageLeft + _stageRight) * 0.5f;
            float tanHalfFov = Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float lineH = 4f;

            Gizmos.color = Color.white;
            Gizmos.DrawLine(new Vector3(_stageLeft, _cameraY, _fightPlaneZ),
                new Vector3(_stageRight, _cameraY, _fightPlaneZ));

            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(_stageLeft, _cameraY - lineH, _fightPlaneZ),
                new Vector3(_stageLeft, _cameraY + lineH, _fightPlaneZ));
            Gizmos.DrawLine(new Vector3(_stageRight, _cameraY - lineH, _fightPlaneZ),
                new Vector3(_stageRight, _cameraY + lineH, _fightPlaneZ));

            float halfMin = tanHalfFov * Camera.aspect * _minDistance;
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
            Gizmos.DrawLine(new Vector3(gizmoMidX - halfMin, _cameraY, _fightPlaneZ),
                new Vector3(gizmoMidX + halfMin, _cameraY, _fightPlaneZ));

            float halfMax = tanHalfFov * Camera.aspect * _maxDistance;
            Gizmos.color = new Color(1f, 0.85f, 0f, 0.8f);
            Gizmos.DrawLine(new Vector3(gizmoMidX - halfMax, _cameraY, _fightPlaneZ),
                new Vector3(gizmoMidX + halfMax, _cameraY, _fightPlaneZ));

            Gizmos.color = Color.red;
            float wallL = Mathf.Max(gizmoMidX - halfMax + _wallInset, _stageLeft);
            float wallR = Mathf.Min(gizmoMidX + halfMax - _wallInset, _stageRight);
            Gizmos.DrawLine(new Vector3(wallL, _cameraY - lineH, _fightPlaneZ),
                new Vector3(wallL, _cameraY + lineH, _fightPlaneZ));
            Gizmos.DrawLine(new Vector3(wallR, _cameraY - lineH, _fightPlaneZ),
                new Vector3(wallR, _cameraY + lineH, _fightPlaneZ));

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(new Vector3(gizmoMidX, _cameraY, _fightPlaneZ - _minDistance), 0.2f);
            Gizmos.DrawSphere(new Vector3(gizmoMidX, _cameraY, _fightPlaneZ - _maxDistance), 0.2f);

            Gizmos.color = new Color(1f, 0.85f, 0f, 0.3f);
            var camMaxPos = new Vector3(gizmoMidX, _cameraY, _fightPlaneZ - _maxDistance);
            Gizmos.DrawLine(camMaxPos, new Vector3(gizmoMidX - halfMax, _cameraY, _fightPlaneZ));
            Gizmos.DrawLine(camMaxPos, new Vector3(gizmoMidX + halfMax, _cameraY, _fightPlaneZ));
        }
#endif
    }
}