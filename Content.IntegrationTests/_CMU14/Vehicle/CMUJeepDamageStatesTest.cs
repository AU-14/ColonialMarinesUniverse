using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A jeep's panels show the damage that wore them, by type: gunfire holes in the door a burst hit,
/// burns on the hood a fire caught, dents where a crash struck. The art for every level is there on
/// every jeep.
/// </summary>
[TestFixture]
public sealed class CMUJeepDamageStatesTest : GameTest
{
    private async Task<(TestMapData Map, EntityUid Jeep)> SpawnJeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);
        return (map, jeep);
    }

    private int? Level(EntityUid jeep, string layer)
    {
        var comp = SEntMan.GetComponent<CMUVehicleDamageStatesComponent>(jeep);
        var stage = comp.Stages.Single(s => s.Layer == layer);
        return SEntMan.System<CMUVehicleDamageStatesSystem>().GetLevel(jeep, comp, stage);
    }

    private void Hit(EntityUid jeep, EntityCoordinates from, string type, float amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        SEntMan.System<DamageableSystem>().TryChangeDamage(jeep, damage, origin: SEntMan.SpawnEntity(null, from));
    }

    [Test]
    public async Task PanelsShowWhatWoreThem()
    {
        var (map, jeep) = await SpawnJeep();
        await Server.WaitAssertion(() =>
        {
            var stages = SEntMan.GetComponent<CMUVehicleDamageStatesComponent>(jeep).Stages.Select(s => s.Layer).ToList();
            Assert.That(stages.Select(s => Level(jeep, s)), Is.All.Null, "A new jeep shows no damage.");

            // Facing south: the driver's door is towards +x, the hood towards -y.
            Hit(jeep, new EntityCoordinates(map.Grid, 2.5f, 0.5f), "Piercing", 300);
            Assert.That(Level(jeep, "door_driver_dmg_holes"), Is.Not.Null, "Gunfire from the driver's side holes the driver's door.");
            Assert.That(Level(jeep, "door_driver_dmg_burnt"), Is.Null, "Gunfire doesn't burn it.");
            Assert.That(Level(jeep, "door_passenger_dmg_holes"), Is.Null, "The far door is untouched.");

            Hit(jeep, new EntityCoordinates(map.Grid, 0.5f, -2.5f), "Heat", 300);
            Assert.That(Level(jeep, "hood_dmg_burnt"), Is.Not.Null, "Fire from ahead burns the hood.");
            Assert.That(Level(jeep, "hood_dmg_holes"), Is.Null, "Fire doesn't hole it.");
            Assert.That(Level(jeep, "door_driver_dmg_burnt"), Is.Null, "Fire on the hood doesn't burn the door.");
        });
    }

    [Test]
    public async Task CrashesDentThePanelTheyHit()
    {
        var (_, jeep) = await SpawnJeep();
        await Server.WaitAssertion(() =>
        {
            // Head on: the contact is at the front of the hull, -y facing south.
            var crash = new CMUVehicleCrashedEvent(40f, true, 1f, new Vector2(0f, -1f));
            SEntMan.EventBus.RaiseLocalEvent(jeep, ref crash);
            Assert.That(Level(jeep, "hood_dmg_dented"), Is.Not.Null, "A head on crash dents the hood.");
            Assert.That(Level(jeep, "hood_dmg_holes"), Is.Null);
            Assert.That(Level(jeep, "tailgate_dmg_dented"), Is.Null, "The back is untouched.");
        });
    }

    [Test]
    public async Task ClientsDrawTheDamageAtItsLevel()
    {
        var (map, jeep) = await SpawnJeep();
        int? level = null;
        await Server.WaitPost(() =>
        {
            Hit(jeep, new EntityCoordinates(map.Grid, 0.5f, -2.5f), "Heat", 300);
            level = Level(jeep, "hood_dmg_burnt");
        });
        await RunTicksSync(10);
        Assert.That(level, Is.Not.Null);

        await Client.WaitAssertion(() =>
        {
            var sprites = Client.System<SpriteSystem>();
            var client = ToClientUid(jeep);
            var sprite = CEntMan.GetComponent<SpriteComponent>(client);
            Assert.That(sprites.LayerMapTryGet((client, sprite), "hood_dmg_burnt", out var index, false), Is.True);
            Assert.That(sprite[index].Visible, Is.True, "The hood's burns show while it is shut.");
            Assert.That(sprites.LayerGetRsiState((client, sprite), index).Name, Is.EqualTo($"hood_dmg_burnt_{level}"));
        });
    }

    [Test]
    public async Task EveryJeepHasItsDamageArt()
    {
        await Client.WaitAssertion(() =>
        {
            var cache = Client.ResolveDependency<IResourceCache>();
            foreach (var proto in CProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract ||
                    !proto.TryComp<CMUVehicleDamageStatesComponent>(out var damage, CEntMan.ComponentFactory) ||
                    !proto.TryComp<SpriteComponent>(out var sprite, CEntMan.ComponentFactory) ||
                    sprite.BaseRSI is not { } rsi)
                {
                    continue;
                }

                var overlayRsi = proto.TryComp<CMUVehicleOverlayComponent>(out var overlay, CEntMan.ComponentFactory) &&
                                 overlay.Prototype is { } overlayProto &&
                                 CProtoMan.Index<EntityPrototype>(overlayProto).TryComp<SpriteComponent>(out var overSprite, CEntMan.ComponentFactory)
                    ? overSprite.BaseRSI
                    : null;
                foreach (var stage in damage.Stages)
                {
                    foreach (var level in stage.Levels)
                    {
                        Assert.That(rsi.TryGetState($"{stage.Layer}_{level}", out _), Is.True, $"{proto.ID}: {stage.Layer}_{level}");

                        if (overlayRsi != null)
                            Assert.That(overlayRsi.TryGetState($"{stage.Layer}_overlay_{level}", out _), Is.True, $"{proto.ID} overlay: {stage.Layer}_overlay_{level}");
                    }
                }
            }
        });
    }
}
