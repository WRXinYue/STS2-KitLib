using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace KitLib.Models;

/// <summary>Training dummy for KitLib card testing — infinite HP, no turn limit, battle-friend-v3 visuals.</summary>
public sealed class KitLibCardTestDummy : MonsterModel {
    protected override string VisualsPath => SceneHelper.GetScenePath("creature_visuals/battle_friend_v3");

    public override LocString Title => MonsterModel.L10NMonsterLookup("BATTLE_FRIEND_V3.name");

    public override int MinInitialHp => Really.bigNumber;

    public override int MaxInitialHp => Really.bigNumber;

    public override void SetupSkins(MegaSprite spine, MegaSkeleton skeleton) {
        var data = skeleton.GetData();
        skeleton.SetSkin(data.FindSkin("v3"));
        skeleton.SetSlotsToSetupPose();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine() {
        var moveState = new MoveState("NOTHING_MOVE", (IReadOnlyList<Creature> _) => Task.CompletedTask);
        moveState.FollowUpState = moveState;
        return new MonsterMoveStateMachine([moveState], moveState);
    }

    public override Task AfterAddedToRoom() {
        if (Creature != null)
            Creature.HpDisplay = HpDisplay.InfiniteWithoutNumbers;
        return Task.CompletedTask;
    }

    public override CreatureAnimator GenerateAnimator(MegaSprite controller) {
        var idle = new AnimState("idle_loop", isLooping: true);
        var hurt = new AnimState("hurt");
        var die = new AnimState("die");
        var dieLoop = new AnimState("die_loop", isLooping: true);
        hurt.NextState = idle;
        die.NextState = dieLoop;
        var animator = new CreatureAnimator(idle, controller);
        animator.AddAnyState("Idle", idle);
        animator.AddAnyState("Dead", die);
        animator.AddAnyState("Hit", hurt);
        return animator;
    }
}
