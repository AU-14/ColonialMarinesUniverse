using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Damage.Components;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class HunterShipHarpoonTest
{
    private static readonly string[] PlacedHarpoonPrototypes =
    [
        "CMUHunterShipPlacedCMUYautjaSpikeLauncherSpikeSouthOffset0x9",
        "CMUHunterShipPlacedCMUYautjaSpikeLauncherSpikeSouthOffset0xNeg9",
        "CMUHunterShipPlacedCMUYautjaSpikeLauncherSpikeSouthOffset1xNeg11",
        "CMUHunterShipPlacedCMUYautjaSpikeLauncherSpikeSouthOffset9x0",
        "CMUHunterShipPlacedCMUYautjaSpikeLauncherSpikeSouthOffsetNeg12x7",
        "CMUHunterShipPlacedCMUYautjaSpikeLauncherSpikeSouthOffsetNeg3x2",
        "CMUHunterShipPlacedCMUYautjaSpikeLauncherSpikeSouthOffsetNeg5x6",
        "CMUHunterShipPlacedCMUYautjaSpikeLauncherSpikeSouthOffsetNeg9x0",
    ];

    [Test]
    public async Task PlacedHunterShipHarpoonsAreNotFirearms()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            foreach (var prototype in PlacedHarpoonPrototypes)
            {
                var harpoon = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(entMan.HasComponent<GunComponent>(harpoon), Is.False,
                            $"{prototype} is a placed harpoon and must not inherit a firing weapon.");
                        Assert.That(entMan.HasComponent<BallisticAmmoProviderComponent>(harpoon), Is.False,
                            $"{prototype} must not carry ammunition for a launcher.");
                        Assert.That(entMan.HasComponent<DamageOtherOnHitComponent>(harpoon), Is.True,
                            $"{prototype} must retain normal harpoon throw damage.");
                        Assert.That(entMan.HasComponent<ItemThrowRangeComponent>(harpoon), Is.True,
                            $"{prototype} must retain the harpoon's manual throw range.");
                    });
                }
                finally
                {
                    if (!entMan.Deleted(harpoon))
                        entMan.DeleteEntity(harpoon);
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
