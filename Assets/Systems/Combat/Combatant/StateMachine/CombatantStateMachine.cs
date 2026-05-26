using System.Collections.Generic;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.HitSystem;
using Systems.Common;

namespace Systems.Combat.Combatant.StateMachine
{
    /// <summary>Physical posture of the character relative to the ground.</summary>
    public enum ECharacterState
    {
        /// <summary>On the ground and not crouching.</summary>
        Standing,

        /// <summary>On the ground and crouching.</summary>
        Crouching,

        /// <summary>Not in contact with the ground.</summary>
        Airborne,

        /// <summary>Wildcard used in move definitions to indicate the move is valid in any posture.</summary>
        Any
    }

    /// <summary>Combat activity state used to drive move entry gates and HUD feedback.</summary>
    public enum ECombatState
    {
        /// <summary>No active combat move; only applicable for <see cref="EMoveCommitType.Neutral"/> moves.</summary>
        Neutral,

        /// <summary>Active move is in its startup (pre-hitbox) phase.</summary>
        Startup,

        /// <summary>Active move has live hitboxes.</summary>
        Active,

        /// <summary>Active move has ended its hitbox phase and is recovering.</summary>
        Recovery,

        /// <summary>Character is suffering hitstun from a received hit.</summary>
        Hitstun,

        /// <summary>Character is in blockstun after successfully blocking an attack.</summary>
        Blockstun,
    }

    /// <summary>
    /// Captures which directional variant of the jump was initiated.
    /// Set by CmnActJumpPre in its IMMEDIATE block; read by CmnActJump to branch poses/velocity.
    /// </summary>
    /// <summary>Directional variant of a jump, written by <c>CmnActJumpPre</c> and read by <c>CmnActJump</c>.</summary>
    public enum EJumpType
    {
        /// <summary>Straight-up jump with no horizontal intent.</summary>
        Neutral,

        /// <summary>Jump angled toward the opponent.</summary>
        Forward,

        /// <summary>Jump angled away from the opponent.</summary>
        Backward,
    }

    /// <summary>
    /// Tracks all mutable per-round state for a combatant: physical posture, combat phase,
    /// active and last move references, hit data, facing direction, and cancel-window flags.
    /// All transitions go through the typed mutator methods so state is always consistent.
    /// </summary>
    public class CombatantStateMachine
    {
        /// <summary>Current physical posture (Standing/Crouching/Airborne).</summary>
        public ECharacterState CharacterState { get; private set; } = ECharacterState.Standing;

        /// <summary>Current combat phase (Neutral/Startup/Active/Recovery/Hitstun/Blockstun).</summary>
        public ECombatState CombatState { get; private set; } = ECombatState.Neutral;

        /// <summary>The move that is currently being executed by the runner.</summary>
        public CombatantMove ActiveMove { get; private set; }

        /// <summary>The move that was active before the current one; used for kara-cancel tier comparison.</summary>
        public CombatantMove LastMove { get; private set; }

        /// <summary>Hit data set by the active move during its Active phase; cleared on move end.</summary>
        public HitData HitData { get; private set; }

        /// <summary>True when the combatant can auto-face: standing, neutral combat state, and turning not suppressed.</summary>
        public bool IsAbleToTurn => CharacterState is ECharacterState.Standing
                                    && IsTurningEnabled
                                    && CombatState is ECombatState.Neutral;

        /// <summary>When false, auto-facing is suppressed (e.g. during certain moves that lock facing).</summary>
        public bool IsTurningEnabled { get; private set; } = true;

        /// <summary>Current facing direction, kept in sync with the visual root.</summary>
        public EFacingDirection FacingDirection { get; private set; } = EFacingDirection.Right;

        /// <summary>True when the combatant can enter blockstun (neutral or already blocking).</summary>
        public bool IsAbleToBlock => CombatState is ECombatState.Neutral or ECombatState.Blockstun;

        /// <summary>True when the combatant is currently serving blockstun frames.</summary>
        public bool IsBusyBlocking => CombatState is ECombatState.Blockstun;

        /// <summary>True during the 3-tick kara-cancel startup window of the current move.</summary>
        public bool IsKaraCancelWindowOpen { get; private set; }

        /// <summary>When true, the kara-cancel window has been overridden by the move script (e.g. locked or extended).</summary>
        public bool IsKaraCancelOverriden { get; private set; }

        /// <summary>True when the active move has explicitly opened an IASA (Interruptible As Soon As) cancel window.</summary>
        public bool IASAEnabled { get; private set; }

        /// <summary>
        /// Directional variant of the current/most-recent jump.
        /// Written by CmnActJumpPre; read by CmnActJump.
        /// </summary>
        public EJumpType JumpType { get; private set; } = EJumpType.Neutral;

        /// <summary>Resets all state to its default values for the start of a new round.</summary>
        public void ResetForNewRound()
        {
            CharacterState = ECharacterState.Standing;
            CombatState = ECombatState.Neutral;
            ActiveMove = null;
            LastMove = null;
            HitData = default;
            IsTurningEnabled = true;
            FacingDirection = EFacingDirection.Right;
            IsKaraCancelWindowOpen = false;
            IsKaraCancelOverriden = false;
            IASAEnabled = false;
            JumpType = EJumpType.Neutral;
        }

        /// <summary>Updates <see cref="FacingDirection"/> only when the direction actually changes.</summary>
        public void SetFacingDirection(EFacingDirection direction)
        {
            if (FacingDirection != direction)
                FacingDirection = direction;
        }

        /// <summary>
        /// Called by <see cref="MoveRunner"/> at the start of every move. Opens the kara-cancel
        /// window and clears any IASA flag left over from the previous move.
        /// </summary>
        public void ResetMoveExecutionState()
        {
            IsKaraCancelWindowOpen = true;
            IsKaraCancelOverriden = false;

            IASAEnabled = false;
        }

        /// <summary>Closes the kara-cancel window at the end of its 3-tick opening. <paramref name="activeType"/> is reserved for future tier filtering.</summary>
        public void CloseKaraCancelWindow(EMoveType activeType)
        {
            IsKaraCancelWindowOpen = false;
        }

        /// <summary>Allows a move script to manually extend or suppress the kara-cancel window.</summary>
        public void OverrideKaraCancel(bool enabled) => IsKaraCancelOverriden = enabled;

        /// <summary>Opens or closes the IASA cancel window; called by the move script's <c>EnableIASA()</c> DSL method.</summary>
        public void SetIASA(bool enabled) => IASAEnabled = enabled;

        /// <summary>Stores the active move's hit data so the overlap solver can read it each frame.</summary>
        public void SetHitData(HitData hitData) => HitData = hitData;

        /// <summary>Records the jump direction chosen by <c>CmnActJumpPre</c> for use by <c>CmnActJump</c>.</summary>
        public void SetJumpType(EJumpType type) => JumpType = type;

        // ── State transitions ─────────────────────────────────────────────────────────

        /// <summary>Sets the physical character posture directly (used by transition moves such as stand-to-crouch).</summary>
        public void SetPhysical(ECharacterState state) => CharacterState = state;

        /// <summary>Sets the combat phase directly.</summary>
        public void SetCombat(ECombatState state) => CombatState = state;

        /// <summary>Records move history and transitions to Startup when the new move has an Active commit type.</summary>
        public void OnMoveStarted(CombatantMove move)
        {
            LastMove = ActiveMove;
            ActiveMove = move;
            if (move.CommitType == EMoveCommitType.Active)
                SetCombat(ECombatState.Startup);
        }

        /// <summary>Transitions to Neutral combat state when a move ends naturally or is cancelled.</summary>
        public void OnMoveEnded() => SetCombat(ECombatState.Neutral);

        /// <summary>Transitions to Hitstun when a hit is received.</summary>
        public void OnGotHit() => SetCombat(ECombatState.Hitstun);

        /// <summary>Transitions to Blockstun when an attack is blocked.</summary>
        public void OnBlocked() => SetCombat(ECombatState.Blockstun);

        /// <summary>Transitions the physical state to Standing on landing.</summary>
        public void OnLanded() => SetPhysical(ECharacterState.Standing);

        /// <summary>Transitions the physical state to Airborne when the character leaves the ground.</summary>
        public void OnBecameAirborne() => SetPhysical(ECharacterState.Airborne);

        // ── Available category query ──────────────────────────────────────────────────

        /// <summary>
        /// Returns which move categories can be entered into based on this state machine's current state.
        /// </summary>
        public IReadOnlyList<EMoveType> GetAllowedCategories()
        {
            // While stunned, no moves can normally be entered.
            // Moves with HitOrBlockstunOk or HitstunOnly/BlockstunOnly will still pass the
            // per-move IsValidForCurrentState check in CombatantBehaviour.
            if (CombatState is ECombatState.Hitstun or ECombatState.Blockstun)
                return System.Array.Empty<EMoveType>();

            return new[]
            {
                EMoveType.Neutral,
                EMoveType.Movement,
                EMoveType.Normal,
                EMoveType.Special,
                EMoveType.Overdrive,
            };
        }

        /// <summary>Returns a multi-line debug string with current facing, physical, and combat state.</summary>
        public override string ToString() =>
            $"Facing: {FacingDirection}\nPhysical: {CharacterState}\nCombat: {CombatState}";
    }
}