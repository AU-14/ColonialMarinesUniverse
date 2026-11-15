using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Stun;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Roles;

/// <summary>
///     The GRU armor goes on without UPP fatigues and carries its own Altyn, raised and lowered like a pressure
///     suit's helmet. The GRU sedative puts its target to sleep.
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
                .SpawnPlayerMob(map.GridCoords, new ProtoId<JobPrototype>("CMUJobIntelGRUSpetsnazMember"), new HumanoidCharacterProfile(), station: null);
            foreach (var slot in new[] { "head", "outerClothing" })
            {
                if (inventory.TryGetSlotEntity(mob, slot, out var worn))
                    SEntMan.DeleteEntity(worn);
            }

            var coords = SEntMan.GetComponent<TransformComponent>(mob).Coordinates;
            var armor = SEntMan.SpawnEntity("CMUIntelGRUHeavyArmor", coords);
            var stockArmor = SEntMan.SpawnEntity("RMCArmorSPPHeavy", coords);

            // A freshly spawned test mob fails unrelated equip checks, so the armor goes on by force and its
            // own wear rule is asked directly through the event a real equip raises.
            bool Allowed(EntityUid item, string slot)
            {
                Assert.That(inventory.TryGetSlot(mob, slot, out var definition), Is.True);
                var attempt = new BeingEquippedAttemptEvent(mob, mob, item, definition!);
                SEntMan.EventBus.RaiseLocalEvent(item, attempt, true);
                return !attempt.Cancelled;
            }

            // The section spawns in civilian clothes, not UPP fatigues.
            Assert.That(Allowed(stockArmor, "outerClothing"), Is.False, "stock UH7 should still need UPP fatigues");
            Assert.That(Allowed(armor, "outerClothing"), Is.True, "the GRU armor must go on over civilian clothes");

            // The armor keeps the stock UH7's tags.
            Assert.That(SComp<TagComponent>(armor).Tags, Is.SupersetOf(SComp<TagComponent>(stockArmor).Tags.ToList()));

            // The Altyn arrives stowed inside the armor.
            var toggle = SComp<ToggleableClothingComponent>(armor);
            Assert.That(toggle.ClothingUid, Is.Not.Null, "the GRU armor spawned without its Altyn");
            var altyn = toggle.ClothingUid!.Value;
            Assert.That(SComp<MetaDataComponent>(altyn).EntityPrototype?.ID, Is.EqualTo("CMUIntelGRUAltyn"));
            Assert.That(toggle.Container?.ContainedEntity, Is.EqualTo(altyn), "the Altyn should start stowed in the armor");

            Assert.That(inventory.TryEquip(mob, armor, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(Allowed(altyn, "head"), Is.True, "nothing should stop the Altyn going on over the GRU armor");

            // Raising the helmet puts the Altyn on the head; lowering it stows it again.
            SEntMan.EventBus.RaiseLocalEvent(armor, new ToggleClothingEvent { Performer = mob });
            Assert.That(inventory.TryGetSlotEntity(mob, "head", out var head), Is.True, "raising the helmet put nothing on the head");
            Assert.That(head, Is.EqualTo(altyn));

            SEntMan.EventBus.RaiseLocalEvent(armor, new ToggleClothingEvent { Performer = mob });
            Assert.That(inventory.TryGetSlotEntity(mob, "head", out _), Is.False, "lowering the helmet left it on the head");
            Assert.That(toggle.Container?.ContainedEntity, Is.EqualTo(altyn));

            // Taking the armor off with the Altyn up pulls the Altyn back into it.
            SEntMan.EventBus.RaiseLocalEvent(armor, new ToggleClothingEvent { Performer = mob });
            Assert.That(inventory.TryGetSlotEntity(mob, "head", out _), Is.True);
            Assert.That(inventory.TryUnequip(mob, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(inventory.TryGetSlotEntity(mob, "head", out _), Is.False, "the Altyn stayed on after the armor came off");
            Assert.That(toggle.Container?.ContainedEntity, Is.EqualTo(altyn));

            SEntMan.DeleteEntity(stockArmor);
            SEntMan.DeleteEntity(mob);
        });
    }

    [Test]
    public async Task GruSedativePutsTheTargetToSleep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        await Server.WaitAssertion(() =>
        {
            // Give the injector's full dose straight to the blood.
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var injector = SEntMan.SpawnEntity("CMUIntelGRUSedativeInjector", map.GridCoords);
            Assert.That(solutions.TryGetSolution(injector, "pen", out _, out var pen), Is.True);
            Assert.That(pen!.ContainsReagent("CMUIntelGRUSedative", null), Is.True, "the injector should hold the GRU sedative");

            // The mob every player species spawns as.
            target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(Server.System<BloodstreamSystem>().TryAddToBloodstream(target, pen.Clone()), Is.True);
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(target), Is.False);
            SEntMan.DeleteEntity(injector);
        });

        await RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(target), Is.True,
                "the sedative should knock the target out, not just make them yawn");
        });

        // A 10u dose wears off after about 40 seconds.
        await RunSeconds(50);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(target), Is.False, "the target never woke up");
            SEntMan.DeleteEntity(target);
        });
    }

    private Task RunSeconds(int seconds)
    {
        return RunTicksSync(seconds * Server.ResolveDependency<IGameTiming>().TickRate);
    }
}
