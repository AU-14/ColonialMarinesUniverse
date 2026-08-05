#nullable enable
using System.Linq;
using Content.Client.Examine;
using Content.Client.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Player;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(BatteryWeaponFireModesSystem))]
public sealed class BatteryWeaponFireModesLocalizationTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: BatteryFireModeLocalizationTestLow
          name: low output

        - type: entity
          id: BatteryFireModeLocalizationTestHigh
          name: high output

        - type: entity
          id: BatteryFireModeLocalizationTestWeapon
          name: test energy weapon
          components:
          - type: BatteryWeaponFireModes
            fireModes:
            - proto: BatteryFireModeLocalizationTestLow
              fireCost: 1
            - proto: BatteryFireModeLocalizationTestHigh
              fireCost: 2
        """;

    [Test]
    public async Task ExamineAndPopupUseSeparatePresentation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var serverSession = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();

        EntityUid sPlayer = default;
        EntityUid sWeapon = default;

        await server.WaitAssertion(() =>
        {
            sPlayer = sEntMan.SpawnEntity(null, map.GridCoords);
            sWeapon = sEntMan.SpawnEntity("BatteryFireModeLocalizationTestWeapon", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sPlayer), Is.True);
        });
        await pair.RunTicksSync(5);

        var cPlayer = cEntMan.GetEntity(sEntMan.GetNetEntity(sPlayer));
        var cWeapon = cEntMan.GetEntity(sEntMan.GetNetEntity(sWeapon));
        var popups = client.System<PopupSystem>();

        await client.WaitPost(() =>
        {
            popups.SetPopupsSuppressed(true);
            popups.SetPopupsSuppressed(false);
        });

        await client.WaitAssertion(() =>
        {
            Assert.That(client.Session?.AttachedEntity, Is.EqualTo(cPlayer));

            var examine = client.System<ExamineSystem>().GetExamineText(cWeapon, cPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(examine.ToString(), Is.EqualTo("Set to low output."));
                Assert.That(examine.ToMarkup(), Does.Contain("[color=yellow]low output[/color]."));
                Assert.That(popups.WorldLabels, Is.Empty);
                Assert.That(popups.CursorLabels, Is.Empty);
            });
        });

        await server.WaitAssertion(() =>
        {
            var component = sEntMan.GetComponent<BatteryWeaponFireModesComponent>(sWeapon);
            var system = sEntMan.System<BatteryWeaponFireModesSystem>();
            Assert.That(system.TrySetFireMode((sWeapon, component), 1, sPlayer), Is.True);
        });
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            Assert.That(popups.WorldLabels, Is.Empty,
                "A server-side mode change must not broadcast the client-only confirmation.");
            Assert.That(popups.CursorLabels, Is.Empty);

            var component = cEntMan.GetComponent<BatteryWeaponFireModesComponent>(cWeapon);
            var system = cEntMan.System<BatteryWeaponFireModesSystem>();
            Assert.That(component.CurrentFireMode, Is.EqualTo(1));
            Assert.That(system.TrySetFireMode((cWeapon, component), 1, cPlayer), Is.True);

            Assert.That(popups.WorldLabels, Has.Count.EqualTo(1));
            var popup = popups.WorldLabels.Single();
            Assert.Multiple(() =>
            {
                Assert.That(popup.Text, Is.EqualTo("Changed to high output"));
                Assert.That(popup.Text, Does.Not.Contain("[color"));
                Assert.That(popups.CursorLabels, Is.Empty);
            });
        });

        await pair.CleanReturnAsync();
    }
}
