using System.Numerics;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared._RMC14.Xenonids.Lunge; // CMU14
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Pulling.Components; // CMU14
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoLeapTest
{
    [Test]
    public async Task LeapTravelsConfiguredRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid xeno = default;
        Vector2 origin = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                xeno = entMan.SpawnEntity("CMXenoLurker", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
                origin = entMan.System<SharedTransformSystem>().GetMapCoordinates(xeno).Position;

                var target = map.GridCoords.Offset(new Vector2(10.5f, 0.5f));
                var leap = new XenoLeapDoAfterEvent(entMan.GetNetCoordinates(target));
                leap.DoAfter = new DoAfter(
                    0,
                    new DoAfterArgs(entMan, xeno, TimeSpan.Zero, leap, xeno),
                    TimeSpan.Zero);

                entMan.EventBus.RaiseLocalEvent(xeno, leap);

                Assert.That(entMan.HasComponent<XenoLeapingComponent>(xeno), Is.True);
            });

            await pair.RunSeconds(0.5f);

            await server.WaitAssertion(() =>
            {
                var position = server.EntMan.System<SharedTransformSystem>().GetMapCoordinates(xeno).Position;
                var displacement = position - origin;
                Assert.That(displacement.X, Is.EqualTo(6f).Within(0.25f));
                Assert.That(displacement.Y, Is.EqualTo(0f).Within(0.25f));
                Assert.That(server.EntMan.HasComponent<XenoLeapingComponent>(xeno), Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (server.EntMan.EntityExists(xeno))
                    server.EntMan.DeleteEntity(xeno);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LeapDoAfterDoesNotStartLeapingWhenPlasmaSpendFails()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var plasmaSystem = entMan.System<XenoPlasmaSystem>();
            var xeno = entMan.SpawnEntity("CMXenoRavager", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            try
            {
                var plasma = entMan.GetComponent<XenoPlasmaComponent>(xeno);
                plasmaSystem.SetPlasma((xeno, plasma), 0);

                var target = map.GridCoords.Offset(new Vector2(5, 0.5f));
                var leap = new XenoLeapDoAfterEvent(entMan.GetNetCoordinates(target));
                leap.DoAfter = new DoAfter(
                    0,
                    new DoAfterArgs(entMan, xeno, TimeSpan.Zero, leap, xeno),
                    TimeSpan.Zero);

                entMan.EventBus.RaiseLocalEvent(xeno, leap);

                Assert.That(entMan.HasComponent<XenoLeapingComponent>(xeno), Is.False);
            }
            finally
            {
                entMan.DeleteEntity(xeno);
            }
        });

        await pair.CleanReturnAsync();
    }

    // CMU14 method
    [Test]
    public async Task LeapKnocksDownStandingMarine()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid parasite = default;
        EntityUid marine = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                parasite = entMan.SpawnEntity("CMXenoParasite", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
                marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));

                var target = map.GridCoords.Offset(new Vector2(2.5f, 0.5f));
                var leap = new XenoLeapDoAfterEvent(entMan.GetNetCoordinates(target));
                leap.DoAfter = new DoAfter(
                    0,
                    new DoAfterArgs(entMan, parasite, TimeSpan.Zero, leap, parasite),
                    TimeSpan.Zero);

                entMan.EventBus.RaiseLocalEvent(parasite, leap);

                Assert.That(entMan.HasComponent<XenoLeapingComponent>(parasite), Is.True);
            });

            await pair.RunSeconds(0.5f);

            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.HasComponent<LeapIncapacitatedComponent>(marine), Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (server.EntMan.EntityExists(parasite))
                    server.EntMan.DeleteEntity(parasite);
            });
        }

        await pair.CleanReturnAsync();
    }

    // CMU14 method
    [Test]
    public async Task LungeGrabsTargetAndPullsIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid warrior = default;
        EntityUid marine = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                warrior = entMan.SpawnEntity("CMXenoWarrior", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
                marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));

                var lunge = new XenoLungeActionEvent
                {
                    Entity = marine,
                    Target = map.GridCoords.Offset(new Vector2(2.5f, 0.5f)),
                };
                entMan.EventBus.RaiseLocalEvent(warrior, lunge);
            });

            await pair.RunSeconds(0.5f);

            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.TryGetComponent<PullerComponent>(warrior, out var puller), Is.True);
                Assert.That(puller!.Pulling, Is.EqualTo(marine));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (server.EntMan.EntityExists(warrior))
                    server.EntMan.DeleteEntity(warrior);
            });
        }

        await pair.CleanReturnAsync();
    }
}
