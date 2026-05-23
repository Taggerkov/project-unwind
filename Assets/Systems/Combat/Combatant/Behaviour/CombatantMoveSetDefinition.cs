using System.Collections.Generic;
using Systems.Combat.Combatant.Data;
using Systems.Combat.Combatant.StateMachine;
using Systems.Input;
using UnityEngine;

namespace Systems.Combat.Combatant.Behaviour
{
    /// <summary>
    /// Asset assigned to a character prefab. Defines all moves the character can perform.
    /// </summary>
    [CreateAssetMenu(fileName = "MoveSet", menuName = "Unwind Database/Combatant/Move Set Definition")]
    public class CombatantMoveSetDefinition : ScriptableObject
    {
        /// <summary>
        /// Inspector-configured prototype for this character's runtime data.
        /// CombatantBehaviour clones it at Awake so each instance gets independent state.
        /// Must be set; combat will not start correctly without it.
        /// </summary>
        [SerializeReference, TypeSelector]
        public CombatantStats StatsTemplate;

        [SerializeReference] public CombatantMoveList Moves = new();

        [SerializeReference, StatsConstrainedSelector]
        public CombatantMove CmnActStand;

        [SerializeReference, StatsConstrainedSelector]
        public CombatantMove CmnActFWalk;

        [SerializeReference, StatsConstrainedSelector]
        public CombatantMove CmnActBWalk;

        [SerializeReference, StatsConstrainedSelector]
        public CombatantMove CmnStandToCrouch;

        [SerializeReference, StatsConstrainedSelector]
        public CombatantMove CmnActCrouch;

        [SerializeReference, StatsConstrainedSelector]
        public CombatantMove CmnCrouchToStand;

        [SerializeReference, StatsConstrainedSelector]
        public CombatantMove CmnActJumpPre;

        [SerializeReference, StatsConstrainedSelector]
        public CombatantMove CmnActJump;

        [SerializeReference, StatsConstrainedSelector]
        public CombatantMove CmnActJumpLand;

        [SerializeReference, StatsConstrainedSelector]
        public CombatantMove CmnActHitstun;

        [SerializeReference, StatsConstrainedSelector]
        public CombatantMove CmnActBlockstun;

        public List<CombatantMove> InstantiateFor(CombatantBehaviour owner)
        {
            var result = new List<CombatantMove>(Moves.list.Count);
            foreach (var def in Moves.list)
            {
                if (def != null) result.Add(def.CloneFor(owner));
            }

            return result;
        }

        public CombatantMove InstantiateCmnActStand(CombatantBehaviour owner)
        {
            var move = CmnActStand?.CloneFor(owner);
            if (move == null)
                Debug.LogWarning($"Common move {nameof(CmnActStand)} is not defined in MoveSetDefinition {name}.");

            move!.OverrideType(EMoveType.Neutral);
            move.OverrideCharacterState(ECharacterState.Standing);
            move.OverrideCommitType(EMoveCommitType.Neutral);
            move.OverrideInputs(new List<MoveInputEntry>
            {
                new(EMotionInput.None, EButtonInput.None),
            });

            return move;
        }

        public CombatantMove InstantiateCmnActFWalk(CombatantBehaviour owner)
        {
            var move = CmnActFWalk?.CloneFor(owner);
            if (move == null)
                Debug.LogWarning($"Common move {nameof(CmnActFWalk)} is not defined in MoveSetDefinition {name}.");

            move!.OverrideType(EMoveType.Movement);
            move.OverrideCharacterState(ECharacterState.Standing);
            move.OverrideCommitType(EMoveCommitType.Neutral);
            move.OverrideInputs(new List<MoveInputEntry>
            {
                new(EMotionInput.Held6, EButtonInput.None),
            });

            return move;
        }

        public CombatantMove InstantiateCmnActBWalk(CombatantBehaviour owner)
        {
            var move = CmnActBWalk?.CloneFor(owner);
            if (move == null)
                Debug.LogWarning($"Common move {nameof(CmnActBWalk)} is not defined in MoveSetDefinition {name}.");

            move!.OverrideType(EMoveType.Movement);
            move.OverrideCharacterState(ECharacterState.Standing);
            move.OverrideCommitType(EMoveCommitType.Neutral);
            move.OverrideInputs(new List<MoveInputEntry>
            {
                new(EMotionInput.Held4, EButtonInput.None),
            });

            return move;
        }

        public CombatantMove InstantiateCmnStandToCrouch(CombatantBehaviour owner)
        {
            var move = CmnStandToCrouch?.CloneFor(owner);
            if (move == null)
                Debug.LogWarning($"Common move {nameof(CmnStandToCrouch)} is not defined in MoveSetDefinition {name}.");

            move!.OverrideType(EMoveType.Movement);
            move.OverrideCharacterState(ECharacterState.Standing);
            move.OverrideCommitType(EMoveCommitType.Neutral);
            move.OverrideInputs(new List<MoveInputEntry>
            {
                new(EMotionInput.HeldAnyDown, EButtonInput.None),
            });

            return move;
        }

        public CombatantMove InstantiateCmnActCrouch(CombatantBehaviour owner)
        {
            var move = CmnActCrouch?.CloneFor(owner);
            if (move == null)
                Debug.LogWarning($"Common move {nameof(CmnActCrouch)} is not defined in MoveSetDefinition {name}.");

            move!.OverrideType(EMoveType.Neutral);
            move.OverrideCharacterState(ECharacterState.Crouching);
            move.OverrideCommitType(EMoveCommitType.Neutral);
            move.OverrideInputs(new List<MoveInputEntry>
            {
                new(EMotionInput.HeldAnyDown, EButtonInput.None),
            });

            return move;
        }

        public CombatantMove InstantiateCmnCrouchToStand(CombatantBehaviour owner)
        {
            var move = CmnCrouchToStand?.CloneFor(owner);
            if (move == null)
                Debug.LogWarning($"Common move {nameof(CmnCrouchToStand)} is not defined in MoveSetDefinition {name}.");

            move!.OverrideType(EMoveType.Movement);
            move.OverrideCharacterState(ECharacterState.Crouching);
            move.OverrideCommitType(EMoveCommitType.Neutral);
            move.OverrideInputs(new List<MoveInputEntry>
            {
                new(EMotionInput.DisallowAnyDown, EButtonInput.None),
            });

            return move;
        }

        public CombatantMove InstantiateCmnActJumpPre(CombatantBehaviour owner)
        {
            var move = CmnActJumpPre?.CloneFor(owner);
            if (move == null)
                Debug.LogWarning($"Common move {nameof(CmnActJumpPre)} is not defined in MoveSetDefinition {name}.");

            move!.OverrideType(EMoveType.Movement);
            move.OverrideCharacterState(ECharacterState.Standing);
            move.OverrideCommitType(EMoveCommitType.Active);
            move.OverrideInputs(new List<MoveInputEntry>
            {
                new(EMotionInput.HeldAnyUp, EButtonInput.None),
            });

            return move;
        }

        public CombatantMove InstantiateCmnActJump(CombatantBehaviour owner)
        {
            var move = CmnActJump?.CloneFor(owner);
            if (move == null)
                Debug.LogWarning($"Common move {nameof(CmnActJump)} is not defined in MoveSetDefinition {name}.");

            move!.OverrideType(EMoveType.Movement);
            move.OverrideCharacterState(ECharacterState.Airborne);
            move.OverrideCommitType(EMoveCommitType.Neutral);
            move.OverrideInputs(new List<MoveInputEntry>
            {
                new(EMotionInput.None, EButtonInput.None), // always matches, score 0
            });

            return move;
        }

        public CombatantMove InstantiateCmnActJumpLand(CombatantBehaviour owner)
        {
            var move = CmnActJumpLand?.CloneFor(owner);
            if (move == null)
                Debug.LogWarning($"Common move {nameof(CmnActJumpLand)} is not defined in MoveSetDefinition {name}.");

            move!.OverrideType(EMoveType.Movement);
            move.OverrideCharacterState(ECharacterState.Airborne);
            move.OverrideCommitType(EMoveCommitType.Neutral);
            move.OverrideInputs(new List<MoveInputEntry>());

            return move;
        }

        public CombatantMove InstantiateCmnActHitstun(CombatantBehaviour owner)
        {
            var move = CmnActHitstun?.CloneFor(owner);
            if (move == null)
                Debug.LogWarning($"Common move {nameof(CmnActHitstun)} is not defined in MoveSetDefinition {name}.");

            // CommitType.Neutral is critical: OnMoveStarted must not overwrite the
            // Hitstun combat state that was set just before this move was started.
            move!.OverrideCharacterState(ECharacterState.Any);
            move.OverrideCommitType(EMoveCommitType.Neutral);
            move.OverrideHitBlockConditions(EHitBlockConditions.HitOrBlockstunOk);
            move.OverrideInputs(new List<MoveInputEntry>());
            return move;
        }

        public CombatantMove InstantiateCmnActBlockstun(CombatantBehaviour owner)
        {
            var move = CmnActBlockstun?.CloneFor(owner);
            if (move == null)
                Debug.LogWarning($"Common move {nameof(CmnActBlockstun)} is not defined in MoveSetDefinition {name}.");

            move!.OverrideCharacterState(ECharacterState.Any);
            move.OverrideCommitType(EMoveCommitType.Neutral);
            move.OverrideHitBlockConditions(EHitBlockConditions.HitOrBlockstunOk);
            move.OverrideInputs(new List<MoveInputEntry>());
            return move;
        }
    }
}