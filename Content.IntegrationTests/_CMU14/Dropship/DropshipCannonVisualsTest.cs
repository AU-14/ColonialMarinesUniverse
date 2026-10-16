// Set ammunition counts directly to isolate the mounted sprite transitions.
#pragma warning disable RA0002

using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Fabricator;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared.Interaction;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Shuttles.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DropshipCannonVisualsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task FabricatedIndividualAmmoIsVisibleAndCentered()
    {
        EntityUid map = default;
        var printed = new List<NetEntity>();
        await Server.WaitAssertion(() =>
        {
            map = SEntMan.System<SharedMapSystem>().CreateMap();
            var fabricator = SEntMan.SpawnEntity("RMCDropshipFabricator",
                new EntityCoordinates(map, Vector2.Zero));
            var component = SEntMan.GetComponent<DropshipFabricatorComponent>(fabricator);
            var system = SEntMan.System<DropshipFabricatorSystem>();
            foreach (var prototype in new[]
                     {
                         "CMUDropshipAmmoHowitzerHE",
                         "CMUDropshipAmmoHowitzerHEFrag",
                         "CMUDropshipAmmoHowitzerHEAT",
                         "CMUDropshipAmmoHowitzerAirburst",
                         "CMUDropshipAmmoBOFORSMagHE",
                         "CMUDropshipAmmoBOFORSMagAP",
                         "CMUDropshipAmmoBOFORSMagAirburst",
                         "CMUDropshipAmmoBOFORSMagHEAT",
                         "CMUDropshipAmmoBOFORSMagIncendiary",
                     })
            {
                component.Printing = prototype;
                component.PrintAt = Server.ResolveDependency<IGameTiming>().CurTime;
                system.Update(0);
                Assert.That(component.Printing, Is.Null);
                var ammo = SEntMan.EntityQuery<MetaDataComponent>()
                    .Single(metadata => metadata.EntityPrototype?.ID == prototype);
                printed.Add(SEntMan.GetNetEntity(ammo.Owner));
            }
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            foreach (var net in printed)
            {
                var uid = CEntMan.GetEntity(net);
                var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                Assert.That(sprite.Visible, Is.True, $"{net} world sprite");
                Assert.That(sprite.Offset, Is.EqualTo(Vector2.Zero), $"{net} sprite offset");
                var layer = sprite[0];
                Assert.That(layer.Visible, Is.True, $"{net} ammo layer");
                Assert.That(layer.ActualRsi, Is.Not.Null);
                Assert.That(layer.ActualRsi!.TryGetState(layer.RsiState, out _), Is.True);
            }
        });
        await Server.WaitAssertion(() => SEntMan.DeleteEntity(map));
    }

    [TestCase("CMUDropshipAttachmentBOFORS", "CMUDropshipAmmoBOFORSMagHE",
        "boforsnew2", "bofors_cannon_interior")]
    [TestCase("CMUDropshipAttachmentHowitzer", "CMUDropshipAmmoHowitzerHE",
        "cas_howitzer_105_uninstalled", "howitzer_cannon_interior")]
    public async Task PrintedCannonsAndInteriorAmmoStatesUseTheirArtwork(
        string weaponPrototype, string ammoPrototype, string printedState, string interiorState)
    {
        await Client.WaitAssertion(() =>
        {
            AssertCatalog(CEntMan);
            var printed = CEntMan.SpawnEntity(weaponPrototype, MapCoordinates.Nullspace);
            var layer = CEntMan.GetComponent<SpriteComponent>(printed)[0];
            Assert.That(layer.RsiState.ToString(), Is.EqualTo(printedState));
            Assert.That(layer.ActualRsi!.Path.ToString(),
                Is.EqualTo("/Textures/_RMC14/Objects/dropship_attachments.rsi"));
            CEntMan.DeleteEntity(printed);
        });

        EntityUid map = default;
        EntityUid point = default;
        EntityUid weapon = default;
        EntityUid ammo = default;
        EntityUid user = default;
        NetEntity pointNet = default;
        NetEntity weaponNet = default;
        NetEntity ammoNet = default;
        await Server.WaitAssertion(() =>
        {
            AssertCatalog(SEntMan);
            map = SEntMan.System<SharedMapSystem>().CreateMap();
            var coordinates = new EntityCoordinates(map, Vector2.Zero);
            point = SEntMan.SpawnEntity("AU14InteriorWeaponAttachmentPoint", coordinates);
            pointNet = SEntMan.GetNetEntity(point);
            weapon = SEntMan.SpawnEntity(weaponPrototype, coordinates);
            ammo = SEntMan.SpawnEntity(ammoPrototype, coordinates);
            weaponNet = SEntMan.GetNetEntity(weapon);
            ammoNet = SEntMan.GetNetEntity(ammo);
            user = SEntMan.SpawnEntity("CMMobHuman", coordinates);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, ammo), Is.True);
            var utility = SEntMan.GetComponent<DropshipUtilityPointComponent>(point);
            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.That(containers.Insert(weapon,
                containers.EnsureContainer<ContainerSlot>(point, utility.UtilitySlotId)), Is.True);
        });

        async Task AssertInteriorState(string state, bool visible = true)
        {
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                CEntMan.System<AppearanceSystem>().FrameUpdate(0f);
                var layer = CEntMan.GetComponent<SpriteComponent>(CEntMan.GetEntity(pointNet))
                    [DropshipPointVisualsLayers.AttachedUtility];
                Assert.That(layer.Visible, Is.EqualTo(visible));
                if (!visible)
                    return;
                Assert.That(layer.RsiState.ToString(), Is.EqualTo(state));
                Assert.That(layer.ActualRsi!.Path.ToString(),
                    Is.EqualTo("/Textures/_RMC14/Objects/dropship_utility_attachments.rsi"));
            });
        }

        await AssertInteriorState(interiorState + "_unloaded");
        await Client.WaitAssertion(() =>
        {
            var ammunition = CEntMan.GetComponent<DropshipAmmoComponent>(CEntMan.GetEntity(ammoNet));
            var mountedPrototype = CEntMan.GetComponent<MetaDataComponent>(CEntMan.GetEntity(weaponNet))
                .EntityPrototype!.ID;
            Assert.That(ammunition.Weapon.Id, Is.EqualTo(mountedPrototype),
                "The client must receive the matching ammo target before predicting the hand-load.");
        });
        await Server.WaitAssertion(() =>
        {
            var interaction = new InteractUsingEvent(user, ammo, point,
                SEntMan.GetComponent<TransformComponent>(point).Coordinates);
            SEntMan.EventBus.RaiseLocalEvent(point, interaction);
            Assert.That(interaction.Handled, Is.True);
            var utility = SEntMan.GetComponent<DropshipUtilityPointComponent>(point);
            Assert.That(SEntMan.System<SharedContainerSystem>()
                .EnsureContainer<ContainerSlot>(point, utility.AmmoContainerSlotId).ContainedEntity, Is.Null,
                "Hand-loading must not complete instantly.");
        });
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            var utility = SEntMan.GetComponent<DropshipUtilityPointComponent>(point);
            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.That(containers.EnsureContainer<ContainerSlot>(point, utility.AmmoContainerSlotId).ContainedEntity,
                Is.EqualTo(ammo), "Hand-loading must accept the magazine or shell, not just direct container insertion.");
        });
        await AssertInteriorState(interiorState + "_loaded");

        await Server.WaitAssertion(() =>
        {
            var ammunition = SEntMan.GetComponent<DropshipAmmoComponent>(ammo);
            ammunition.Rounds = 0;
            SEntMan.Dirty(ammo, ammunition);
            SEntMan.System<PowerLoaderSystem>().SyncAppearance(point);
        });
        await AssertInteriorState(interiorState + "_unloaded");

        await Server.WaitAssertion(() =>
        {
            var ammunition = SEntMan.GetComponent<DropshipAmmoComponent>(ammo);
            ammunition.Rounds = ammunition.RoundsPerShot - 1;
            SEntMan.Dirty(ammo, ammunition);
            SEntMan.System<PowerLoaderSystem>().SyncAppearance(point);
        });
        await AssertInteriorState(interiorState + "_unloaded");

        await Server.WaitAssertion(() =>
        {
            var ammunition = SEntMan.GetComponent<DropshipAmmoComponent>(ammo);
            ammunition.Rounds = ammunition.RoundsPerShot;
            SEntMan.Dirty(ammo, ammunition);
            SEntMan.System<PowerLoaderSystem>().SyncAppearance(point);
        });
        await AssertInteriorState(interiorState + "_loaded");
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedContainerSystem>().TryRemoveFromContainer(ammo), Is.True));
        await AssertInteriorState(interiorState + "_unloaded");
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedContainerSystem>().TryRemoveFromContainer(weapon), Is.True));
        await AssertInteriorState("", visible: false);

        await Server.WaitAssertion(() => SEntMan.DeleteEntity(map));
    }

    [TestCase("AU14InteriorWeaponAttachmentPoint")]
    [TestCase("CMAlamoWall61")]
    public async Task BoforsAcceptsEveryMagazineButRejectsHowitzerShells(string pointPrototype)
    {
        EntityUid map = default;
        EntityUid point = default;
        EntityUid user = default;
        ContainerSlot ammoSlot = default!;
        EntityCoordinates coordinates = default;
        await Server.WaitAssertion(() =>
        {
            map = SEntMan.System<SharedMapSystem>().CreateMap();
            coordinates = new EntityCoordinates(map, Vector2.Zero);
            user = SEntMan.SpawnEntity("CMMobHuman", coordinates);
            point = SEntMan.SpawnEntity(pointPrototype, coordinates);
            var weapon = SEntMan.SpawnEntity("CMUDropshipAttachmentBOFORS", coordinates);
            var containers = SEntMan.System<SharedContainerSystem>();
            var weaponSlotId = SEntMan.TryGetComponent<DropshipUtilityPointComponent>(point, out var utility)
                ? utility.UtilitySlotId
                : SEntMan.GetComponent<DropshipWeaponPointComponent>(point).WeaponContainerSlotId;
            var ammoSlotId = utility != null
                ? utility.AmmoContainerSlotId
                : SEntMan.GetComponent<DropshipWeaponPointComponent>(point).AmmoContainerSlotId;
            Assert.That(containers.Insert(weapon,
                containers.EnsureContainer<ContainerSlot>(point, weaponSlotId)), Is.True);
            ammoSlot = containers.EnsureContainer<ContainerSlot>(point, ammoSlotId);
        });

        foreach (var ammoPrototype in new[]
                 {
                     "CMUDropshipAmmoBOFORSMagHE",
                     "CMUDropshipAmmoBOFORSMagAP",
                     "CMUDropshipAmmoBOFORSMagAirburst",
                     "CMUDropshipAmmoBOFORSMagHEAT",
                     "CMUDropshipAmmoBOFORSMagIncendiary",
                     "CMUDropshipAmmoHowitzerHE",
                 })
        {
            EntityUid ammo = default;
            await Server.WaitAssertion(() =>
            {
                ammo = SEntMan.SpawnEntity(ammoPrototype, coordinates);
                Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, ammo), Is.True);
                var interaction = new InteractUsingEvent(user, ammo, point, coordinates);
                SEntMan.EventBus.RaiseLocalEvent(point, interaction);
                Assert.That(interaction.Handled, Is.True);
                Assert.That(ammoSlot.ContainedEntity, Is.Null);
            });
            await Pair.RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                if (ammoPrototype == "CMUDropshipAmmoHowitzerHE")
                    Assert.That(ammoSlot.ContainedEntity, Is.Null, "A 105mm shell must not fit the 40mm Bofors.");
                else
                {
                    Assert.That(ammoSlot.ContainedEntity, Is.EqualTo(ammo), ammoPrototype);
                    Assert.That(SEntMan.System<SharedContainerSystem>().TryRemoveFromContainer(ammo), Is.True);
                }
                SEntMan.DeleteEntity(ammo);
            });
        }

        await Server.WaitAssertion(() => SEntMan.DeleteEntity(map));
    }

    [TestCase(FTLState.Starting, false)]
    [TestCase(FTLState.Travelling, false)]
    [TestCase(FTLState.Arriving, false)]
    [TestCase(FTLState.Available, true)]
    public async Task HandLoadingRejectsFlightAndRechecksAtCompletion(FTLState state, bool takeOffDuringLoad)
    {
        var map = await Pair.CreateTestMap();
        ContainerSlot slot = default!;
        await Server.WaitAssertion(() =>
        {
            var ship = SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            ship.State = state;
            var point = SEntMan.SpawnEntity("AU14InteriorWeaponAttachmentPoint", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var weapon = SEntMan.SpawnEntity("CMUDropshipAttachmentHowitzer", map.GridCoords);
            var ammo = SEntMan.SpawnEntity("CMUDropshipAmmoHowitzerHE", map.GridCoords);
            var utility = SEntMan.GetComponent<DropshipUtilityPointComponent>(point);
            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.That(containers.Insert(weapon,
                containers.EnsureContainer<ContainerSlot>(point, utility.UtilitySlotId)), Is.True);
            slot = containers.EnsureContainer<ContainerSlot>(point, utility.AmmoContainerSlotId);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, ammo), Is.True);
            var interaction = new InteractUsingEvent(user, ammo, point, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(point, interaction);
            Assert.That(interaction.Handled, Is.True);
            Assert.That(slot.ContainedEntity, Is.Null);
            if (takeOffDuringLoad)
                ship.State = FTLState.Travelling;
        });
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() => Assert.That(slot.ContainedEntity, Is.Null));
    }

    [Test]
    public async Task BombBayShowsLoadedArtworkOnlyWithAnUnspentBomb()
    {
        EntityUid map = default;
        EntityUid point = default;
        EntityUid clamp = default;
        NetEntity pointNet = default;
        await Server.WaitAssertion(() =>
        {
            map = SEntMan.System<SharedMapSystem>().CreateMap();
            var coordinates = new EntityCoordinates(map, Vector2.Zero);
            point = SEntMan.SpawnEntity("AU14InteriorWeaponAttachmentPoint", coordinates);
            clamp = SEntMan.SpawnEntity("CMUDropshipAttachmentJDAMBombClamp", coordinates);
            pointNet = SEntMan.GetNetEntity(point);
            var utility = SEntMan.GetComponent<DropshipUtilityPointComponent>(point);
            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.That(containers.Insert(clamp,
                containers.EnsureContainer<ContainerSlot>(point, utility.UtilitySlotId)), Is.True);
        });

        async Task AssertBombBay(string state, bool visible = true)
        {
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                CEntMan.System<AppearanceSystem>().FrameUpdate(0f);
                var layer = CEntMan.GetComponent<SpriteComponent>(CEntMan.GetEntity(pointNet))
                    [DropshipPointVisualsLayers.AttachedUtility];
                Assert.That(layer.Visible, Is.EqualTo(visible));
                if (!visible)
                    return;
                Assert.That(layer.RsiState.ToString(), Is.EqualTo(state));
                Assert.That(layer.ActualRsi!.Path.ToString(),
                    Is.EqualTo("/Textures/_RMC14/Objects/dropship_utility_attachments.rsi"));
            });
        }

        await AssertBombBay("bomb_clamp_unloaded");
        foreach (var prototype in new[]
                 {
                     "CMUDropshipAmmoJDAMHE",
                     "CMUDropshipAmmoJDAMIncendiary",
                     "CMUDropshipAmmoJDAMAP",
                     "CMUDropshipAmmoJDAMCluster",
                     "CMUDropshipAmmoJDAMNapalm",
                 })
        {
            EntityUid bomb = default;
            await Server.WaitAssertion(() =>
            {
                bomb = SEntMan.SpawnEntity(prototype, new EntityCoordinates(map, Vector2.Zero));
                var utility = SEntMan.GetComponent<DropshipUtilityPointComponent>(point);
                var containers = SEntMan.System<SharedContainerSystem>();
                Assert.That(containers.Insert(bomb,
                    containers.EnsureContainer<ContainerSlot>(point, utility.AmmoContainerSlotId)), Is.True);
            });
            await AssertBombBay("bomb_clamp_interior_loaded");
            await Server.WaitAssertion(() =>
            {
                var ammunition = SEntMan.GetComponent<DropshipAmmoComponent>(bomb);
                ammunition.Rounds = 0;
                SEntMan.Dirty(bomb, ammunition);
                SEntMan.System<PowerLoaderSystem>().SyncAppearance(point);
            });
            await AssertBombBay("bomb_clamp_unloaded");
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.System<SharedContainerSystem>().TryRemoveFromContainer(bomb), Is.True);
                SEntMan.DeleteEntity(bomb);
            });
            await AssertBombBay("bomb_clamp_unloaded");
        }

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedContainerSystem>().TryRemoveFromContainer(clamp), Is.True));
        await AssertBombBay("", visible: false);
        await Server.WaitAssertion(() => SEntMan.DeleteEntity(map));
    }

    private static void AssertCatalog(IEntityManager entities)
    {
        var catalog = entities.System<DropshipFabricatorSystem>().Printables.Select(id => id.Id).ToArray();
        Assert.That(catalog, Does.Contain("CMUDropshipAttachmentBOFORS"));
        Assert.That(catalog, Does.Contain("CMUDropshipAttachmentHowitzer"));
        Assert.That(catalog, Does.Not.Contain("RMCDropshipAttachmentBOFORS"));
        Assert.That(catalog, Does.Not.Contain("RMCDropshipAttachmentHowitzer"));
    }
}
