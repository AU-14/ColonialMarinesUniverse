using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-112: the weapon-choice menu is filled by the server from the bracer's own gear, and a choice installs it
[TestFixture]
public sealed class BadBloodWeaponChoiceTest
{
    private const string BadBloodBracer = "CMUYautjaBadBloodBracer";

    [Test]
    public async Task MenuOpensWithServerChoicesAndAChoiceInstallsTheGear()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid? previousAttached = null;
        var spawned = new List<EntityUid>();

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var attachments = entMan.System<YautjaAttachmentSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                var badBlood = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                spawned.Add(badBlood);
                entMan.EnsureComponent<YautjaBadBloodComponent>(badBlood);
                if (inventory.TryUnequip(badBlood, "gloves", out var oldBracer, silent: true, force: true))
                    entMan.DeleteEntity(oldBracer.Value);

                server.PlayerMan.SetAttachedEntity(session, badBlood);

                var bracer = entMan.SpawnEntity(BadBloodBracer, map.GridCoords);
                spawned.Add(bracer);
                var choice = entMan.GetComponent<YautjaBadBloodGearChoiceComponent>(bracer);
                var gear = entMan.GetComponent<YautjaGearContainerComponent>(bracer);

                Assert.That(inventory.TryEquip(badBlood, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(ui.IsUiOpen(bracer, YautjaBadBloodWeaponChoiceUI.Key, badBlood), Is.True,
                    "Putting the bracer on must open the weapon-choice menu.");
                Assert.That(
                    ui.TryGetUiState<YautjaBadBloodWeaponChoiceBuiState>(bracer, YautjaBadBloodWeaponChoiceUI.Key, out var state),
                    Is.True,
                    "The server must send the menu its choices.");

                Assert.Multiple(() =>
                {
                    Assert.That(state!.Choices.Select(c => c.Kind), Is.EqualTo(choice.Choices),
                        "The menu must offer exactly the bracer's choices.");
                    foreach (var entry in state.Choices)
                    {
                        Assert.That(entry.Prototype, Is.EqualTo(gear.GearPrototypes[entry.Kind]),
                            $"The menu must show the gear the bracer really installs for {entry.Kind}.");
                    }

                    Assert.That(state.PendingChoice, Is.Null);
                    Assert.That(state.Choices, Is.EqualTo(attachments.GetBadBloodWeaponChoices((bracer, choice))));
                });

                // the caster is not on the menu, so asking for it must not burn the pick
                SendChoice(entMan, bracer, badBlood, YautjaGearKind.Caster);
                SendChoice(entMan, bracer, badBlood, YautjaGearKind.Caster);
                Assert.That(choice.Chosen, Is.False, "A weapon that was never offered must not count as the choice.");

                // first click asks for confirmation, the second one commits
                SendChoice(entMan, bracer, badBlood, YautjaGearKind.Scimitar);
                Assert.Multiple(() =>
                {
                    Assert.That(choice.Chosen, Is.False);
                    Assert.That(choice.PendingChoice, Is.EqualTo(YautjaGearKind.Scimitar));
                    Assert.That(gear.Gear.ContainsKey(YautjaGearKind.Scimitar), Is.False,
                        "Nothing is installed until the choice is confirmed.");
                });

                SendChoice(entMan, bracer, badBlood, YautjaGearKind.Scimitar);
                Assert.Multiple(() =>
                {
                    Assert.That(choice.Chosen, Is.True);
                    Assert.That(gear.Gear.TryGetValue(YautjaGearKind.Scimitar, out var installed), Is.True,
                        "Confirming the choice must install the weapon.");
                    Assert.That(entMan.GetComponent<MetaDataComponent>(installed).EntityPrototype?.ID,
                        Is.EqualTo(gear.GearPrototypes[YautjaGearKind.Scimitar].Id));
                    Assert.That(gear.InstalledGear, Does.Contain(installed));
                    Assert.That(gear.Gear.ContainsKey(YautjaGearKind.WristBlades), Is.False);
                    Assert.That(gear.Gear.ContainsKey(YautjaGearKind.ChainGauntlet), Is.False);
                    Assert.That(ui.IsUiOpen(bracer, YautjaBadBloodWeaponChoiceUI.Key, badBlood), Is.False,
                        "The menu closes once the weapon is chosen.");
                });

                // the pick is final
                SendChoice(entMan, bracer, badBlood, YautjaGearKind.WristBlades);
                SendChoice(entMan, bracer, badBlood, YautjaGearKind.WristBlades);
                Assert.That(gear.Gear.ContainsKey(YautjaGearKind.WristBlades), Is.False,
                    "A second weapon must not be installed after the choice is made.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in spawned)
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    private static void SendChoice(IEntityManager entMan, EntityUid bracer, EntityUid actor, YautjaGearKind kind)
    {
        var msg = new YautjaBadBloodWeaponChoiceMsg(kind)
        {
            UiKey = YautjaBadBloodWeaponChoiceUI.Key,
            Actor = actor,
        };
        entMan.EventBus.RaiseLocalEvent(bracer, (object) msg);
    }
}
