using System.Linq;
using System.Reflection;
using Content.Client._RMC14.Dropship.Weapon;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DropshipInteriorWeaponMenuTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task InteriorWeaponsRemainSelectableBeyondFourExteriorWeapons()
    {
        var map = await Pair.CreateTestMap();
        NetEntity ownerNet = default;
        var expected = new List<NetEntity>();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            var owner = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EnsureComponent<DropshipTerminalWeaponsComponent>(owner);
            ownerNet = SEntMan.GetNetEntity(owner);
            var containers = SEntMan.System<SharedContainerSystem>();
            for (var i = 0; i < 4; i++)
            {
                var point = SEntMan.SpawnEntity("CMAlamoWall61", map.GridCoords);
                var weapon = SEntMan.SpawnEntity("RMCDropshipAttachmentGau21Cannon", map.GridCoords);
                var slotId = SEntMan.GetComponent<DropshipWeaponPointComponent>(point).WeaponContainerSlotId;
                Assert.That(containers.Insert(weapon,
                    containers.EnsureContainer<ContainerSlot>(point, slotId)), Is.True);
                expected.Add(SEntMan.GetNetEntity(weapon));
            }

            foreach (var prototype in new[]
                     {
                         "CMUDropshipAttachmentBOFORS",
                         "CMUDropshipAttachmentHowitzer",
                         "CMUDropshipAttachmentJDAMBombClamp",
                     })
            {
                var point = SEntMan.SpawnEntity("AU14InteriorWeaponAttachmentPoint", map.GridCoords);
                var weapon = SEntMan.SpawnEntity(prototype, map.GridCoords);
                var slotId = SEntMan.GetComponent<DropshipUtilityPointComponent>(point).UtilitySlotId;
                Assert.That(containers.Insert(weapon,
                    containers.EnsureContainer<ContainerSlot>(point, slotId)), Is.True);
                expected.Add(SEntMan.GetNetEntity(weapon));
            }
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var bui = new DropshipWeaponsBui(CEntMan.GetEntity(ownerNet), DropshipTerminalWeaponsUi.Key);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var collect = typeof(DropshipWeaponsBui).GetMethod("TryGetWeapons", flags)!;
            var window = new DropshipWeaponsWindow();
            typeof(DropshipWeaponsBui).GetProperty("Window", flags | BindingFlags.DeclaredOnly)!.SetValue(bui, window);
            foreach (var first in new[] { true, false })
            {
                var pageField = typeof(DropshipWeaponsBui)
                    .GetField(first ? "_screenOneWeaponPage" : "_screenTwoWeaponPage", flags)!;
                var displayed = new List<NetEntity>();
                for (var page = 0; page < 2; page++)
                {
                    pageField.SetValue(bui, page);
                    object[] arguments = { first, null, null, null, null, null, null, null, null, null };
                    var result = collect.Invoke(bui, arguments);
                    Assert.That(result, Is.TypeOf<List<DropshipWeaponsButtonData>>());
                    var allWeapons = (List<DropshipWeaponsButtonData>) result!;
                    Assert.That(allWeapons.Select(button => button.Weapon), Is.EquivalentTo(expected),
                        "Equipment and firemission summaries must include every mounted weapon.");
                    foreach (var argument in arguments.Skip(1).Take(4))
                    {
                        if (argument is DropshipWeaponsButtonData { Weapon: { } weapon })
                            displayed.Add(weapon);
                    }
                }

                Assert.That(displayed, Is.EquivalentTo(expected),
                    "Both terminal screens must expose every exterior and interior weapon across pages.");

                var terminal = CEntMan.GetComponent<DropshipTerminalWeaponsComponent>(CEntMan.GetEntity(ownerNet));
                foreach (var state in new[]
                         {
                             DropshipTerminalWeaponsScreen.Equip,
                             DropshipTerminalWeaponsScreen.StrikeWeapon,
                             DropshipTerminalWeaponsScreen.FireMissionEdit,
                         })
                {
                    pageField.SetValue(bui, 0);
                    var screenState = new DropshipTerminalWeaponsComponent.Screen { State = state };
                    // Configure only the local UI state, without issuing server-side weapon commands.
#pragma warning disable RA0002
                    if (first)
                        terminal.ScreenOne = screenState;
                    else
                        terminal.ScreenTwo = screenState;
#pragma warning restore RA0002
                    bui.Refresh();
                    var screen = first ? window.ScreenOne : window.ScreenTwo;
                    Assert.That(screen.BottomRow.Five.Disabled, Is.False, $"{state} next weapon page");
                    var next = (Action<BaseButton.ButtonEventArgs>) typeof(DropshipWeaponsButton)
                        .GetField("_onPressed", flags)!.GetValue(screen.BottomRow.Five)!;
                    next(null!);
                    Assert.That(pageField.GetValue(bui), Is.EqualTo(1), $"{state} advances the weapon page");
                    Assert.That(screen.BottomRow.Four.Disabled, Is.False, $"{state} previous weapon page");
                    Assert.That(screen.BottomRow.Five.Disabled, Is.True, $"{state} final weapon page");
                }
            }
            window.Dispose();
        });
    }
}
