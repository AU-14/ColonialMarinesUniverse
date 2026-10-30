using Content.Shared._RMC14.Fireman;
using Content.Shared.CMU14.PowerLoader;
using Content.Shared.DragDrop;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

[TestFixture]
public sealed class CMUWendigoUtilityTest
{
    private const string Lab = "CMUWendigoLab";
    private const string Human = "CMMobHuman";
    private const string Crate = "RMCCrateAlpha";

    [Test]
    public async Task LabWendigoDragsAndCarriesBodies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid dragger = default, body = default, carrier = default, victim = default;
        float walkBefore = 0f, sprintBefore = 0f;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            dragger = entMan.SpawnEntity(Lab, map.GridCoords);
            body = entMan.SpawnEntity(Human, map.GridCoords);
            carrier = entMan.SpawnEntity(Lab, map.GridCoords);
            victim = entMan.SpawnEntity(Human, map.GridCoords);
            entMan.System<MobStateSystem>().ChangeMobState(body, MobState.Dead);
        });
        await pair.RunTicksSync(5);

        // (a) dragging a dead body costs no speed: no 0.95x puller slowdown, no SlowOnPull.
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var speed = entMan.GetComponent<MovementSpeedModifierComponent>(dragger);
            walkBefore = speed.CurrentWalkSpeed;
            sprintBefore = speed.CurrentSprintSpeed;
            var pulling = entMan.System<PullingSystem>();
            Assert.That(pulling.TryStartPull(dragger, body), Is.True);
            Assert.That(pulling.TryStartPull(carrier, victim), Is.True);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var speed = server.EntMan.GetComponent<MovementSpeedModifierComponent>(dragger);
            Assert.That(speed.CurrentWalkSpeed, Is.EqualTo(walkBefore).Within(0.01f), "dragging a body slowed the walk");
            Assert.That(speed.CurrentSprintSpeed, Is.EqualTo(sprintBefore).Within(0.01f), "dragging a body slowed the sprint");
        });

        // (b) aggressive grab, then drag the human onto itself to fireman carry, same as a player does.
        await pair.RunTicksSync(pair.SecondsToTicks(5));
        await server.WaitPost(() =>
        {
            var toggle = new RMCPullToggleEvent();
            server.EntMan.EventBus.RaiseLocalEvent(carrier, ref toggle);
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            Assert.That(server.EntMan.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), Is.True,
                "the wendigo couldn't grab aggressively");
            var drag = new DragDropDraggedEvent(carrier, carrier);
            server.EntMan.EventBus.RaiseLocalEvent(victim, ref drag);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(8));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<FiremanCarriableComponent>(victim).BeingCarried, Is.True, "the carry never started");
            Assert.That(entMan.GetComponent<CanFiremanCarryComponent>(carrier).Carrying, Is.EqualTo(victim));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LabWendigoLiftsCratesAtFullSpeed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wendigo = default, crate = default;
        var sprintBefore = 0f;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            wendigo = entMan.SpawnEntity(Lab, map.GridCoords);
            crate = entMan.SpawnEntity(Crate, map.GridCoords);
        });
        await pair.RunTicksSync(5);

        // (c) the crate goes into a hand despite WhitelistPickup and the missing ItemComponent.
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            sprintBefore = entMan.GetComponent<MovementSpeedModifierComponent>(wendigo).CurrentSprintSpeed;
            Assert.That(entMan.System<CMULargeObjectGrabberSystem>().TryStartGrab(wendigo, crate), Is.True);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            Assert.That(hands.IsHolding(wendigo, crate), Is.True, "the crate never reached a hand");

            var sprint = entMan.GetComponent<MovementSpeedModifierComponent>(wendigo).CurrentSprintSpeed;
            Assert.That(sprint, Is.EqualTo(sprintBefore).Within(0.01f), "the wendigo got the power-loader cargo crawl");

            Assert.That(hands.TryDrop(wendigo, crate), Is.True, "a lifted crate must drop like any held item");
            Assert.That(entMan.System<SharedContainerSystem>().IsEntityInContainer(crate), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
