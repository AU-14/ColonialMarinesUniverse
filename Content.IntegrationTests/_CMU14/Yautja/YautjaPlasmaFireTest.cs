using Content.Shared.Atmos.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaPlasmaFireTest
{
    [Test]
    public async Task GenericExplosionIgnitedXenoDoesNotCreateNanFireDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid xeno = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                xeno = entMan.SpawnEntity("CMXenoRunner", MapCoordinates.Nullspace);

                var flammable = entMan.GetComponent<FlammableComponent>(xeno);
                flammable.OnFire = true;
                flammable.FireStacks = 2;
                flammable.Intensity = 0;
                flammable.Duration = 0;
                entMan.Dirty(xeno, flammable);
            });

            await server.WaitRunTicks(70);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.Deleted(xeno), Is.False);

                var flammable = entMan.GetComponent<FlammableComponent>(xeno);
                Assert.That(float.IsNaN(flammable.FireStacks), Is.False);
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                if (xeno.IsValid() && !entMan.Deleted(xeno))
                    entMan.DeleteEntity(xeno);
            });
        }

        await pair.CleanReturnAsync();
    }
}
