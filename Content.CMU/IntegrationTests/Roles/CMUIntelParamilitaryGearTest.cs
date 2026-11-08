using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Roles;

/// <summary>
///     The GRU armor goes on without UPP fatigues, and the GRU Altyn only goes on over that armor.
/// </summary>
[TestFixture]
public sealed class CMUIntelParamilitaryGearTest : GameTest
{
    [Test]
    public async Task GruArmorFitsCoverClothesAndCarriesTheAltyn()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var inventory = Server.System<InventorySystem>();
            var mob = Server.System<StationSpawningSystem>()
                .SpawnPlayerMob(map.GridCoords, new ProtoId<JobPrototype>("CMUJobIntelParamilitaryMember"), new HumanoidCharacterProfile(), station: null);
            foreach (var slot in new[] { "head", "outerClothing" })
            {
                if (inventory.TryGetSlotEntity(mob, slot, out var worn))
                    SEntMan.DeleteEntity(worn);
            }

            var coords = SEntMan.GetComponent<TransformComponent>(mob).Coordinates;
            var altyn = SEntMan.SpawnEntity("CMUIntelGRUAltyn", coords);
            var armor = SEntMan.SpawnEntity("CMUIntelGRUHeavyArmor", coords);
            var stockArmor = SEntMan.SpawnEntity("RMCArmorSPPHeavy", coords);

            // A freshly spawned test mob fails unrelated equip checks, so items go on by force and each
            // item's own wear rule is asked directly through the event a real equip raises.
            bool Allowed(EntityUid item, string slot)
            {
                Assert.That(inventory.TryGetSlot(mob, slot, out var definition), Is.True);
                var attempt = new BeingEquippedAttemptEvent(mob, mob, item, definition!);
                SEntMan.EventBus.RaiseLocalEvent(item, attempt, true);
                return !attempt.Cancelled;
            }

            bool AltynAllowed() => Allowed(altyn, "head");

            // The section spawns in civilian clothes, not UPP fatigues.
            Assert.That(Allowed(stockArmor, "outerClothing"), Is.False, "stock UH7 should still need UPP fatigues");
            Assert.That(Allowed(armor, "outerClothing"), Is.True, "the GRU armor must go on over civilian clothes");

            Assert.That(AltynAllowed(), Is.False, "the Altyn went on without armor");
            Assert.That(inventory.TryEquip(mob, stockArmor, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(AltynAllowed(), Is.False, "stock UH7 must not unlock the Altyn");

            Assert.That(inventory.TryUnequip(mob, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(mob, armor, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(AltynAllowed(), Is.True, "the Altyn must go on over the GRU armor");

            // Listing the pairing tag replaces the inherited ones, so check none were lost.
            var stockTags = SEntMan.TryGetComponent<TagComponent>(stockArmor, out var stock)
                ? stock.Tags.ToHashSet()
                : new HashSet<ProtoId<TagPrototype>>();
            Assert.That(SComp<TagComponent>(armor).Tags, Is.SupersetOf(stockTags), "the pairing tag replaced the armor's own tags");

            SEntMan.DeleteEntity(stockArmor);
            SEntMan.DeleteEntity(mob);
        });
    }
}
