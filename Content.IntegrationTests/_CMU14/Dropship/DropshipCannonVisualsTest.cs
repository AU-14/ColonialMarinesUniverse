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
            user = SEntMan.SpawnEntity("CMMobHuman", coordinates.Offset(new Vector2(1, 0)));
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

    [TestCase(FTLState.Travelling, false)]
    [TestCase(FTLState.Available, false)]
    [TestCase(FTLState.Available, true)]
    public async Task HandLoadingAllowsInteriorWeaponPointsInFlightAndRechecksAtCompletion(FTLState state, bool takeoffDuringLoad)
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
            if (takeoffDuringLoad)
                ship.State = FTLState.Starting;
        });
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            var expectedToLoad = state is FTLState.Travelling or FTLState.Arriving ||
                                 state == FTLState.Available && !takeoffDuringLoad;
            Assert.That(slot.ContainedEntity != null, Is.EqualTo(expectedToLoad));
        });
    }

}
