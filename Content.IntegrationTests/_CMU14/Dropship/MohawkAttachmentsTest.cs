using System.Linq;
using System.Numerics;
using Content.Client.Clickable;
using Content.Server.Shuttles.Systems;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Dropship.Utility.Systems;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.PowerLoader.Events;
using Content.Shared.Buckle;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkAttachmentsTest
{
    [TestCase("omaha")]
    [TestCase("midway")]
    public async Task EnginesWeaponsAndMedevacOperateThroughTheCabinController(string variant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        var enginePoints = new List<NetEntity>();
        EntityUid ship = default;
        EntityUid medevac = default;
        EntityUid medicalPoint = default;
        EntityUid medic = default;
        EntityUid patient = default;
        EntityUid target = default;
        EntityUid ground = default;
        EntityUid gun = default;
        EntityUid gunAmmo = default;
        EntityUid chinGun = default;
        EntityUid chinAmmo = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            enginePoints.AddRange(entities.EntityQuery<DropshipEnginePointComponent>()
                .Select(p => entities.GetNetEntity(p.Owner)));
        });
        await pair.RunSeconds(1);
        async Task AssertEngineClickTargets(bool installed)
        {
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                entities.System<AppearanceSystem>().FrameUpdate(0f);
                var eye = pair.Client.ResolveDependency<IEyeManager>().CurrentEye;
                foreach (var net in enginePoints)
                {
                    var uid = entities.GetEntity(net);
                    var sprite = entities.GetComponent<SpriteComponent>(uid);
                    Assert.That(sprite.Color.A, Is.EqualTo(1f), "Hiding the entire sprite also hides its installed upgrade.");
                    var mount = sprite[DropshipPointVisualsLayers.AttachmentBase];
                    Assert.That(mount.Color.A, Is.Zero, "Only the mismatched empty mount artwork stays hidden.");
                    var attachment = sprite[DropshipPointVisualsLayers.AttachedUtility];
                    Assert.That(attachment.Visible, Is.EqualTo(installed));
                    if (installed)
                    {
                        Assert.That(attachment.Color.A, Is.EqualTo(1f));
                        Assert.That(attachment.RsiState.ToString(), Is.AnyOf("fuel_enhancer_omaha", "cooling_system_omaha"));
                        Assert.That(((SpriteComponent.Layer) attachment).Rotation, Is.EqualTo(Angle.Zero));
                        var rsi = attachment.ActualRsi!;
                        var clicks = pair.Client.ResolveDependency<IClickMapManager>();
                        var direction = sprite.DirectionOverride.Convert(rsi[attachment.RsiState].RsiDirections);
                        var pixels = 0;
                        for (var x = 0; x < rsi.Size.X; x++)
                        for (var y = 0; y < rsi.Size.Y; y++)
                        {
                            if (!clicks.IsOccluding(rsi, attachment.RsiState, direction, 0, new Vector2i(x, y)))
                                continue;
                            // Click maps add a two-pixel tolerance around the 20x12 equipment face.
                            Assert.That(x, Is.InRange(20, 43));
                            Assert.That(y, Is.InRange(24, 39), "The long engine housing must not be drawn.");
                            pixels++;
                        }
                        Assert.That(pixels, Is.GreaterThan(100), "The compact equipment face must remain visible.");
                    }
                    var transform = entities.GetComponent<TransformComponent>(uid);
                    var port = transform.LocalPosition.X < 0f;
                    Assert.That(sprite.Offset, Is.EqualTo(new Vector2(port ? 0.5f : -0.5f, 0.5f)));
                    Assert.That(transform.LocalPosition + sprite.Offset, Is.EqualTo(new Vector2(port ? -3f : 4f, -4f)),
                        "The compact engine face must be centered on the black plate in the underside artwork.");
                    var transforms = entities.System<SharedTransformSystem>();
                    var position = transforms.GetWorldPosition(uid) + transforms.GetWorldRotation(uid).RotateVec(sprite.Offset);
                    foreach (var offset in new[] { new Vector2(-0.4f, -0.4f), Vector2.Zero, new Vector2(0.4f, 0.4f) })
                    {
                        Assert.That(entities.System<ClickableSystem>().CheckClick((uid, null, sprite, null), position + offset,
                            eye, false, out _, out _, out _), Is.True, "The entire black engine servicing plate must remain clickable.");
                    }
                }
            });
        }
        await AssertEngineClickTargets(false);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var containers = entities.System<SharedContainerSystem>();
            var dropships = entities.System<SharedDropshipSystem>();
            var points = entities.GetComponent<DropshipComponent>(ship).AttachmentPoints;

            EntityUid Install(EntityUid point, string prototype)
            {
                var coordinates = entities.GetComponent<TransformComponent>(point).Coordinates;
                var loader = entities.SpawnEntity("RMCMechPowerLoader", coordinates);
                var item = entities.SpawnEntity(prototype, coordinates);
                var request = new GetAttachmentSlotEvent(entities.GetNetEntity(loader), entities.GetNetEntity(item));
                entities.EventBus.RaiseLocalEvent(point, request);
                Assert.That(request.CanUse, Is.True, prototype);
                Assert.That(request.SlotId, Is.Not.Empty, prototype);
                var attach = new DropshipAttachDoAfterEvent(entities.GetNetEntity(point), entities.GetNetEntity(item), request.SlotId);
                Assert.That(entities.System<SharedDoAfterSystem>().TryStartDoAfter(
                    new DoAfterArgs(entities, loader, TimeSpan.Zero, attach, point, point)), Is.True);
                Assert.That(containers.GetContainer(point, request.SlotId).Contains(item), Is.True, prototype);
                Assert.That(dropships.TryGetGridDropship(item, out var owner), Is.True);
                Assert.That(owner.Owner, Is.EqualTo(ship));
                entities.DeleteEntity(loader);
                return item;
            }

            var engines = points.Where(entities.HasComponent<DropshipEnginePointComponent>).ToArray();
            Assert.That(engines, Has.Length.EqualTo(2));
            Install(engines[0], "RMCDropshipAttachmentFuelEnhancer");
            Install(engines[1], "RMCDropshipAttachmentCoolingSystem");

            medicalPoint = points.First(entities.HasComponent<DropshipUtilityPointComponent>);
            Assert.That(entities.GetComponent<TransformComponent>(medicalPoint).GridUid, Is.EqualTo(ship));
            medevac = Install(medicalPoint, "RMCDropshipAttachmentMedevac");
            Assert.That(entities.GetComponent<DropshipUtilityComponent>(medevac).AttachmentPoint, Is.EqualTo(medicalPoint));

            var point = points.First(p => entities.HasComponent<DropshipWeaponPointComponent>(p) &&
                entities.GetComponent<MetaDataComponent>(p).EntityPrototype?.ID != "CMUMohawkM90Point");
            gun = Install(point, "RMCDropshipAttachmentGau21Cannon");
            gunAmmo = Install(point, "RMCDropshipAttachmentAmmoGAU");
            if (variant == "midway")
            {
                var chin = points.Single(p => entities.GetComponent<MetaDataComponent>(p).EntityPrototype?.ID == "CMUMohawkM90Point");
                var weaponPoint = entities.GetComponent<DropshipWeaponPointComponent>(chin);
                chinGun = containers.GetContainer(chin, weaponPoint.WeaponContainerSlotId).ContainedEntities.Single();
                chinAmmo = Install(chin, "CMUMohawkM90Ammo");
            }

            medic = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(ship, 0.5f, 0.5f));
            entities.System<SkillsSystem>().SetSkill(medic, "RMCSkillMedical", 2);
            ground = entities.System<SharedMapSystem>().CreateMap();
            target = entities.SpawnEntity("RMCMedevacStretcher", new EntityCoordinates(ground, 0.5f, 0.5f));
            patient = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(ground, 0.5f, 0.5f));
            Assert.That(entities.System<SharedBuckleSystem>().TryBuckle(patient, null, target, popup: false), Is.True);
            var marker = entities.SpawnEntity(null, new EntityCoordinates(ground, 30, 30));
            entities.AddComponent<DropshipDestinationComponent>(marker);
            var nav = entities.EntityQuery<DropshipNavigationComputerComponent>()
                .Single(c => entities.GetComponent<TransformComponent>(c.Owner).GridUid == ship);
            Assert.That(dropships.FlyTo((nav.Owner, nav), marker, null, startupTime: 0.5f), Is.True);
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            Assert.That(entities.GetComponent<FTLComponent>(ship).TravelTime,
                Is.EqualTo(entities.System<ShuttleSystem>().DefaultTravelTime * 0.75f + config.GetCVar(CCVars.FTLArrivalTime)).Within(0.01f));
            Assert.That(entities.GetComponent<DropshipComponent>(ship).RechargeTime,
                Is.EqualTo(TimeSpan.FromSeconds(config.GetCVar(CCVars.FTLCooldown) * 0.5f)));
        });
        await pair.RunSeconds(1);
        await AssertEngineClickTargets(true);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            // Exercise CAS's fly-by gate while the real flight is in transit.
            entities.AddComponent<DropshipInFlyByComponent>(ship);
            var weapons = entities.System<SharedDropshipWeaponSystem>();
            var rounds = entities.GetComponent<DropshipAmmoComponent>(gunAmmo);
            var before = rounds.Rounds;
            Assert.That(weapons.TryFireWeapon(gun, new EntityCoordinates(ground, 20, 20), DropshipWeaponStrikeType.Direct), Is.True);
            Assert.That(rounds.Rounds, Is.EqualTo(before - rounds.RoundsPerShot));
            Assert.That(entities.EntityQuery<AmmoInFlightComponent>().Any(), Is.True);
            if (variant == "midway")
            {
                var seat = entities.EntityQuery<MohawkGunnerySeatComponent>().Single().Owner;
                var gunner = entities.SpawnEntity("CMMobHuman", entities.GetComponent<TransformComponent>(seat).Coordinates);
                Assert.That(entities.System<SharedBuckleSystem>().TryBuckle(gunner, null, seat, popup: false), Is.True);
                var terminal = entities.EntityQuery<DropshipTerminalWeaponsComponent>().Single(c => c.Gunnery);
                Assert.That(weapons.TryFireWeapon(chinGun, new EntityCoordinates(ground, 25, 25),
                    DropshipWeaponStrikeType.Direct, gunner, terminal), Is.True);
                Assert.That(entities.GetComponent<DropshipAmmoComponent>(chinAmmo).Rounds, Is.EqualTo(360));
            }

            var utility = entities.GetComponent<DropshipUtilityComponent>(medevac);
            Assert.That(entities.System<DropshipUtilitySystem>().IsActivatable((medevac, utility), medic, out var popup), Is.True, popup);
            var changed = new DropshipTargetChangedEvent(entities.GetNetEntity(target));
            entities.EventBus.RaiseLocalEvent(medicalPoint, changed);
            Assert.That(utility.Target, Is.EqualTo(target));
            var interact = new InteractHandEvent(medic, medicalPoint);
            entities.EventBus.RaiseLocalEvent(medicalPoint, interact);
            Assert.That(entities.GetComponent<MedevacComponent>(medevac).IsActivated, Is.True);
        });
        await pair.RunSeconds(4);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<TransformComponent>(patient).GridUid, Is.EqualTo(ship),
                "The medevac module must deliver its patient into the moving cabin.");
            Assert.That(entities.GetComponent<MedevacComponent>(medevac).IsActivated, Is.False);
            Assert.That(entities.GetComponent<DropshipUtilityComponent>(medevac).NextActivateAt, Is.Not.Null);
            entities.DeleteEntity(ship);
        });
        await pair.RunTicksSync(2);
        await pair.CleanReturnAsync();
    }
}
