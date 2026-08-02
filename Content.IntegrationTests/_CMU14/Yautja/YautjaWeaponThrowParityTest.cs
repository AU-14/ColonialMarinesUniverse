using System.Collections.Generic;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaWeaponThrowParityTest
{
    private static readonly (string Prototype, float Range)[] ExplicitCmss13Ranges =
    {
        ("CMUYautjaHarpoon", 4),
        ("CMUYautjaCombistick", 4),
        ("CMUYautjaWarAxe", 4),
        ("CMUYautjaCeremonialDagger", 6),
        ("CMUYautjaDuellingClub", 7),
        ("CMUYautjaDuellingHatchet", 4),
    };

    [Test]
    public async Task ExplicitCmss13ThrowRangesAreAppliedToCtrlQThrowPath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                foreach (var (prototype, expectedRange) in ExplicitCmss13Ranges)
                {
                    var item = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
                    spawned.Add(item);

                    Assert.That(entMan.TryGetComponent<ItemThrowRangeComponent>(item, out var range), Is.True, prototype);
                    Assert.That(range.Range, Is.EqualTo(expectedRange), prototype);
                }
            }
            finally
            {
                foreach (var item in spawned)
                {
                    if (!entMan.Deleted(item))
                        entMan.DeleteEntity(item);
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
