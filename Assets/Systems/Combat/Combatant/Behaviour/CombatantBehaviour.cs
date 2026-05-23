using System;
using System.Collections.Generic;
using KinematicCharacterController;
using Reflex.Attributes;
using Systems.Audio;
using Systems.Combat.Combatant.Animation;
using Systems.Combat.Combatant.Controller;
using Systems.Combat.Combatant.StateMachine;
using Systems.Combat.HitSystem;
using Systems.Common;
using Systems.Core;
using Systems.Input;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace Systems.Combat.Combatant.Behaviour
{
    [RequireComponent(typeof(KinematicCharacterMotor))]
    [RequireComponent(typeof(PoseAnimator))]
    public class CombatantBehaviour : MonoBehaviour, ITickable<CombatManager>
    {
        [Inject] private readonly CombatManager _combatManager;
        [Inject] private readonly GameManager _gameManager;

        [field: SerializeField] public KinematicCharacterMotor Motor { get; private set; }
        [field: SerializeField] public PoseAnimator Animator { get; private set; }
        [SerializeField] public CombatantMoveSetDefinition combatantMoveSetDefinition;
        [SerializeField] public CombatantPoseSheet combatantPoseSheet;
        [SerializeField] public AudioSheet audioSheet;
        [SerializeField] public GameObject visualRoot;
        [SerializeField] public GameObject directionIndicatorRoot;

        private CombatantBehaviour _opponent;

        public MoveRunner Runner => _runner;
        public CombatantStateMachine StateMachine => _stateMachine;

        public CombatantCharacterController CharacterController;

        public GameManager GameManager => _gameManager;

        /// <summary>
        /// Live, per-instance character data (HP, character-specific counters/flags).
        /// Cloned from the move set definition's StatsTemplate at Awake so two
        /// combatants of the same type never share state.
        /// </summary>
        public CombatantStats Stats { get; private set; }

        public Action<EFacingDirection> OnFacingDirectionChanged;

        public event Action OnHitstunEnded;
        public event Action OnBlockstunEnded;


        private List<CombatantMove> _movementMoves;
        private List<CombatantMove> _normalMoves;
        private List<CombatantMove> _specialMoves;
        private List<CombatantMove> _overdriveMoves;

        private CombatantMove _cmnActStand;
        private CombatantMove _cmnActFWalk;
        private CombatantMove _cmnActBWalk;
        private CombatantMove _cmnStandToCrouch;
        private CombatantMove _cmnActCrouch;
        private CombatantMove _cmnCrouchToStand;
        private CombatantMove _cmnActJumpPre;
        private CombatantMove _cmnActJump;
        private CombatantMove _cmnActJumpLand;

        private CombatantMove _cmnActHitstun;
        private CombatantMove _cmnActBlockstun;

        /// <summary>
        /// Cache of move IDs for quick lookup during cancel resolution.
        /// </summary>
        private Dictionary<uint, CombatantMove> _moveIdCache = new();

        private Dictionary<CombatantMove, uint> _moveToIdCache = new();

        private Dictionary<string, uint> _moveNameToIdCache = new();
        private uint _nextMoveId = 1; // Start at 1 to avoid confusion with default(uint) == 0

        /// <summary>
        /// Full list of moves in an un-ordered collection. Used for querying.
        /// </summary>
        private List<CombatantMove> _runtimeMoveList;

        private List<CombatantMove> _runtimeCmnMoveList;

        /// <summary>
        /// Temporary list to hold all candidates to search for cancel attempts.
        /// Cleared and repopulated on every cancel pass — never persisted.
        /// </summary>
        private readonly List<CombatantMove> _cancelCandidates = new();


        private readonly MoveRunner _runner = new();
        private readonly CombatantStateMachine _stateMachine = new();

        private IInputProvider _inputProvider = new DummyInputProvider();

        public IInputProvider InputProvider
        {
            get => _inputProvider;
            set => _inputProvider = value ?? new DummyInputProvider();
        }

        private InputBuffer Buffer => InputProvider.Buffer;

        private void OnValidate()
        {
            if (!Motor) Motor = GetComponent<KinematicCharacterMotor>();
            if (!Animator) Animator = GetComponent<PoseAnimator>();
        }

        private void Awake()
        {
            CharacterController = new CombatantCharacterController();
            Motor.CharacterController = CharacterController;

            CharacterController.Motor = Motor;

            CharacterController.OnBecameAirborne += NotifyAirborne;
            CharacterController.OnLanded += NotifyLand;

            Animator.BuildBoneCache();

            _runner.Initialize(this);

            _runner.OnPoseChanged += OnPoseChanged;
            _runner.OnMoveStarted += OnMoveStarted;
            _runner.OnMoveFinished += OnMoveEnded;

            // Clone the stats template so this instance gets its own independent runtime state.
            if (combatantMoveSetDefinition && combatantMoveSetDefinition.StatsTemplate != null)
            {
                Stats = combatantMoveSetDefinition.StatsTemplate.Clone();
                Stats.Initialize();
            }
            else
            {
                Debug.LogWarning($"{name}: no StatsTemplate assigned in MoveSetDefinition. " +
                                 "HP and character-specific data will be unavailable.");
            }

            CharacterController.Stats = Stats;

            if (combatantMoveSetDefinition)
            {
                _runtimeMoveList = combatantMoveSetDefinition.InstantiateFor(this);

                _movementMoves = new List<CombatantMove>();
                _normalMoves = new List<CombatantMove>();
                _specialMoves = new List<CombatantMove>();
                _overdriveMoves = new List<CombatantMove>();

                foreach (var move in _runtimeMoveList)
                {
                    move.Initialize();
                    switch (move.Type)
                    {
                        // Filter moves into categories for easier access during cancel resolution.
                        case EMoveType.Movement:
                            _movementMoves.Add(move);
                            break;
                        case EMoveType.Normal:
                            _normalMoves.Add(move);
                            break;
                        case EMoveType.Special:
                            _specialMoves.Add(move);
                            break;
                        case EMoveType.Overdrive:
                            _overdriveMoves.Add(move);
                            break;
                    }
                }

                _cmnActStand = combatantMoveSetDefinition.InstantiateCmnActStand(this);
                _cmnActFWalk = combatantMoveSetDefinition.InstantiateCmnActFWalk(this);
                _cmnActBWalk = combatantMoveSetDefinition.InstantiateCmnActBWalk(this);
                _cmnStandToCrouch = combatantMoveSetDefinition.InstantiateCmnStandToCrouch(this);
                _cmnActCrouch = combatantMoveSetDefinition.InstantiateCmnActCrouch(this);
                _cmnCrouchToStand = combatantMoveSetDefinition.InstantiateCmnCrouchToStand(this);
                _cmnActJumpPre = combatantMoveSetDefinition.InstantiateCmnActJumpPre(this);
                _cmnActJump = combatantMoveSetDefinition.InstantiateCmnActJump(this);
                _cmnActJumpLand = combatantMoveSetDefinition.InstantiateCmnActJumpLand(this);

                _cmnActHitstun = combatantMoveSetDefinition.InstantiateCmnActHitstun(this);
                _cmnActBlockstun = combatantMoveSetDefinition.InstantiateCmnActBlockstun(this);

                _runtimeCmnMoveList = new List<CombatantMove>
                {
                    _cmnActStand,
                    _cmnActFWalk,
                    _cmnActBWalk,
                    _cmnStandToCrouch,
                    _cmnActCrouch,
                    _cmnCrouchToStand,
                    _cmnActJumpPre,
                    _cmnActJump,
                    _cmnActJumpLand,
                    _cmnActHitstun,
                    _cmnActBlockstun
                };


                _cmnActStand?.Initialize();
                _cmnActFWalk?.Initialize();
                _cmnActBWalk?.Initialize();
                _cmnStandToCrouch?.Initialize();
                _cmnActCrouch?.Initialize();
                _cmnCrouchToStand?.Initialize();
                _cmnActJumpPre?.Initialize();
                _cmnActJump?.Initialize();
                _cmnActJumpLand?.Initialize();

                _cmnActHitstun?.Initialize();
                _cmnActBlockstun?.Initialize();
            }
            else
            {
                _runtimeMoveList = new List<CombatantMove>();
            }

            _combatManager.OnCombatStarted += (combatant0, combatant1) =>
            {
                _opponent = combatant0 == this ? combatant1 : combatant0;
            };
        }

        public void ResetForNewRound()
        {
            Stats.Initialize(); //Reset HP and any character-specific stats to their initial values.
            _stateMachine.ResetForNewRound();
            _runner.ResetForNewRound();
        }

        private void OnPoseChanged(uint id, uint collectionId, uint poseId)
        {
            if (!combatantPoseSheet || !Animator) return;
            var foundPose = combatantPoseSheet.TryGetPose(collectionId, poseId, out var poseOrDefault);
            Animator.ApplyPose(poseOrDefault);


            if (!foundPose)
            {
                Debug.LogWarning($"Pose with ID {id} not found in CombatantPoseSheet for combatant {name}.");
            }
        }

        private void OnMoveStarted(CombatantMove move)
        {
            if (move == _cmnStandToCrouch) _stateMachine.SetPhysical(ECharacterState.Crouching);
            else if (move == _cmnCrouchToStand) _stateMachine.SetPhysical(ECharacterState.Standing);
        }

        private void OnMoveEnded(CombatantMove move)
        {
            if (move == _cmnActHitstun)
            {
                NotifyHitstunEnd();
            }

            if (move == _cmnActBlockstun)
            {
                NotifyBlockstunEnd();
            }
        }

        public void SetFacingDirection(EFacingDirection direction)
        {
            _stateMachine.SetFacingDirection(direction);
            CharacterController.FacingSign = direction == EFacingDirection.Right ? 1 : -1;

            directionIndicatorRoot.transform.localRotation = direction switch
            {
                EFacingDirection.Left => Quaternion.Euler(0, 180, 0),
                EFacingDirection.Right => Quaternion.identity,
                _ => directionIndicatorRoot.transform.localRotation
            };

            var scale = visualRoot.transform.localScale;
            scale.z = direction switch
            {
                EFacingDirection.Left => -Mathf.Abs(scale.z),
                EFacingDirection.Right => Mathf.Abs(scale.z),
                _ => scale.z
            };
            visualRoot.transform.localScale = scale;

            OnFacingDirectionChanged?.Invoke(direction);
        }

        public EFacingDirection GetNewFacingDirectionTowardsOpponent()
        {
            EFacingDirection newFacingDirection =
                _opponent.transform.position.x > transform.position.x ? EFacingDirection.Right : EFacingDirection.Left;
            return newFacingDirection;
        }

        public void LogicTick()
        {
            if (_stateMachine.IsAbleToTurn)
            {
                SetFacingDirection(GetNewFacingDirectionTowardsOpponent());
            }


            var view = new CharacterInputView(Buffer, _stateMachine.FacingDirection);


            if (_runner.IsRunning)
            {
                // ── Move system ────────────────────────────────────────────────────────────


                _runner.LogicTick(view.GetFrame(0));

                // After ticking, check for cancels (runner may still be running)
                if (_runner.IsRunning)
                    TryCancel(view, _runner.CurrentMove, _stateMachine.LastMove);
            }

            // Not an else — the tick above may have finished the move naturally,
            // or a cancel may have started a new one. If still not running, enter a move.
            if (!_runner.IsRunning)
            {
                _stateMachine.OnMoveEnded();
                TryEnterMove(view);
            }

            // ── Hurtbox / hitbox registration ─────────────────────────────────────────────

            MinMaxAABB[] worldHurtboxes = new MinMaxAABB[Animator.CurrentPose.Hurtboxes.Length];

            for (var i = 0; i < Animator.CurrentPose.Hurtboxes.Length; i++)
            {
                var hurtbox = Animator.CurrentPose.Hurtboxes[i];
                var worldBox = BoxToWorld(hurtbox);
                worldHurtboxes[i] = worldBox;
            }

            _combatManager.RegisterHurtboxes(this, worldHurtboxes);
            if (_stateMachine.CombatState == ECombatState.Active)
            {
                MinMaxAABB[] worldHitboxes = new MinMaxAABB[Animator.CurrentPose.Hitboxes.Length];
                for (var i = 0; i < Animator.CurrentPose.Hitboxes.Length; i++)
                {
                    var hitbox = Animator.CurrentPose.Hitboxes[i];
                    var worldBox = BoxToWorld(hitbox);
                    worldHitboxes[i] = worldBox;
                }

                _combatManager.RegisterHitboxes(this, _stateMachine.HitData, worldHitboxes);
            }
        }

        // ── Move entry ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called exclusively when no move is active. Registered combat moves are checked
        /// first; common moves (walk, stand, crouch…) are a fallback entered only if
        /// nothing in the combat pool matched.
        /// </summary>
        private void TryEnterMove(IInputView view)
        {
            // 1. Registered combat moves — full priority.
            _cancelCandidates.Clear();
            AddCombatCandidates(_cancelCandidates);
            var (result, move) = FindBestScoringMove(view, _cancelCandidates);

            if (result.IsMatch)
            {
                StartMove(move, result, view);
                return;
            }

            // 2. Common movement fallback — only reached when nothing above matched.
            _cancelCandidates.Clear();
            AddCommonCandidates(_cancelCandidates);
            var (commonResult, commonMove) = FindBestScoringMove(view, _cancelCandidates);

            if (commonResult.IsMatch)
            {
                StartMove(commonMove, commonResult, view);
            }
        }

        private (MoveMatchResult bestResult, CombatantMove bestMove) FindBestScoringMove(IInputView view,
            List<CombatantMove> candidates)
        {
            MoveMatchResult bestResult = MoveMatchResult.None;
            CombatantMove bestMove = null;

            foreach (var move in candidates)
            {
                var result = move.GetBestMatch(view);
                if (result.Score > bestResult.Score)
                {
                    bestResult = result;
                    bestMove = move;
                }
            }

            return (bestResult, bestMove);
        }

        // ── Cancel resolution ──────────────────────────────────────────────────────────

        /// <summary>
        /// Called every tick while a move is active. Checks all cancel conditions in priority
        /// order and starts the first move that wins the input contest.
        /// </summary>
        private void TryCancel(IInputView buffer, CombatantMove activeMove, CombatantMove lastMove)
        {
            // ── Neutral commit: fully transparent ──────────────────────────────────────
            // Walking, standing idle, crouching idle — these never lock the player out.
            // Any registered combat move, or a *different* common move, can preempt them.
            if (activeMove.CommitType == EMoveCommitType.Neutral)
            {
                // 1. Combat moves first.
                _cancelCandidates.Clear();
                AddCombatCandidates(_cancelCandidates);
                var (result, move) = FindBestScoringMove(buffer, _cancelCandidates);

                if (result.IsMatch && move != activeMove)
                {
                    StartMove(move, result, buffer);
                    return;
                }

                // 2. Common moves fallback — prevents restarting the same move each tick
                //    (e.g. CmnActStand staying idle without constantly re-entering itself).
                _cancelCandidates.Clear();
                AddCommonCandidates(_cancelCandidates);
                var (commonResult, commonMove) = FindBestScoringMove(buffer, _cancelCandidates);

                if (commonResult.IsMatch && commonMove != activeMove)
                    StartMove(commonMove, commonResult, buffer);

                return;
            }

            // ── Active commit: ordered cancel rules ────────────────────────────────────

            // 1. Kara-Cancel — same tier or higher while the opening window is live.
            //    Resolves multi-button inputs where one button arrives a frame late:
            //    e.g. pressing M on frame 1, then H on frame 2 still produces the
            //    M+H grab rather than locking in the M normal.
            if (_runner.CanKaraCancel())
            {
                _cancelCandidates.Clear();
                AddCandidatesForTypes(_cancelCandidates, GetKaraCancelTypes(activeMove.Type));

                var (result, move) = FindBestScoringMove(buffer, _cancelCandidates);
                if (result.IsMatch && move != activeMove)
                {
                    StartMove(move, result, buffer);
                    return;
                }
            }

            // 2. IASA — the move script explicitly opened a free-cancel window.
            if (_runner.IsIASA)
            {
                _cancelCandidates.Clear();
                AddCombatCandidates(_cancelCandidates);

                var (result, move) = FindBestScoringMove(buffer, _cancelCandidates);
                if (result.IsMatch && move != activeMove)
                {
                    StartMove(move, result, buffer);
                    return;
                }
            }

            // 3. Gatling / hit-confirm — only valid on the tick a hit or guard lands.
            if (_runner.HitConfirmed)
            {
                _cancelCandidates.Clear();

                // Explicit per-move whitelist takes precedence over the category ladder.
                foreach (var id in activeMove.GetGatlingOptions())
                {
                    var m = GetMoveById(id);
                    if (m != null && IsValidForCurrentState(m, allowFollowup: true))
                        _cancelCandidates.Add(m);
                }

                // Implicit category ladder: higher tiers are always reachable on hit.
                AddCandidatesForTypes(_cancelCandidates, _runner.GetAllowedCancelCategories(activeMove.Type));

                var (result, move) = FindBestScoringMove(buffer, _cancelCandidates);
                if (result.IsMatch)
                {
                    StartMove(move, result, buffer);
                    return;
                }
            }

            // 4. Whiff cancel — only in recovery, only into explicitly whitelisted moves.
            if (_stateMachine.CombatState == ECombatState.Recovery && !_runner.HitConfirmed)
            {
                _cancelCandidates.Clear();

                foreach (var id in activeMove.GetWhiffCancelOptions())
                {
                    var m = GetMoveById(id);
                    if (m != null && IsValidForCurrentState(m, allowFollowup: true))
                        _cancelCandidates.Add(m);
                }

                if (_cancelCandidates.Count > 0)
                {
                    var (result, move) = FindBestScoringMove(buffer, _cancelCandidates);
                    if (result.IsMatch)
                    {
                        StartMove(move, result, buffer);
                        return;
                    }
                }
            }
        }

        // ── Cancel helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the move tiers that are reachable via a Kara-Cancel from
        /// <paramref name="activeType"/>: the move's own tier (so you can resolve to a
        /// different move of the same type) plus any higher tiers, with the exception
        /// that Overdrive cannot kara into another Overdrive.
        /// </summary>
        private List<EMoveType> GetKaraCancelTypes(EMoveType activeType)
        {
            // GetAllowedCancelCategories already returns the strictly-higher tiers.
            var result = new List<EMoveType>(_runner.GetAllowedCancelCategories(activeType) ?? new List<EMoveType>());

            // Add the active tier itself so, e.g., pressing M a frame before H can still
            // resolve to a different Normal rather than always locking in the first one.
            if (activeType != EMoveType.Overdrive)
                result.Add(activeType);

            return result;
        }

        /// <summary>
        /// Appends all registered combat moves (Normal / Special / Overdrive / Movement)
        /// that are valid in the current character and combat state.
        /// </summary>
        private void AddCombatCandidates(List<CombatantMove> candidates)
        {
            AddFromList(_movementMoves, candidates, requireRegistered: true);
            AddFromList(_normalMoves, candidates, requireRegistered: true);
            AddFromList(_specialMoves, candidates, requireRegistered: true);
            AddFromList(_overdriveMoves, candidates, requireRegistered: true);
        }

        /// <summary>
        /// Appends common moves (walk, stand, crouch transitions…) appropriate for
        /// the current character state. Common moves are not subject to the IsRegistered
        /// gate — they are always structurally available.
        /// </summary>
        private void AddCommonCandidates(List<CombatantMove> candidates)
        {
            switch (_stateMachine.CharacterState)
            {
                case ECharacterState.Standing:
                    TryAddCommon(candidates, _cmnActStand);
                    TryAddCommon(candidates, _cmnActFWalk);
                    TryAddCommon(candidates, _cmnActBWalk);
                    TryAddCommon(candidates, _cmnStandToCrouch);
                    TryAddCommon(candidates, _cmnActJumpPre);
                    break;

                case ECharacterState.Crouching:
                    TryAddCommon(candidates, _cmnActCrouch);
                    TryAddCommon(candidates, _cmnCrouchToStand);
                    break;

                case ECharacterState.Airborne:
                    TryAddCommon(candidates, _cmnActJump);
                    TryAddCommon(candidates, _cmnActJumpLand);
                    break;
            }
        }

        private void TryAddCommon(List<CombatantMove> candidates, CombatantMove move)
        {
            if (move != null && IsValidForCurrentState(move, requireRegistered: false))
                candidates.Add(move);
        }

        /// <summary>
        /// Appends all registered combat moves whose <see cref="EMoveType"/> is in
        /// <paramref name="types"/>, filtered by the current state.
        /// Passing null is a no-op.
        /// </summary>
        private void AddCandidatesForTypes(List<CombatantMove> candidates, List<EMoveType> types)
        {
            if (types == null) return;

            foreach (var type in types)
            {
                var list = type switch
                {
                    EMoveType.Normal => _normalMoves,
                    EMoveType.Special => _specialMoves,
                    EMoveType.Overdrive => _overdriveMoves,
                    EMoveType.Movement => _movementMoves,
                    _ => null
                };

                if (list != null)
                    AddFromList(list, candidates, requireRegistered: true);
            }
        }

        private void AddFromList(List<CombatantMove> source, List<CombatantMove> candidates, bool requireRegistered)
        {
            foreach (var move in source)
                if (IsValidForCurrentState(move, requireRegistered))
                    candidates.Add(move);
        }

        /// <summary>
        /// Returns true when <paramref name="move"/> passes all entry gates for the
        /// current character and combat state.
        /// </summary>
        /// <param name="requireRegistered">
        /// Pass false for common moves which bypass the IsRegistered system.
        /// </param>
        /// <param name="allowFollowup">
        /// </param>
        private bool IsValidForCurrentState(CombatantMove move, bool requireRegistered = true,
            bool allowFollowup = false)
        {
            if (requireRegistered && !move.CanBeEntered) return false;

            if (move.IsFollowupMove && !allowFollowup) return false;

            // Character-state gate (Standing / Crouching / Airborne / Any).
            if (move.CharacterState != ECharacterState.Any &&
                move.CharacterState != _stateMachine.CharacterState)
                return false;

            // Hit / blockstun gate.
            var combatState = _stateMachine.CombatState;
            return move.HitBlockConditions switch
            {
                EHitBlockConditions.NotHitOrBlockstun =>
                    combatState != ECombatState.Hitstun && combatState != ECombatState.Blockstun,

                EHitBlockConditions.HitOrBlockstunOnly =>
                    combatState == ECombatState.Hitstun || combatState == ECombatState.Blockstun,

                EHitBlockConditions.HitstunOnly => combatState == ECombatState.Hitstun,
                EHitBlockConditions.BlockstunOnly => combatState == ECombatState.Blockstun,

                _ => true // HitOrBlockstunOk — always allowed
            };
        }

        // ── Move start ─────────────────────────────────────────────────────────────────

        private void StartMove(CombatantMove move, MoveMatchResult matchResult, IInputView view)
        {
            if (_runner.CurrentMove != null)
            {
                Debug.Log($"Cancelled {_runner.CurrentMove} into {move}.");
                _runner.Cancel();
            }

            _runner.Start(move, matchResult, view.GetFrame(0));
            _stateMachine.OnMoveStarted(move);
        }

        /// <summary>
        /// Starts a move that is triggered programmatically rather than through input
        /// (hitstun, blockstun, throw reactions, etc.).
        /// Uses the current buffer frame as a neutral entry input.
        /// </summary>
        public void StartMove(CombatantMove move)
        {
            var entryInput = new CharacterInputView(Buffer, _stateMachine.FacingDirection).GetFrame(0);
            if (_runner.CurrentMove != null) _runner.Cancel();
            _runner.Start(move, MoveMatchResult.None, entryInput);
            _stateMachine.OnMoveStarted(move);
        }

        // ── Move ID registry ───────────────────────────────────────────────────────────

        public uint GetMoveId(string moveName)
        {
            if (_moveNameToIdCache.TryGetValue(moveName, out var id))
            {
                return id;
            }

            var move = _runtimeMoveList.Find(m => m.GetType().Name == moveName);

            if (move == null)
            {
                move = _runtimeCmnMoveList.Find(m => m.GetType().Name == moveName);
            }

            if (move == null)
            {
                Debug.LogWarning($"Move with name {moveName} not found in combatant {name}.");
                return 0;
            }


            var newId = _nextMoveId++;
            _moveIdCache[newId] = move;
            _moveToIdCache[move] = newId;
            _moveNameToIdCache[moveName] = newId;
            return newId;
        }

        public CombatantMove GetMoveById(uint moveId)
        {
            if (_moveIdCache.TryGetValue(moveId, out var move))
            {
                return move;
            }

            Debug.LogWarning($"Move with ID {moveId} not found.");
            return null;
        }

        public MinMaxAABB BoxToWorld(MinMaxAABB localBox)
        {
            var position = (Unity.Mathematics.float3)transform.position;
            var scale = (Unity.Mathematics.float3)directionIndicatorRoot.transform.localScale;
            var rotation = directionIndicatorRoot.transform.rotation;

            // Apply scale to the local bounds
            var scaledMin = localBox.Min * scale;
            var scaledMax = localBox.Max * scale;

            // Generate the 8 corners of the scaled local bounding box
            Unity.Mathematics.float3[] corners =
            {
                new(scaledMin.x, scaledMin.y, scaledMin.z),
                new(scaledMin.x, scaledMin.y, scaledMax.z),
                new(scaledMin.x, scaledMax.y, scaledMin.z),
                new(scaledMin.x, scaledMax.y, scaledMax.z),
                new(scaledMax.x, scaledMin.y, scaledMin.z),
                new(scaledMax.x, scaledMin.y, scaledMax.z),
                new(scaledMax.x, scaledMax.y, scaledMin.z),
                new(scaledMax.x, scaledMax.y, scaledMax.z)
            };

            var worldMin = new Unity.Mathematics.float3(float.MaxValue);
            var worldMax = new Unity.Mathematics.float3(float.MinValue);

            // Rotate and translate each corner to calculate the new axis-aligned bounds
            foreach (var corner in corners)
            {
                var rotatedCorner = (Unity.Mathematics.float3)(rotation * corner);
                var worldCorner = position + rotatedCorner;

                worldMin = Unity.Mathematics.math.min(worldMin, worldCorner);
                worldMax = Unity.Mathematics.math.max(worldMax, worldCorner);
            }

            return new MinMaxAABB
            {
                Min = worldMin,
                Max = worldMax
            };
        }

        // ── External notifications ─────────────────────────────────────────────────────

        public EHitResolution NotifyIncomingHit(HitData hitData, CombatantBehaviour attacker)
        {
            var view = new CharacterInputView(Buffer, _stateMachine.FacingDirection);
            var dir = view.GetFrame(0).Direction.Current;
            var isHoldingBack = dir is EDirectionInput.Input4 or EDirectionInput.Input7 or EDirectionInput.Input1;

            if (_stateMachine.IsAbleToBlock && isHoldingBack)
            {
                return EHitResolution.Blocked;
            }

            return EHitResolution.Hit;
        }

        public void NotifyGotHit(HitResult hitResult)
        {
            if (Stats != null)
            {
                var dealt = Stats.ApplyDamage(hitResult.HitData.Damage);
                Stats.PendingHitstunTicks = hitResult.HitData.HitstunDuration;
                Stats.PendingHitLevel = hitResult.HitData.Level;
                Stats.PendingDamagePoseOverride = hitResult.HitData.OverrideDamagePose;
                Stats.PendingDamagePoseOverrideId = hitResult.HitData.DamagePoseOverrideId;
            }

            if (hitResult.HitData.IsLauncher) CharacterController.ForceUnground(1 * TickManager.TickInterval);

            CharacterController.AddVelocity(hitResult.VictimKnockback, EVelocitySpace.World);

            // Set combat state before starting the move so CommitType.Neutral
            // in OnMoveStarted leaves it untouched.
            _stateMachine.OnGotHit();

            if (_cmnActHitstun != null)
                StartMove(_cmnActHitstun);
            else
                Debug.LogWarning($"{name}: no CmnActHitstun configured — character will be stuck in hitstun.");
        }

        public void NotifyDealtHit(HitResult hitResult)
        {
            CharacterController.AddVelocity(hitResult.PerpetratorKnockback, EVelocitySpace.World);
            _runner.NotifyDealtHit();
        }

        public void NotifyBlocked(HitResult hitResult)
        {
            if (Stats != null)
            {
                Stats.PendingBlockstunTicks = hitResult.HitData.BlockstunDuration;
                Stats.PendingHitLevel = hitResult.HitData.Level;
            }

            CharacterController.AddVelocity(hitResult.VictimKnockback, EVelocitySpace.World);
            _stateMachine.OnBlocked();

            if (_cmnActBlockstun != null)
                StartMove(_cmnActBlockstun);
            else
                Debug.LogWarning($"{name}: no CmnActBlockstun configured — character will be stuck in blockstun.");
        }

        public void NotifyGotBlocked(HitResult hitResult)
        {
            CharacterController.AddVelocity(hitResult.PerpetratorKnockback, EVelocitySpace.World);
            _runner.NotifyGotBlocked();
            _stateMachine.OnBlocked();
        }


        public void NotifyLand()
        {
            _runner.NotifyLand();
            _stateMachine.OnLanded();
        }

        public void NotifyAirborne() => _stateMachine.OnBecameAirborne();

        public void NotifyHitstunEnd()
        {
            OnHitstunEnded?.Invoke();
        }

        public void NotifyBlockstunEnd()
        {
            OnBlockstunEnded?.Invoke();
        }
    }
}