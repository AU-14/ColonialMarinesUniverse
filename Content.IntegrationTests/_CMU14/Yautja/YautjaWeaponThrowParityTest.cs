using System.Collections.Generic;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaWeaponThrowParityTest
{
    private static readonly (string Prototype, float Range)[] ExplicitThrowRanges =
    {
        ("CMUYautjaHarpoon", 14),
        ("CMUYautjaCombistick", 14),
        ("CMUYautjaWarAxe", 14),
        ("CMUYautjaCeremonialDagger", 14),
        ("CMUYautjaDuellingClub", 14),
        ("CMUYautjaDuellingHatchet", 14),
    };

    [Test]
    public async Task ExplicitThrowRangesAreAppliedToCtrlQThrowPath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                foreach (var (prototype, expectedRange) in ExplicitThrowRanges)
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
