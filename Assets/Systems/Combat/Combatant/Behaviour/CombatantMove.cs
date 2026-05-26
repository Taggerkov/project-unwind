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
    /// <summary>
    /// Result of a move input match attempt. Carries the winning specificity score and the
    /// button and direction that triggered the match, so that <see cref="MoveRunner"/> can
    /// reconstruct the entry input without re-reading the buffer.
    /// </summary>
    public readonly struct MoveMatchResult
    {
        /// <summary>Specificity score of the winning entry; negative means no match.</summary>
        public readonly int Score;

        /// <summary>Primary button from the matched input entry.</summary>
        public readonly EButtonInput TriggerButton;

        /// <summary>Primary direction from the matched input entry.</summary>
        public readonly EDirectionInput TriggerDirection;

        /// <summary>True when a valid entry matched (score is zero or positive).</summary>
        public bool IsMatch => Score >= 0;

        /// <summary>Sentinel value representing a failed match.</summary>
        public static readonly MoveMatchResult None = new(-1, EButtonInput.None, EDirectionInput.Input5);

        /// <summary>Constructs a successful match result.</summary>
        /// <param name="score">Specificity score of the winning entry.</param>
        /// <param name="button">Primary button that triggered the match.</param>
        /// <param name="direction">Primary direction that triggered the match.</param>
        public MoveMatchResult(int score, EButtonInput button, EDirectionInput direction)
        {
            Score = score;
            TriggerButton = button;
            TriggerDirection = direction;
        }
    }

    /// <summary>Broad category of a move, used by the cancel resolution priority ladder.</summary>
    public enum EMoveType
    {
        /// <summary>Idle — character is not committed to any action.</summary>
        Neutral = 0,
        /// <summary>Walking forward.</summary>
        ForwardWalk = 1,
        /// <summary>Walking backward.</summary>
        BackwardWalk = 2,
        /// <summary>Ground forward dash.</summary>
        ForwardDash = 3,
        /// <summary>Ground backward dash.</summary>
        BackwardDash = 4,
        /// <summary>Forward jump arc.</summary>
        ForwardJump = 5,
        /// <summary>Backward jump arc.</summary>
        BackwardJump = 6,
        /// <summary>Neutral jump arc.</summary>
        NeutralJump = 7,
        /// <summary>Aerial forward jump.</summary>
        ForwardAirJump = 8,
        /// <summary>Aerial backward jump.</summary>
        BackwardAirJump = 9,
        /// <summary>Aerial neutral jump.</summary>
        NeutralAirJump = 10,
        /// <summary>Aerial forward dash.</summary>
        ForwardAirDash = 11,
        /// <summary>Aerial backward dash.</summary>
        BackwardAirDash = 12,
        /// <summary>Generic movement move (does not fit a named locomotion type).</summary>
        Movement = 13,
        /// <summary>Standard attack move; lowest tier on the cancel ladder.</summary>
        Normal = 14,
        /// <summary>Special move; cancels from Normal on hit.</summary>
        Special = 15,
        /// <summary>Overdrive move; cancels from Special on hit.</summary>
        Overdrive = 16
    }

    /// <summary>Determines whether a move occupies the active-commit slot or is transparent to cancels.</summary>
    public enum EMoveCommitType
    {
        /// <summary>
        /// The move does not lock the player into the active-commit state. Movement and idle
        /// moves use this so any combat move or a different common move can always preempt them.
        /// </summary>
        Neutral = 0,

        /// <summary>
        /// The move occupies the active slot and is subject to the cancel priority rules
        /// (Kara, IASA, Gatling, Whiff cancel). Used by all attacking moves.
        /// </summary>
        Active = 1
    }

    /// <summary>Restricts which hit/blockstun states allow entry into a move.</summary>
    public enum EHitBlockConditions
    {
        /// <summary>Default. The move may only be entered when the character is not in hitstun or blockstun.</summary>
        NotHitOrBlockstun = 0,

        /// <summary>The move may be entered regardless of hitstun or blockstun state.</summary>
        HitOrBlockstunOk = 1,

        /// <summary>The move may only be entered while the character is in hitstun or blockstun. Rarely used.</summary>
        HitOrBlockstunOnly = 2,

        /// <summary>The move may only be entered while the character is in hitstun.</summary>
        HitstunOnly = 3,

        /// <summary>The move may only be entered while the character is in blockstun.</summary>
        BlockstunOnly = 4
    }

    /// <summary>Determines how the move can be guarded against.</summary>
    public enum EGuardType
    {
        /// <summary>Can be blocked from any guard stance.</summary>
        Any = 0,

        /// <summary>Can only be blocked by a standing guard.</summary>
        HighOnly = 1,

        /// <summary>Can only be blocked by a crouching guard.</summary>
        LowOnly = 2,

        /// <summary>Cannot be blocked.</summary>
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

        /// <summary>
        /// When false the move is excluded from the automatic candidate pool and can only be
        /// entered via an explicit gatling or whiff-cancel reference. Override to gate moves
        /// behind resource or state conditions that should not appear in normal entry.
        /// </summary>
        public virtual bool IsRegistered { get; } = true;

        /// <summary>True when both <see cref="IsRegistered"/> and <see cref="CanEnter"/> pass.</summary>
        public bool CanBeEntered => IsRegistered && CanEnter();

        /// <summary>Whether this move occupies the active-commit slot; drives cancel priority.</summary>
        public EMoveCommitType CommitType => commitType;

        /// <summary>Broad category used by the cancel priority ladder.</summary>
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

        /// <summary>Replaces the serialised move type at runtime; used by common-move factories.</summary>
        public void OverrideType(EMoveType newType) => type = newType;

        /// <summary>Replaces the required character state at runtime.</summary>
        public void OverrideCharacterState(ECharacterState newState) => characterState = newState;

        /// <summary>Replaces the hit/blockstun entry condition at runtime.</summary>
        public void OverrideHitBlockConditions(EHitBlockConditions newConditions) => hitBlockConditions = newConditions;

        /// <summary>Replaces the commit type at runtime.</summary>
        public void OverrideCommitType(EMoveCommitType newCommitType) => commitType = newCommitType;

        /// <summary>Replaces the entire input definition with a new OR-level list of entries.</summary>
        public void OverrideInputs(List<MoveInputEntry> newInputs) => inputs = newInputs;

        // ── Runtime context ────────────────────────────────────────────────────────────

        /// <summary>The combatant this move instance belongs to; set by <see cref="CloneFor"/>.</summary>
        [NonSerialized] protected CombatantBehaviour Owner;

        /// <summary>Handlers registered via <see cref="OnEachTick"/>; invoked by <see cref="MoveRunner"/> every tick.</summary>
        [NonSerialized] internal readonly List<Action<TickInput>> OnTickHandlers = new();

        /// <summary>Handlers registered via <see cref="OnHit"/>; invoked when a hit lands.</summary>
        [NonSerialized] internal readonly List<Action> OnHitHandlers = new();

        /// <summary>Handlers registered via <see cref="OnGuard"/>; invoked when the attack is blocked.</summary>
        [NonSerialized] internal readonly List<Action> OnGuardHandlers = new();

        /// <summary>Handlers registered via <see cref="OnLand"/>; invoked when the character touches the ground.</summary>
        [NonSerialized] internal readonly List<Action> OnLandHandlers = new();

        /// <summary>Handlers registered via <see cref="OnExit"/>; invoked when the move ends for any reason.</summary>
        [NonSerialized] internal readonly List<Action> OnExitHandlers = new();

        /// <summary>Permanent gatling cancel targets registered via <see cref="AddStaticGatlingOption"/>.</summary>
        [NonSerialized] internal readonly List<uint> StaticGatlingOptions = new();

        /// <summary>Permanent whiff-cancel targets registered via <see cref="AddStaticWhiffCancelOption"/>.</summary>
        [NonSerialized] internal readonly List<uint> StaticWhiffCancelOptions = new();

        /// <summary>Per-activation gatling cancel targets registered via <see cref="AddDynamicGatlingOption"/>; cleared on exit.</summary>
        [NonSerialized] internal readonly List<uint> DynamicGatlingOptions = new();

        /// <summary>Per-activation whiff-cancel targets registered via <see cref="AddDynamicWhiffCancelOption"/>; cleared on exit.</summary>
        [NonSerialized] internal readonly List<uint> DynamicWhiffCancelOptions = new();

        // ── Initialization ────────────────────────────────────────────────────────────

        /// <summary>Called once after cloning to let the move register static cancel options and cache owner references.</summary>
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

        /// <summary>Returns the <see cref="Script"/> coroutine; called by <see cref="MoveRunner"/> to start the move.</summary>
        internal IEnumerator GetScript()
        {
            return Script();
        }

        /// <summary>Returns the merged union of static and dynamic gatling cancel targets for this activation.</summary>
        internal List<uint> GetGatlingOptions() => StaticGatlingOptions.Union(DynamicGatlingOptions).ToList();

        /// <summary>Returns the merged union of static and dynamic whiff-cancel targets for this activation.</summary>
        internal List<uint> GetWhiffCancelOptions() =>
            StaticWhiffCancelOptions.Union(DynamicWhiffCancelOptions).ToList();

        /// <summary>
        /// Define the move here. yield return Pose(...) to hold a pose for N ticks.
        /// All other calls are instant and execute at the transition between poses.
        /// </summary>
        protected abstract IEnumerator Script();

        // ── DSL: Pose (the blocking primitive) ────────────────────────────────────────

        /// <summary>
        /// The only blocking primitive in a move script. Yields control back to
        /// <see cref="MoveRunner"/> and holds <paramref name="poseId"/> for <paramref name="ticks"/> ticks
        /// before the coroutine resumes.
        /// </summary>
        /// <param name="poseId">Global pose ID from the character's <see cref="Animation.CombatantPoseSheet"/>.</param>
        /// <param name="ticks">Number of simulation ticks to hold this pose.</param>
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

        /// <summary>Removes a previously registered per-tick handler.</summary>
        protected void ClearEventHandler(Action<TickInput> handler)
        {
            OnTickHandlers.RemoveAll(h => h == handler);
        }

        /// <summary>Removes a previously registered hit, guard, land, or exit handler.</summary>
        protected void ClearEventHandler(Action handler)
        {
            OnHitHandlers.RemoveAll(h => h == handler);
            OnGuardHandlers.RemoveAll(h => h == handler);
            OnLandHandlers.RemoveAll(h => h == handler);
            OnExitHandlers.RemoveAll(h => h == handler);
        }

        /// <summary>Clears all per-activation event handlers and dynamic cancel lists; called by <see cref="MoveRunner"/> on move exit.</summary>
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

        /// <summary>Registers a handler that fires when the trigger button is released while this move is active.</summary>
        protected void OnNegativeEdge(Action handler) => Owner.Runner.RegisterNegativeEdge(handler);

        /// <summary>Resolves or registers the numeric ID for the move type <typeparamref name="TMoveName"/> on this combatant.</summary>
        protected uint GetMoveId<TMoveName>() => Owner.GetMoveId(typeof(TMoveName).Name);

        // ── DSL: Hit data ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Publishes <paramref name="hitData"/> to the runner so it can be registered with
        /// <see cref="CombatManager"/> during the Active phase. Assigns a unique HitId automatically.
        /// </summary>
        protected void SetHitData(HitData hitData)
        {
            hitData.HitId = Owner.Runner.NextHitId();
            Owner.Runner.SetHitData(hitData);
        }

        /// <summary>
        /// Convenience overload that publishes hit data and returns a <see cref="HitScope"/>
        /// for fluent on-hit / on-guard callbacks in the script.
        /// </summary>
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

        /// <summary>Zeroes all accumulated free velocity on the character controller.</summary>
        protected void HaltMomentum()
            => Owner.CharacterController.HaltMomentum();

        /// <summary>Applies a gravity multiplier for the remainder of this move. Restored on exit.</summary>
        protected void SetGravityScale(float scale)
            => Owner.CharacterController.SetGravityScale(scale);

        /// <summary>Disables gravity entirely for the remainder of this move. Restored on exit.</summary>
        protected void DisableGravity()
            => Owner.CharacterController.DisableGravity();

        /// <summary>Re-enables gravity after a <see cref="DisableGravity"/> call.</summary>
        protected void RestoreGravity()
            => Owner.CharacterController.RestoreGravity();

        /// <summary>Disables ground friction for the remainder of this move. Restored on exit.</summary>
        protected void DisableFriction()
            => Owner.CharacterController.DisableFriction();

        /// <summary>Re-enables friction after a <see cref="DisableFriction"/> call.</summary>
        protected void RestoreFriction()
            => Owner.CharacterController.RestoreFriction();

        /// <summary>Plays the audio event mapped to <paramref name="soundId"/> in the owner's audio sheet.</summary>
        protected void PlaySound(uint soundId) => Owner.GameManager.AudioManager.Play(Owner.audioSheet.Get(soundId));

        // ── Cloning ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Produces a deep copy of the serialised move data and binds it to <paramref name="owner"/>,
        /// so each combatant instance gets independent runtime state. Called by
        /// <see cref="CombatantMoveSetDefinition"/> during session initialisation.
        /// </summary>
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