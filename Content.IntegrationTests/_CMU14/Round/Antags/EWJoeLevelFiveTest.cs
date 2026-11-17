using System.Linq;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared._RMC14.Projectiles.Reflect;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Stunnable;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Phase E6, level 5 (MELDING): the announcement flag, the level-5 kit and its stand-down cleanup,
/// THE LIGHT's envelope on a viewer who faces the Joe, and Saturate's cone.
/// </summary>
[TestFixture]
public sealed class EWJoeLevelFiveTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Rifle = "AU14WeaponRifleM41AMK2Tactical";
    private const string Armor = "CMArmorM3Medium";
    private const string Helmet = "ArmorHelmetM10";

    [Test]
    public async Task TheLightEnvelopsAViewerWhoFacesTheJoe()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, viewer = default, bystander = default;
        await server.WaitAssertion(() =>
        {
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            entities.EnsureComponent<EWJoeLightComponent>(joe);

            var spot = EWJoeTestHelpers.TileEast(entities, map, 2);
            viewer = entities.SpawnEntity(Human, spot);
            bystander = entities.SpawnEntity(Human, spot);
            EWJoeTestHelpers.Face(entities, viewer, joe);
            // the bystander looks the other way
            EWJoeTestHelpers.Face(entities, bystander, joe);
            var xforms = entities.System<SharedTransformSystem>();
            xforms.SetWorldRotation(bystander, xforms.GetWorldRotation(bystander) + Angle.FromDegrees(180));
        });

        await pair.RunTicksSync((int) Math.Ceiling(4.75 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.TryGetComponent(viewer, out EWJoeEnvelopedComponent? envelope), "the viewer was caught");
            Assert.Multiple(() =>
            {
                Assert.That(envelope!.ImmuneUntil, Is.GreaterThan(timing.CurTime), "the envelope filled and landed");
                Assert.That(envelope.Progress, Is.EqualTo(0f), "and reset");
                Assert.That(entities.HasComponent<StunnedComponent>(viewer) || entities.HasComponent<KnockedDownComponent>(viewer),
                    "the viewer is paralyzed");
                Assert.That(entities.HasComponent<EWJoeEnvelopedComponent>(bystander), Is.False, "looking away is safe");
            });

            // stand-down releases everyone the light was holding
            entities.RemoveComponent<EWJoeLightComponent>(joe);
            Assert.That(entities.HasComponent<EWJoeEnvelopedComponent>(viewer), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SaturateFloorsAndStripsTheConeAndCostsHeat()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var hands = entities.System<SharedHandsSystem>();
            var inventory = entities.System<InventorySystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var heat = entities.EnsureComponent<EWJoeHeatComponent>(joe);

            var targetTile = EWJoeTestHelpers.TileEast(entities, map, 2);
            var target = entities.SpawnEntity(Human, targetTile);
            var behind = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, -2));
            // playtest 4: the Joe faces away; the cone follows the tile it is aimed at, not its facing
            EWJoeTestHelpers.Face(entities, joe, behind);

            var helmet = entities.SpawnEntity(Helmet, map.GridCoords);
            var armor = entities.SpawnEntity(Armor, map.GridCoords);
            var rifle = entities.SpawnEntity(Rifle, map.GridCoords);
            Assert.That(inventory.TryEquip(target, helmet, "head", silent: true, force: true), "helmet on");
            Assert.That(inventory.TryEquip(target, armor, "outerClothing", silent: true, force: true), "armor on");
            Assert.That(hands.TryPickupAnyHand(target, rifle), "rifle in hand");

            var behindHelmet = entities.SpawnEntity(Helmet, map.GridCoords);
            Assert.That(inventory.TryEquip(behind, behindHelmet, "head", silent: true, force: true));

            var ev = new EWJoeSaturateActionEvent { Performer = joe, Target = targetTile };
            entities.EventBus.RaiseLocalEvent(joe, ev);

            var tiles = 0;
            var waves = 0;
            foreach (var meta in entities.EntityQuery<MetaDataComponent>())
            {
                if (meta.EntityPrototype?.ID == "CMUEWJoeSaturateTile")
                    tiles++;
                else if (meta.EntityPrototype?.ID == "CMUEWJoeSaturateWave")
                    waves++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, "saturate handled");
                Assert.That(waves, Is.EqualTo(1), "a shockwave rings out from the Joe");
                Assert.That(tiles, Is.GreaterThan(0), "the cone's tiles flash");
                Assert.That(heat.Heat, Is.EqualTo(comp.SaturateHeat).Within(0.01f), "it cost ten heat");
                Assert.That(entities.HasComponent<KnockedDownComponent>(target), "the target is floored");
                Assert.That(hands.EnumerateHeld(target).Any(), Is.False, "and drops what it held");
                Assert.That(inventory.TryGetSlotEntity(target, "head", out _), Is.False, "helmet torn away");
                Assert.That(inventory.TryGetSlotEntity(target, "outerClothing", out _), Is.False, "armor torn away");
                Assert.That(inventory.TryGetSlotEntity(behind, "head", out _), "the one behind the Joe is untouched");
            });

            heat.Overheated = true;
            var refused = new EWJoeSaturateActionEvent { Performer = joe, Target = targetTile };
            entities.EventBus.RaiseLocalEvent(joe, refused);
            Assert.That(refused.Handled, Is.False, "an overheated core cannot saturate");
        });

        await pair.CleanReturnAsync();
    }
}
