using System.Numerics;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoLeapTest
{
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
}
