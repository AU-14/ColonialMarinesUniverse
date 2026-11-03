using System.Numerics;
using Content.Client.CMU14.Fluids;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Fluids;
using Content.Shared.CMU14.Fluids;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU.Industry;

// Exercise actual network state: a raw appearance list is not registered by the serializer.
public sealed class CMUFluidPipeFlowTest : InteractionTest
{
    [Test]
    public async Task PortMarkersReachClientAndClearWhenDisconnected()
    {
        await SetTile(Plating, TargetCoords);
        await SpawnTarget("RUMCOilBarrel");
        await PlaceInHands("CMScrewdriver");
        EntityUid pipe = default;
        NetCoordinates pipeCoords = default;
        await Server.WaitPost(() => pipeCoords = SEntMan.GetNetCoordinates(
            SComp<TransformComponent>(STarget!.Value).Coordinates.Offset(new Vector2(0, 1))));
        await SetTile(Plating, pipeCoords, MapData.Grid);
        await Server.WaitAssertion(() =>
        {
            pipe = SSpawnAtPosition("RUMCBrassPipe", SEntMan.GetCoordinates(pipeCoords));
            var machine = STarget!.Value;
            Assert.That(Server.System<FluidLinkSystem>().TryConnect(
                (machine, SComp<FluidPortsComponent>(machine)), "liquid_out", pipe, SPlayer), Is.True);
        });
        await RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var arrows = CComp<CMUFluidPipeFlowComponent>(ToClientUid(pipe)).Arrows;
            Assert.That(arrows, Is.EqualTo(new[] { new CMUFluidPipeArrow(new Vector2i(0, -1), true, false) }));
        });
        await Server.WaitAssertion(() =>
        {
            var machine = STarget!.Value;
            Server.System<FluidSystem>().CutPorts(machine);
            Assert.That(Server.System<FluidLinkSystem>().TryConnect(
                (machine, SComp<FluidPortsComponent>(machine)), "liquid_in", pipe, SPlayer), Is.True);
        });
        await RunUntilSynced();
        await Client.WaitAssertion(() => Assert.That(CComp<CMUFluidPipeFlowComponent>(ToClientUid(pipe)).Arrows,
            Is.EqualTo(new[] { new CMUFluidPipeArrow(new Vector2i(0, -1), false, false) })));
        await Server.WaitPost(() => Server.System<SharedTransformSystem>().SetLocalRotation(pipe, Angle.FromDegrees(90)));
        await RunUntilSynced();
        await Client.WaitAssertion(() => Assert.That(CEntMan.HasComponent<CMUFluidPipeFlowComponent>(ToClientUid(pipe)), Is.False));
        await Server.WaitAssertion(() =>
        {
            var machine = STarget!.Value;
            Transform.Unanchor(pipe);
            Transform.SetCoordinates(pipe, SComp<TransformComponent>(machine).Coordinates);
            Transform.AnchorEntity(pipe);
            Assert.That(Server.System<FluidLinkSystem>().TryConnect(
                (machine, SComp<FluidPortsComponent>(machine)), "liquid_out", pipe, SPlayer), Is.True);
        });
        await RunUntilSynced();
        await Client.WaitAssertion(() => Assert.That(CComp<CMUFluidPipeFlowComponent>(ToClientUid(pipe)).Arrows,
            Is.EqualTo(new[] { new CMUFluidPipeArrow(new Vector2i(1, 0), true, true) })));
        await Server.WaitPost(() => Server.System<FluidSystem>().CutPorts(STarget!.Value));
        await RunUntilSynced();
        await Client.WaitAssertion(() => Assert.That(CEntMan.HasComponent<CMUFluidPipeFlowComponent>(ToClientUid(pipe)), Is.False));
    }
}
