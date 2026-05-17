using System.Collections.Generic;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.HitSystem;
using Systems.Common;

namespace Systems.Combat.Combatant.StateMachine
{
    public enum ECharacterState
    {
        Standing,
        Crouching,
        Airborne,
        Any
    }

    public enum ECombatState
    {
        Neutral, // Only applicable for EMoveCommitType.Neutral moves.
        Startup,
        Active,
        Recovery,
        Hitstun,
        Blockstun,
    }

    /// <summary>
    /// Captures which directional variant of the jump was initiated.
    /// Set by CmnActJumpPre in its IMMEDIATE block; read by CmnActJump to branch poses/velocity.
    /// </summary>
    public enum EJumpType
    {
        Neutral,
        Forward,
        Backward,
    }

    public class CombatantStateMachine
    {
        public ECharacterState CharacterState { get; private set; } = ECharacterState.Standing;
        public ECombatState CombatState { get; private set; } = ECombatState.Neutral;

        public CombatantMove ActiveMove { get; private set; }

        public CombatantMove LastMove { get; private set; }

        public HitData HitData { get; private set; }

        public bool IsAbleToTurn => CharacterState is ECharacterState.Standing
                                    && IsTurningEnabled
                                    && CombatState is ECombatState.Neutral;

        public bool IsTurningEnabled { get; private set; } = true;

        public EFacingDirection FacingDirection { get; private set; } = EFacingDirection.Right;

        public bool IsAbleToBlock => CombatState is ECombatState.Neutral or ECombatState.Blockstun;

        public bool IsBusyBlocking => CombatState is ECombatState.Blockstun;

        public bool IsKaraCancelWindowOpen { get; private set; }

        public bool IsKaraCancelOverriden { get; private set; }


        public bool IASAEnabled { get; private set; }

        /// <summary>
        /// Directional variant of the current/most-recent jump.
        /// Written by CmnActJumpPre; read by CmnActJump.
        /// </summary>
        public EJumpType JumpType { get; private set; } = EJumpType.Neutral;

        public void SetFacingDirection(EFacingDirection direction)
        {
            if (FacingDirection != direction)
                FacingDirection = direction;
        }


        /// <summary>
        /// Called by the MoveRunner everytime a move starts.
        /// </summary>
        public void ResetMoveExecutionState()
        {
            IsKaraCancelWindowOpen = true;
            IsKaraCancelOverriden = false;

            IASAEnabled = false;
        }

        public void CloseKaraCancelWindow(EMoveType activeType)
        {
            IsKaraCancelWindowOpen = false;
        }

        public void OverrideKaraCancel(bool enabled) => IsKaraCancelOverriden = enabled;

        public void SetIASA(bool enabled) => IASAEnabled = enabled;

        public void SetHitData(HitData hitData) => HitData = hitData;

        public void SetJumpType(EJumpType type) => JumpType = type;

        // ── State transitions ─────────────────────────────────────────────────────────

        public void SetPhysical(ECharacterState state) => CharacterState = state;
        public void SetCombat(ECombatState state) => CombatState = state;

        public void OnMoveStarted(CombatantMove move)
        {
            LastMove = ActiveMove;
            ActiveMove = move;
            if (move.CommitType == EMoveCommitType.Active)
                SetCombat(ECombatState.Startup);
        }

        public void OnMoveEnded() => SetCombat(ECombatState.Neutral);
        public void OnGotHit() => SetCombat(ECombatState.Hitstun);
        public void OnBlocked() => SetCombat(ECombatState.Blockstun);
        public void OnLanded() => SetPhysical(ECharacterState.Standing);
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

        public override string ToString() =>
            $"Facing: {FacingDirection}\nPhysical: {CharacterState}\nCombat: {CombatState}";
    }
}