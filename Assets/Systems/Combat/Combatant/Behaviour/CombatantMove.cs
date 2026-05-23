using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Systems.Combat.Combatant.Controller;
using Systems.Combat.Combatant.StateMachine;
using Systems.Combat.HitSystem;
using Systems.Core;
using Systems.Input;
using UnityEngine;

namespace Systems.Combat.Combatant.Behaviour
{
    public readonly struct MoveMatchResult
    {
        public readonly int Score;
        public readonly EButtonInput TriggerButton; // buttons from the matched entry
        public readonly EDirectionInput TriggerDirection; // direction from the matched entry

        public bool IsMatch => Score >= 0;

        public static readonly MoveMatchResult None = new(-1, EButtonInput.None, EDirectionInput.Input5);

        public MoveMatchResult(int score, EButtonInput button, EDirectionInput direction)
        {
            Score = score;
            TriggerButton = button;
            TriggerDirection = direction;
        }
    }

    public enum EMoveType
    {
        Neutral = 0,
        ForwardWalk = 1,
        BackwardWalk = 2,
        ForwardDash = 3,
        BackwardDash = 4,
        ForwardJump = 5,
        BackwardJump = 6,
        NeutralJump = 7,
        ForwardAirJump = 8,
        BackwardAirJump = 9,
        NeutralAirJump = 10,
        ForwardAirDash = 11,
        BackwardAirDash = 12,
        Movement = 13,
        Normal = 14,
        Special = 15,
        Overdrive = 16
    }

    public enum EMoveCommitType
    {
        Neutral =
            0, // The move does not put the character in an active state. This is typically used for movement moves that should not be interrupted by other moves.

        Active =
            1 // The move puts the character in an active state, allowing it to be interrupted by other moves according to the usual rules.
    }

    public enum EHitBlockConditions
    {
        NotHitOrBlockstun =
            0, //Default. The move can only be entered if the character is not currently in hitstun or blockstun. This is the standard behavior for most moves.
        HitOrBlockstunOk = 1, // Allows the move to be executed no matter the hit/blockstun state.

        HitOrBlockstunOnly =
            2, // The move can only be entered if the character is currently in hitstun or blockstun. Rarely used.
        HitstunOnly = 3,
        BlockstunOnly = 4
    }

    ///<summary>Determines how the move can be guarded against.</summary>
    public enum EGuardType
    {
        Any = 0,
        HighOnly = 1,
        LowOnly = 2,
        Unblockable = 3
    }

    [Serializable]
    public abstract class CombatantMove
    {
        // ── Move identity (set in the inspector) ───────────────────────────────────────

        [SerializeField] private EMoveType type = EMoveType.Normal;

        [SerializeField] private ECharacterState characterState = ECharacterState.Any;

        [SerializeField] private EHitBlockConditions hitBlockConditions = EHitBlockConditions.NotHitOrBlockstun;

        [SerializeField] private EMoveCommitType commitType = EMoveCommitType.Active;

        [SerializeField] private bool isFollowupMove = false;

        /// <summary>
        /// OR-level list: the move matches when ANY entry resolves to true.
        /// Each MoveInputEntry is an AND-clause: all of its descriptors must match simultaneously.
        /// </summary>
        [SerializeField] private List<MoveInputEntry> inputs = new();

        public virtual bool IsRegistered { get; } = true;

        public bool CanBeEntered => IsRegistered && CanEnter();

        public EMoveCommitType CommitType => commitType;
        public EMoveType Type => type;

        /// <summary>The character state required to enter this move (Standing, Crouching, Airborne, or Any).</summary>
        public ECharacterState CharacterState => characterState;

        /// <summary>Defines under what hit/blockstun conditions this move may be entered.</summary>
        public EHitBlockConditions HitBlockConditions => hitBlockConditions;

        /// <summary>
        /// When true, this move won't be considered as a candidate for move entering. Only when specifically added as a gatling/whiff cancel option from another move.
        /// </summary>
        public bool IsFollowupMove => isFollowupMove;

        // ── Input matching ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the best-scoring MoveMatchResult among all entries that fully match
        /// the current buffer state, or MoveMatchResult.None if nothing matches.
        /// </summary>
        public MoveMatchResult GetBestMatch(IInputView buffer)
        {
            var best = MoveMatchResult.None;

            foreach (var entry in inputs)
            {
                if (!MotionMatcher.Matches(buffer, entry)) continue;

                int score = entry.Specificity;
                if (score > best.Score)
                {
                    best = new MoveMatchResult(
                        score,
                        entry.PrimaryButton,
                        GetDirectionFromMotion(buffer, entry.PrimaryMotion, 0));
                }
            }

            return best;
        }

        private EDirectionInput GetDirectionFromMotion(IInputView buffer, EMotionInput motion, int ticksAgo)
        {
            switch (motion)
            {
                case EMotionInput.Held4: return EDirectionInput.Input4;
                case EMotionInput.Held6: return EDirectionInput.Input6;
                case EMotionInput.Held2: return EDirectionInput.Input2;
                case EMotionInput.Held8: return EDirectionInput.Input8;
                case EMotionInput.QCF: return EDirectionInput.Input6;
                case EMotionInput.QCB: return EDirectionInput.Input4;
                case EMotionInput.DP: return EDirectionInput.Input3;
                case EMotionInput.RDP: return EDirectionInput.Input1;
                case EMotionInput.Charge46: return EDirectionInput.Input6;
                case EMotionInput.Charge64: return EDirectionInput.Input4;
                case EMotionInput.Charge28: return EDirectionInput.Input8;
                case EMotionInput.Charge82: return EDirectionInput.Input2;
                case EMotionInput.HCF: return EDirectionInput.Input6;
                case EMotionInput.HCB: return EDirectionInput.Input4;
                case EMotionInput.FC:
                    return buffer.GetFrame(ticksAgo).Direction.Current;
                case EMotionInput.DoubleTap2: return EDirectionInput.Input2;
                case EMotionInput.DoubleTap4: return EDirectionInput.Input4;
                case EMotionInput.DoubleTap6: return EDirectionInput.Input6;
                case EMotionInput.DoubleTap8: return EDirectionInput.Input8;
                case EMotionInput.None:
                default:
                    return EDirectionInput.Input5; // Neutral
            }
        }

        /// <summary>
        /// Returns true if any entry that has a button requirement currently matches the buffer.
        /// Used to distinguish button-anchored inputs from pure motion/direction inputs.
        /// </summary>
        public bool HasButtonAnchoredInput(IInputView buffer)
        {
            foreach (var entry in inputs)
            {
                if (entry.PrimaryButton == EButtonInput.None) continue;
                if (MotionMatcher.Matches(buffer, entry)) return true;
            }

            return false;
        }

        public void OverrideType(EMoveType newType) => type = newType;
        public void OverrideCharacterState(ECharacterState newState) => characterState = newState;
        public void OverrideHitBlockConditions(EHitBlockConditions newConditions) => hitBlockConditions = newConditions;
        public void OverrideCommitType(EMoveCommitType newCommitType) => commitType = newCommitType;

        /// <summary>Replaces the entire input definition with a new OR-level list of entries.</summary>
        public void OverrideInputs(List<MoveInputEntry> newInputs) => inputs = newInputs;

        // ── Runtime context ────────────────────────────────────────────────────────────
        [NonSerialized] protected CombatantBehaviour Owner;

        [NonSerialized] internal readonly List<Action<TickInput>> OnTickHandlers = new();
        [NonSerialized] internal readonly List<Action> OnHitHandlers = new();
        [NonSerialized] internal readonly List<Action> OnGuardHandlers = new();
        [NonSerialized] internal readonly List<Action> OnLandHandlers = new();
        [NonSerialized] internal readonly List<Action> OnExitHandlers = new();
        [NonSerialized] internal readonly List<uint> StaticGatlingOptions = new();
        [NonSerialized] internal readonly List<uint> StaticWhiffCancelOptions = new();
        [NonSerialized] internal readonly List<uint> DynamicGatlingOptions = new();
        [NonSerialized] internal readonly List<uint> DynamicWhiffCancelOptions = new();

        // ── Initialization ────────────────────────────────────────────────────────────

        internal void Initialize()
        {
            OnInitialize();
        }

        /// <summary>Called once after cloning when Owner is available. Use for one-time setup.</summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// A check that determines if the move can be entered beyond general rules.
        /// Useful for moves that are only usable when low health or use a resource of some kind to activate.
        /// </summary>
        protected internal virtual bool CanEnter()
        {
            return true;
        }

        /// <summary>Called every time this move becomes active, regardless if it's kara-cancelable.</summary>
        protected internal virtual void OnMoveEnter()
        {
        }

        /// <summary>Called every time this move becomes active, after the Kara-Cancel window closes.</summary>
        protected internal virtual void OnMoveCommited()
        {
        }

        /// <summary>
        /// Called every time this move ends, naturally or via cancel.
        /// Runs after all OnExit() script handlers. Use for guaranteed cleanup.
        /// </summary>
        protected internal virtual void OnMoveExit()
        {
        }

        // ── Entry point called by MoveRunner ──────────────────────────────────────────

        internal IEnumerator GetScript()
        {
            return Script();
        }

        internal List<uint> GetGatlingOptions() => StaticGatlingOptions.Union(DynamicGatlingOptions).ToList();

        internal List<uint> GetWhiffCancelOptions() =>
            StaticWhiffCancelOptions.Union(DynamicWhiffCancelOptions).ToList();

        /// <summary>
        /// Define the move here. yield return Pose(...) to hold a pose for N ticks.
        /// All other calls are instant and execute at the transition between poses.
        /// </summary>
        protected abstract IEnumerator Script();

        // ── DSL: Pose (the blocking primitive) ────────────────────────────────────────
        protected static PoseYield Pose(uint poseId, int ticks)
            => new PoseYield(poseId, ticks);

        // ── DSL: Async event handlers ─────────────────────────────────────────────────

        /// <summary>Run on every tick while this move is active. Receives the current frame's input.</summary>
        protected void OnEachTick(Action<TickInput> handler) => OnTickHandlers.Add(handler);

        /// <summary>Run when this move's hitbox lands a hit.</summary>
        protected void OnHit(Action handler) => OnHitHandlers.Add(handler);

        /// <summary>Run when this move is blocked by the opponent.</summary>
        protected void OnGuard(Action handler) => OnGuardHandlers.Add(handler);

        /// <summary>Run when this move either hits or is blocked.</summary>
        protected void OnHitOrGuard(Action handler)
        {
            OnHit(handler);
            OnGuard(handler);
        }

        /// <summary>Run when the character touches the ground.</summary>
        protected void OnLand(Action handler) => OnLandHandlers.Add(handler);

        /// <summary>Run when this move ends for any reason, including cancels.</summary>
        protected void OnExit(Action handler) => OnExitHandlers.Add(handler);

        protected void ClearEventHandler(Action<TickInput> handler)
        {
            OnTickHandlers.RemoveAll(h => h == handler);
        }

        protected void ClearEventHandler(Action handler)
        {
            OnHitHandlers.RemoveAll(h => h == handler);
            OnGuardHandlers.RemoveAll(h => h == handler);
            OnLandHandlers.RemoveAll(h => h == handler);
            OnExitHandlers.RemoveAll(h => h == handler);
        }

        public void ClearDynamicMoveState()
        {
            OnTickHandlers.Clear();
            OnHitHandlers.Clear();
            OnGuardHandlers.Clear();
            OnLandHandlers.Clear();
            OnExitHandlers.Clear();
            DynamicGatlingOptions.Clear();
            DynamicWhiffCancelOptions.Clear();
        }

        // ── DSL: State transitions ─────────────────────────────────────────────────────

        /// <summary>Call from Script() to mark the transition into the active (hitbox) phase.</summary>
        protected void BeginActiveState()
        {
            if (commitType != EMoveCommitType.Active)
            {
                Debug.LogWarning(
                    $"Move {GetType().Name} is not an Active move, but BeginActiveState was called. This is not intended behaviour.");
                return;
            }

            Owner.StateMachine.SetCombat(ECombatState.Active);
        }

        /// <summary>Call from Script() to mark the transition into the recovery phase.</summary>
        protected void BeginRecoveryState()
        {
            if (commitType != EMoveCommitType.Active)
            {
                Debug.LogWarning(
                    $"Move {GetType().Name} is not an Active move, but BeginRecoveryState was called. This is not intended behaviour.");
                return;
            }

            Owner.StateMachine.SetCombat(ECombatState.Recovery);
        }

        // ── DSL: Cancel / transition options ──────────────────────────────────────────

        /// <summary>Allow cancelling into moveId on hit or block (permanent).</summary>
        protected void AddStaticGatlingOption(uint moveId) => StaticGatlingOptions.Add(moveId);

        /// <summary>Allow cancelling into moveId on whiff from recovery (permanent).</summary>
        protected void AddStaticWhiffCancelOption(uint moveId) => StaticWhiffCancelOptions.Add(moveId);

        /// <summary>Allow cancelling into moveId on hit or block (removable).</summary>
        protected void AddDynamicGatlingOption(uint moveId) => DynamicGatlingOptions.Add(moveId);

        /// <summary>Allow cancelling into moveId on whiff from recovery (removable).</summary>
        protected void AddDynamicWhiffCancelOption(uint moveId) => DynamicWhiffCancelOptions.Add(moveId);

        /// <summary>Disallows the MoveRunner from modifying the state of the Kara-Cancel window.</summary>
        protected void OverrideKaraCancelWindow() => Owner.Runner.OverrideKaraCancel(true);

        protected void OnNegativeEdge(Action handler) => Owner.Runner.RegisterNegativeEdge(handler);
        protected uint GetMoveId<TMoveName>() => Owner.GetMoveId(typeof(TMoveName).Name);

        // ── DSL: Hit data ──────────────────────────────────────────────────────────────

        protected void SetHitData(HitData hitData)
        {
            hitData.HitId = Owner.Runner.NextHitId();
            Owner.Runner.SetHitData(hitData);
        }

        protected HitScope Hit(HitData hitData)
        {
            hitData.HitId = Owner.Runner.NextHitId();
            Owner.Runner.SetHitData(hitData);
            return new HitScope(Owner.Runner);
        }


        // ── DSL: Input access (character space) ───────────────────────────────────────

        /// <summary>
        /// Character-space input frame from the tick this move was entered.
        /// Reflects facing: Input6 is always "forward", Input4 always "backward".
        /// Available from the IMMEDIATE block onward.
        /// </summary>
        protected TickInput EntryInput => Owner.Runner.EntryInput;

        /// <summary>
        /// Character-space input frame for the tick currently executing.
        /// Mirrors what OnEachTick handlers receive; updated before Script() resumes each tick.
        /// </summary>
        protected TickInput CurrentInput => Owner.Runner.CurrentInput;

        // ── DSL: Character stats ───────────────────────────────────────────────────────

        /// <summary>
        /// Live stats for this character (HP, etc.).
        /// Equivalent to <c>GetStats&lt;CharacterStats&gt;()</c> without the cast.
        /// </summary>
        protected CombatantStats Stats => Owner.Stats;

        /// <summary>
        /// Returns the stats cast to a character-specific type.
        /// Use this inside moves that belong to a concrete character:
        ///
        ///   int jumps = GetStats&lt;SolStats&gt;().RemainingJumps;
        ///
        /// Throws InvalidCastException if the wrong type is requested — which is
        /// a configuration error caught immediately in development.
        /// </summary>
        protected TStats GetStats<TStats>() where TStats : CombatantStats
            => (TStats)Owner.Stats;

        // ── DSL: Jump type ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Directional variant of the jump set by CmnActJumpPre.
        /// Reads from the state machine — valid from the moment SetJumpType is called.
        /// </summary>
        protected EJumpType JumpType => Owner.StateMachine.JumpType;

        /// <summary>
        /// Stamps the jump direction so that CmnActJump (and any air moves) can branch
        /// on it without re-reading input. Call this in the IMMEDIATE block of CmnActJumpPre.
        /// </summary>
        protected void SetJumpType(EJumpType type) => Owner.StateMachine.SetJumpType(type);

        /// <summary>
        /// Closes the Kara-Cancel window. Called automatically by MoveRunner after a
        /// certain number of ticks, but can be called manually for tighter control.
        /// </summary>
        protected void CloseKaraCancelWindow() => Owner.Runner.CloseKaraCancelWindow();

        /// <summary>Make the character interruptible by ANY action from this point onward.</summary>
        protected void EnableIASA() => Owner.Runner.SetIASA(true);


        // ── DSL: State notifications ────────────────────────────────────────────────────
        protected void BecomeAirborne() => Owner.NotifyAirborne();

        // ── DSL: Instant actions ───────────────────────────────────────────────────────


        protected void SetConstantVelocity(Vector3 velocity, EVelocitySpace space = EVelocitySpace.Character)
            => Owner.CharacterController.SetConstantVelocity(velocity, space);

        protected void ClearConstantVelocity(EVelocitySpace space = EVelocitySpace.Character)
            => Owner.CharacterController.ClearConstantVelocity(space);

        protected void AddVelocity(Vector3 velocity, EVelocitySpace space = EVelocitySpace.Character)
            => Owner.CharacterController.AddVelocity(velocity, space);

        protected void ScaleFreeVelocityX(float factor)
            => Owner.CharacterController.ScaleFreeVelocityX(factor);

        protected void ForceUnground(int ticksUngrounded = 1) =>
            Owner.CharacterController.ForceUnground(ticksUngrounded * TickManager.TickInterval);

        // Physics overrides — all automatically cleaned up on move exit
        // via ResetPhysicsOverrides() called in MoveRunner.Finish()

        protected void HaltMomentum()
            => Owner.CharacterController.HaltMomentum();

        protected void SetGravityScale(float scale)
            => Owner.CharacterController.SetGravityScale(scale);

        protected void DisableGravity()
            => Owner.CharacterController.DisableGravity();

        protected void RestoreGravity()
            => Owner.CharacterController.RestoreGravity();

        protected void DisableFriction()
            => Owner.CharacterController.DisableFriction();

        protected void RestoreFriction()
            => Owner.CharacterController.RestoreFriction();

        protected void PlaySound(uint soundId) => Owner.GameManager.AudioManager.Play(Owner.audioSheet.Get(soundId));

        // ── Cloning ────────────────────────────────────────────────────────────────────

        internal CombatantMove CloneFor(CombatantBehaviour owner)
        {
            var json = JsonUtility.ToJson(this);
            var clone = (CombatantMove)Activator.CreateInstance(GetType());
            JsonUtility.FromJsonOverwrite(json, clone);
            clone.Owner = owner;
            return clone;
        }
    }

    public abstract class CombatantMove<TStats> : CombatantMove where TStats : CombatantStats
    {
        /// <summary>
        /// Character stats, pre-cast to <typeparamref name="TStats"/>.
        /// Throws a clear InvalidOperationException if this move is assigned to a character
        /// whose StatsTemplate is not a <typeparamref name="TStats"/> — catches mismatched
        /// move/character pairings immediately rather than at an arbitrary cast site.
        /// </summary>
        protected new TStats Stats
        {
            get
            {
                if (Owner.Stats is TStats typed) return typed;
                throw new InvalidOperationException(
                    $"Move {GetType().Name} expects {typeof(TStats).Name} but " +
                    $"{Owner.name} has {Owner.Stats?.GetType().Name ?? "null"}. " +
                    "Check the StatsTemplate assignment in CombatantMoveSetDefinition.");
            }
        }
    }
}