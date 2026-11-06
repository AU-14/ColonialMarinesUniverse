using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.NightVision;
using Content.Shared.CMU14.Clothing.NightVision;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Clothing;

/// <summary>
///     The M1A1 visor glows on its wearer while working, cuts out and stays off until a screwdriver fixes it.
/// </summary>
[TestFixture]
public sealed class CMUNightVisionMalfunctionTest : InteractionTest
{
    private const string Goggles = "CMUHelmetGarbNightVisionM1A1";

    [Test]
    public async Task BrokenGogglesStayOffUntilScrewdriverRepair()
    {
        await SpawnTarget(Goggles);
        var goggles = STarget!.Value;
        var toggle = SEntMan.System<ItemToggleSystem>();
        var malfunction = SEntMan.System<CMUNightVisionMalfunctionSystem>();

        await Server.WaitAssertion(() =>
        {
            Assert.That(toggle.TryActivate(goggles, predicted: false), Is.True, "Working goggles switch on.");

            var comp = SEntMan.GetComponent<CMUNightVisionMalfunctionComponent>(goggles);
            malfunction.Malfunction((goggles, comp));

            Assert.That(comp.Broken, Is.True);
            Assert.That(toggle.IsActivated(goggles), Is.False, "Cutting out switches them off.");
            Assert.That(toggle.TryActivate(goggles, predicted: false), Is.False, "Broken goggles won't switch on.");
        });

        await InteractUsing("CMScrewdriver");

        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUNightVisionMalfunctionComponent>(goggles);
            Assert.That(comp.Broken, Is.False, "A screwdriver fixes them.");
            Assert.That(toggle.TryActivate(goggles, predicted: false), Is.True, "Fixed goggles switch on again.");
        });
    }

    [Test]
    public async Task WearerGlowsOnlyWhileGogglesWork()
    {
        EntityUid goggles = default;
        EntityUid wearer = default;
        var toggle = SEntMan.System<ItemToggleSystem>();

        await Server.WaitAssertion(() =>
        {
            // The interaction test's own player has no head slot, so a regular marine wears the helmet.
            var coords = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            wearer = SEntMan.SpawnEntity("CMMobHuman", coords);
            var helmet = SEntMan.SpawnEntity("ArmorHelmetM10", coords);
            goggles = SEntMan.SpawnEntity(Goggles, coords);

            Assert.That(SEntMan.System<SharedStorageSystem>().Insert(helmet, goggles, out _, playSound: false), Is.True);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(wearer, helmet, "head", silent: true, force: true), Is.True);
            Assert.That(toggle.TryActivate(goggles, predicted: false), Is.True);
        });

        await RunTicks(30);

        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUNightVisionMalfunctionComponent>(goggles);
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(goggles).User, Is.EqualTo(wearer));
            Assert.That(comp.Glow, Is.Not.Null, "The wearer glows while the goggles work.");
            Assert.That(SEntMan.GetComponent<TransformComponent>(comp.Glow!.Value).ParentUid, Is.EqualTo(wearer));

            SEntMan.System<CMUNightVisionMalfunctionSystem>().Malfunction((goggles, comp));
        });

        await RunTicks(30);

        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUNightVisionMalfunctionComponent>(goggles);
            Assert.That(comp.Glow, Is.Null, "The glow goes out with the goggles.");
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(goggles).User, Is.Null, "Night vision goes too.");
        });
    }
}
