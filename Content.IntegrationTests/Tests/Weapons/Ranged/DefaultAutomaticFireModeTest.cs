using Content.Shared.Weapons.Ranged.Components;

namespace Content.IntegrationTests.Tests.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(GunComponent))]
public sealed class DefaultAutomaticFireModeTest
{
    private static readonly string[] AutomaticWeapons =
    {
        "WeaponPistolViper",
        "WeaponPulseCarbine",
    };

    [Test]
    public async Task AutomaticWeaponsStartInFullAuto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            foreach (var prototype in AutomaticWeapons)
            {
                var weapon = entMan.SpawnEntity(prototype, map.GridCoords);
                var gun = entMan.GetComponent<GunComponent>(weapon);
                var availableModes = gun.AvailableModes;

                Assert.Multiple(() =>
                {
                    Assert.That(gun.SelectedMode, Is.EqualTo(SelectiveFire.FullAuto), prototype);
                    Assert.That((availableModes & SelectiveFire.SemiAuto) != 0, Is.True, prototype);
                    Assert.That((availableModes & SelectiveFire.FullAuto) != 0, Is.True, prototype);
                });
            }

            var control = entMan.SpawnEntity("WeaponPistolMk58", map.GridCoords);
            Assert.That(
                entMan.GetComponent<GunComponent>(control).SelectedMode,
                Is.EqualTo(SelectiveFire.SemiAuto),
                "The base pistol default must remain semi-auto.");
        });

        await pair.CleanReturnAsync();
    }
}
