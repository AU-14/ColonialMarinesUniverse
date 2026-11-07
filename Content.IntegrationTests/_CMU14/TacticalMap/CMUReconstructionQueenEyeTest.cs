using Content.Client._RMC14.TacticalMap;
using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.Events;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Watch;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.DoAfter;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Input;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Controlled construction and map fixtures.
    [TestCase(false)]
    [TestCase(true)]
    public async Task QueenEyeMovementDoesNotCancelResinConstruction(bool remote)
    {
        EntityUid queen = default;
        DoAfter pending = null!;
        var target = new EntityCoordinates(_upper, new Vector2(1.5f, 0.5f));
        await Server.WaitAssertion(() =>
        {
            queen = SEntMan.SpawnEntity("CMXenoQueen", new EntityCoordinates(_upper, new Vector2(0.5f)));
            SEntMan.SpawnEntity("XenoWeeds", target);
            SEntMan.SpawnEntity("XenoWeeds", new EntityCoordinates(_upper, new Vector2(6.5f)));
            if (remote) SEntMan.EventBus.RaiseLocalEvent(queen, new QueenEyeActionEvent { Performer = queen });
            var construction = SComp<XenoConstructionComponent>(queen);
            construction.BuildChoice = "WallXenoResin";
            construction.BuildDelay = TimeSpan.FromSeconds(1);
            var action = SEntMan.System<SharedActionsSystem>().GetActions(queen)
                .Single(a => SComp<MetaDataComponent>(a).EntityPrototype?.ID == "ActionXenoSecreteStructure");
            var attempt = new XenoSecreteStructureActionEvent
            {
                Performer = queen, Action = (action, SComp<ActionComponent>(action)), Target = target,
            };
            SEntMan.EventBus.RaiseLocalEvent(queen, attempt);
            Assert.That(attempt.Handled, Is.True);
            pending = SComp<DoAfterComponent>(queen).DoAfters.Values.Single(d => d.Args.Event is XenoSecreteStructureDoAfterEvent);
            var mover = remote ? SComp<QueenEyeActionComponent>(queen).Eye!.Value : queen;
            SEntMan.System<SharedTransformSystem>().SetCoordinates(mover, new EntityCoordinates(_upper, new Vector2(6.5f)));
        });
        await RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(pending.Cancelled, Is.EqualTo(!remote), "Only physical construction should break when its builder moves.");
            var built = _maps.GetAnchoredEntities(_upper, SComp<Robust.Shared.Map.Components.MapGridComponent>(_upper), new Vector2i(1, 0))
                .Any(uid => SEntMan.HasComponent<XenoConstructComponent>(uid));
            Assert.That(built, Is.EqualTo(remote), "The remote order must actually complete at the original tile.");
            SEntMan.DeleteEntity(queen);
        });
    }

#pragma warning restore RA0002
}
