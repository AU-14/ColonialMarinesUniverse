using System.Linq;
using Content.Server.CMU14.Hygiene;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Power;
using Content.Shared.Explosion;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Content.Shared.Verbs;
using Content.Shared.CMU14.Hygiene;
using Content.Shared.CMU14.Item.Stain;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared._RMC14.Xenonids.Spray;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Client.GameObjects;

namespace Content.IntegrationTests.CMU14.Hygiene;

[TestFixture]
public sealed class CMUHygieneTest
{
    [Test]
    public async Task AcidContactStainsButCleaningPreservesActiveAcidAndDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var human = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var projectile = em.SpawnEntity(null, MapCoordinates.Nullspace);
            em.AddComponent<XenoAcidProjectileComponent>(projectile);
            var damage = new DamageSpecifier { DamageDict = { ["Caustic"] = 10 } };
            var hit = new ProjectileHitEvent(damage, human);
            em.EventBus.RaiseLocalEvent(projectile, ref hit);
            Assert.That(em.GetComponent<CMUResidueComponent>(human).Alien, Is.True);
            var activeAcid = em.EnsureComponent<SprayAcidedComponent>(human);
            em.System<DamageableSystem>().SetDamage((human, em.GetComponent<DamageableComponent>(human)), damage);
            em.System<CMUResidueSystem>().Clean(human);
            Assert.Multiple(() =>
            {
                Assert.That(em.GetComponent<CMUResidueComponent>(human).Color, Is.Null);
                Assert.That(em.GetComponent<SprayAcidedComponent>(human), Is.SameAs(activeAcid));
                Assert.That(em.GetComponent<DamageableComponent>(human).TotalDamage.Float(), Is.EqualTo(10f));
            });
            em.DeleteEntity(human);
            em.DeleteEntity(projectile);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LaundryLocksContentsAndOnlyCleansOnCompletion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid machine = default, boots = default, spare = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            machine = em.SpawnEntity("CMWashingMachine", map.GridCoords);
            SetTestPower(em, machine, true);
            boots = em.SpawnEntity("CMBootsBlack", map.GridCoords);
            spare = em.SpawnEntity("CMBootsBlack", map.GridCoords);
            var hygiene = em.System<CMUHygieneSystem>();
            var storage = em.System<SharedStorageSystem>();
            Assert.That(hygiene.TryStartLaundry(machine), Is.False, "Empty cycles must not start.");
            Assert.That(storage.Insert(machine, boots, out _, playSound: false), Is.True);
            em.System<CMUItemStainSystem>().TryStain(boots, CMUItemStainKind.Blood, Color.Green);
            em.GetComponent<CMULaundryComponent>(machine).CycleSeconds = 0.2f;
            Assert.That(hygiene.TryStartLaundry(machine), Is.True);
            Assert.That(hygiene.TryStartLaundry(machine), Is.False, "A running cycle cannot restart.");
            Assert.That(storage.Insert(machine, spare, out _, playSound: false), Is.False);
            Assert.That(em.System<SharedContainerSystem>().Remove(boots,
                em.GetComponent<StorageComponent>(machine).Container), Is.False);
            Assert.That(em.GetComponent<CMUItemStainComponent>(boots).Color, Is.Not.Null);
        });
        await pair.RunTicksSync(30);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.GetComponent<CMUItemStainComponent>(boots).Color, Is.Null);
            Assert.That(em.GetComponent<CMULaundryComponent>(machine).FinishAt, Is.Null);
            Assert.That(em.System<SharedContainerSystem>().Remove(boots,
                em.GetComponent<StorageComponent>(machine).Container), Is.True);
            Assert.That(em.System<SharedStorageSystem>().Insert(machine, spare, out _, playSound: false), Is.True);
            em.System<CMUItemStainSystem>().TryStain(spare, CMUItemStainKind.Blood, Color.Green);
            Assert.That(em.System<CMUHygieneSystem>().TryStartLaundry(machine), Is.True);
            em.System<CMUHygieneSystem>().StopLaundry((machine, em.GetComponent<CMULaundryComponent>(machine)));
        });
        await pair.RunTicksSync(30);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.GetComponent<CMUItemStainComponent>(spare).Color, Is.Not.Null,
                "Cancelling must not finish the cleaning later.");
            em.DeleteEntity(machine);
            em.DeleteEntity(boots);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShowerRequiresUndressingAndLeavesClothingDirty()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default, shower = default, uniform = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("CMMobHuman", map.GridCoords);
            shower = em.SpawnEntity("CMShower", map.GridCoords);
            uniform = em.SpawnEntity("JumpsuitMarine", map.GridCoords);
            Assert.That(em.System<InventorySystem>().TryEquip(human, uniform, "jumpsuit", silent: true, force: true), Is.True);
            em.System<CMUHygieneSystem>().ApplyAlienResidue(human);
            Assert.That(em.GetComponent<CMUResidueComponent>(human).Alien, Is.True);
            em.GetComponent<CMUShowerComponent>(shower).WashSeconds = 1f;
            var activate = new ActivateInWorldEvent(human, shower, true);
            em.EventBus.RaiseLocalEvent(shower, activate);
            Assert.That(activate.Handled, Is.True);
        });
        await pair.RunTicksSync(90);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.GetComponent<CMUResidueComponent>(human).Color, Is.Not.Null);
            Assert.That(em.System<InventorySystem>().TryUnequip(human, "jumpsuit", silent: true, force: true), Is.True);
        });
        await pair.RunTicksSync(90);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.GetComponent<CMUResidueComponent>(human).Color, Is.Null);
            Assert.That(em.GetComponent<CMUResidueComponent>(human).Alien, Is.False);
            Assert.That(em.GetComponent<CMUItemStainComponent>(uniform).Color, Is.Not.Null,
                "Showers wash skin; discarded clothing still needs laundry.");
            em.DeleteEntity(human);
            em.DeleteEntity(shower);
            em.DeleteEntity(uniform);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LeavingShowerAndTurningItOffResetProgress()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default, shower = default;
        await server.WaitPost(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("CMMobHuman", map.GridCoords);
            shower = em.SpawnEntity("CMShower", map.GridCoords);
            em.System<CMUHygieneSystem>().ApplyAlienResidue(human);
            em.GetComponent<CMUShowerComponent>(shower).WashSeconds = 20f;
            em.EventBus.RaiseLocalEvent(shower, new ActivateInWorldEvent(human, shower, true));
        });
        await pair.RunTicksSync(90);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.GetComponent<CMUShowerComponent>(shower).Progress.ContainsKey(human), Is.True);
            em.System<SharedTransformSystem>().SetCoordinates(human, new EntityCoordinates(map.Grid, 5f, 5f));
        });
        await pair.RunTicksSync(90);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.GetComponent<CMUShowerComponent>(shower).Progress.ContainsKey(human), Is.False);
            em.System<SharedTransformSystem>().SetCoordinates(human, map.GridCoords);
        });
        await pair.RunTicksSync(90);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            em.EventBus.RaiseLocalEvent(shower, new ActivateInWorldEvent(human, shower, true));
            Assert.That(em.GetComponent<CMUShowerComponent>(shower).Progress, Is.Empty);
            Assert.That(em.GetComponent<CMUResidueComponent>(human).Color, Is.Not.Null);
            em.DeleteEntity(human);
            em.DeleteEntity(shower);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ResidueReplicatesAndClientOverlayDisappearsAfterCleaning()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        EntityUid human = default;
        await server.WaitPost(() =>
        {
            human = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            server.System<CMUHygieneSystem>().ApplyAlienResidue(human);
            server.System<CMUResidueSystem>().Stain(human, Color.Red);
        });
        await pair.RunTicksSync(10);
        var clientHuman = client.EntMan.GetEntity(server.EntMan.GetNetEntity(human));
        await client.WaitAssertion(() =>
        {
            Assert.That(client.EntMan.GetComponent<CMUResidueComponent>(clientHuman).Alien, Is.True);
            var sprite = client.EntMan.GetComponent<SpriteComponent>(clientHuman);
            Assert.That(sprite.LayerMapTryGet("cmu-body-residue", out var index), Is.True);
            Assert.That(sprite[index].Visible, Is.True);
        });
        await server.WaitPost(() => server.System<CMUResidueSystem>().Clean(human));
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() =>
        {
            Assert.That(client.EntMan.GetComponent<CMUResidueComponent>(clientHuman).Color, Is.Null);
            var sprite = client.EntMan.GetComponent<SpriteComponent>(clientHuman);
            Assert.That(sprite.LayerMapTryGet("cmu-body-residue", out var index), Is.True);
            Assert.That(sprite[index].Visible, Is.False);
        });
        await server.WaitPost(() => server.EntMan.DeleteEntity(human));
        await pair.CleanReturnAsync();
    }
    [TestCase("CMWashingMachine")]
    [TestCase("CMWashingMachineDouble")]
    public async Task AltClickAndApplianceVisualsReplicate(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        EntityUid machine = default, shower = default, human = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("CMMobHuman", map.GridCoords);
            machine = em.SpawnEntity(prototype, map.GridCoords);
            SetTestPower(em, machine, true);
            shower = em.SpawnEntity("CMShower", map.GridCoords);
            var boots = em.SpawnEntity("CMBootsBlack", map.GridCoords);
            Assert.That(em.System<SharedStorageSystem>().Insert(machine, boots, out _, playSound: false), Is.True);
            em.GetComponent<CMULaundryComponent>(machine).CycleSeconds = 1f;
            var blocked = new GetVerbsEvent<AlternativeVerb>(human, machine, null, null, true, true, false, new());
            em.EventBus.RaiseLocalEvent(machine, blocked);
            Assert.That(blocked.Verbs.Any(verb => verb.Priority == 10), Is.False,
                "The wash action must not be offered through walls or out of reach.");
        });
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() => AssertIndicators(false));
        await server.WaitAssertion(() =>
        {
            var verbs = new GetVerbsEvent<AlternativeVerb>(human, machine, null, null, true, true, true, new());
            server.EntMan.EventBus.RaiseLocalEvent(machine, verbs);
            verbs.Verbs.Single(verb => verb.Priority == 10).Act!();
            Assert.That(server.EntMan.GetComponent<CMULaundryComponent>(machine).FinishAt, Is.Not.Null);
            server.EntMan.EventBus.RaiseLocalEvent(shower, new ActivateInWorldEvent(human, shower, true));
        });
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() => AssertIndicators(true));
        // Cancel by Alt-click: both server state and client indicator must return to idle.
        await server.WaitAssertion(() =>
        {
            var verbs = new GetVerbsEvent<AlternativeVerb>(human, machine, null, null, true, true, true, new());
            server.EntMan.EventBus.RaiseLocalEvent(machine, verbs);
            verbs.Verbs.Single(verb => verb.Priority == 10).Act!();
            server.EntMan.EventBus.RaiseLocalEvent(shower, new ActivateInWorldEvent(human, shower, true));
        });
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() => AssertIndicators(false));
        await server.WaitAssertion(() => Assert.That(server.System<CMUHygieneSystem>().TryStartLaundry(machine), Is.True));
        await pair.RunTicksSync(90);
        await client.WaitAssertion(() => AssertIndicators(false));
        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(machine);
            server.EntMan.DeleteEntity(shower);
            server.EntMan.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();

        void AssertIndicators(bool running)
        {
            var em = client.EntMan;
            var washerSprite = em.GetComponent<SpriteComponent>(em.GetEntity(server.EntMan.GetNetEntity(machine)));
            Assert.That(washerSprite.LayerMapTryGet(CMUHygieneVisuals.Indicator, out var indicator), Is.True);
            Assert.That(washerSprite[indicator].Color, Is.EqualTo(Color.FromHex(running ? "#45ff69" : "#ff3030")));
            var showerSprite = em.GetComponent<SpriteComponent>(em.GetEntity(server.EntMan.GetNetEntity(shower)));
            Assert.That(showerSprite.LayerMapTryGet(CMUHygieneVisuals.Water, out var water), Is.True);
            Assert.That(showerSprite[water].Visible, Is.EqualTo(running));
        }
    }

    [Test]
    public async Task LaundryPowerLossCancelsWithoutCleaning()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var machine = em.SpawnEntity("CMWashingMachine", map.GridCoords);
            var boots = em.SpawnEntity("CMBootsBlack", map.GridCoords);
            Assert.That(em.System<SharedStorageSystem>().Insert(machine, boots, out _, playSound: false), Is.True);
            em.System<CMUItemStainSystem>().TryStain(boots, CMUItemStainKind.Blood, Color.Red);
            var hygiene = em.System<CMUHygieneSystem>();
            SetTestPower(em, machine, false);
            Assert.That(hygiene.TryStartLaundry(machine), Is.False);
            SetTestPower(em, machine, true);
            Assert.That(hygiene.TryStartLaundry(machine), Is.True);
            SetTestPower(em, machine, false);
            Assert.Multiple(() =>
            {
                Assert.That(em.GetComponent<CMULaundryComponent>(machine).FinishAt, Is.Null);
                Assert.That(em.HasComponent<CMUActiveLaundryComponent>(machine), Is.False);
                Assert.That(em.GetComponent<CMUItemStainComponent>(boots).Color, Is.Not.Null);
                Assert.That(hygiene.TryStartLaundry(machine), Is.False);
            });
            em.DeleteEntity(machine);
            em.DeleteEntity(boots);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WasherExplosionProfilesPreserveEffectsAndHalveDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var serialization = pair.Server.ResolveDependency<ISerializationManager>();
            foreach (var original in prototypes.EnumeratePrototypes<ExplosionPrototype>())
            {
                if (original.ID.StartsWith("CMULaundryHalf", System.StringComparison.Ordinal))
                    continue;
                Assert.That(prototypes.TryIndex<ExplosionPrototype>("CMULaundryHalf" + original.ID, out var half),
                    Is.True, $"Missing washer profile for {original.ID}; regenerate laundry_explosions.yml.");
                Assert.That(half!.DamagePerIntensity.DamageDict, Is.EqualTo((original.DamagePerIntensity * 0.5f).DamageDict),
                    $"Incorrect washer damage for {original.ID}.");
                var sourceData = (MappingDataNode) serialization.WriteValue(original, alwaysWrite: true);
                var washerData = (MappingDataNode) serialization.WriteValue(half, alwaysWrite: true);
                sourceData.Remove("id");
                sourceData.Remove("damagePerIntensity");
                washerData.Remove("id");
                washerData.Remove("damagePerIntensity");
                Assert.That(washerData, Is.EqualTo(sourceData), $"Washer effects differ for {original.ID}.");
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LaundryBatchesAmmunitionAndPreservesOverflow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var machine = em.SpawnEntity("CMWashingMachine", map.GridCoords);
            var clothing = em.SpawnEntity("CMBootsBlack", map.GridCoords);
            var magazine = em.SpawnEntity("CMMagazineRifleM54C", map.GridCoords);
            var containers = em.System<SharedContainerSystem>();
            var pocket = containers.EnsureContainer<Container>(clothing, "hygiene-test");
            Assert.That(containers.Insert(magazine, pocket), Is.True);
            Assert.That(em.System<SharedStorageSystem>().Insert(machine, clothing, out _, playSound: false), Is.True);
            SetTestPower(em, machine, true);
            var ammunition = em.GetComponent<BallisticAmmoProviderComponent>(magazine);
            ammunition.Capacity = 2000;
            ammunition.UnspawnedCount = ammunition.Capacity;
            em.System<SharedGunSystem>().UpdateBallisticAppearance((magazine, ammunition));
            var laundry = em.GetComponent<CMULaundryComponent>(machine);
            laundry.AmmoCookoffSeconds = 0;
            laundry.CookoffSeconds = 100;
            var hygiene = em.System<CMUHygieneSystem>();
            Assert.That(hygiene.TryStartLaundry(machine), Is.True);
            var initial = CountRounds();
            hygiene.Update(0);
            Assert.That(CountRounds(), Is.InRange(1, initial - 1), "One update must not extract the whole load.");
            var emitters = em.EntityQueryEnumerator<CMULaundryPopcornComponent>();
            Assert.That(emitters.MoveNext(out var source, out var popcorn), Is.True);
            Assert.That(CountRounds() + popcorn.RoundsRemaining, Is.EqualTo(initial),
                "Extracted ammunition must be accounted for before popping begins.");

            var retiring = source;
            popcorn.RoundsRemaining = 1;
            var beforeRetirement = CountRounds();
            hygiene.Update(0);
            Assert.That(em.IsQueuedForDeletion(retiring), Is.True);
            emitters = em.EntityQueryEnumerator<CMULaundryPopcornComponent>();
            while (emitters.MoveNext(out var nextSource, out var nextPopcorn))
            {
                if (nextSource == retiring || nextPopcorn.Washer != machine || em.IsQueuedForDeletion(nextSource))
                    continue;
                source = nextSource;
                popcorn = nextPopcorn;
                break;
            }
            Assert.That(source, Is.Not.EqualTo(retiring), "New rounds need an emitter that is not retiring.");
            Assert.That(popcorn.RoundsRemaining, Is.EqualTo(beforeRetirement - CountRounds()),
                "A retiring emitter must not discard the next batch of ammunition.");
            for (var update = 0; update < initial; update++)
                hygiene.Update(0);
            var remaining = CountRounds();
            Assert.That(remaining, Is.GreaterThan(0), "Overflow ammunition must remain in its magazine.");
            Assert.That(popcorn.RoundsRemaining, Is.GreaterThan(0));
            for (var update = 0; update < 10; update++)
                hygiene.Update(0);
            Assert.That(CountRounds(), Is.EqualTo(remaining));
            Assert.That(pocket.Contains(magazine), Is.True);
            em.DeleteEntity(source);
            em.DeleteEntity(machine);
            if (!em.Deleted(clothing))
                em.DeleteEntity(clothing);
            if (!em.Deleted(magazine))
                em.DeleteEntity(magazine);
            hygiene.Update(0);

            int CountRounds()
            {
                var count = new GetAmmoCountEvent();
                em.EventBus.RaiseLocalEvent(magazine, ref count);
                return count.Count;
            }
        });
        await pair.CleanReturnAsync();
    }

    private static void SetTestPower(IEntityManager em, EntityUid machine, bool powered)
    {
        // These fixtures isolate laundry behavior from APC network construction.
        var receiver = em.GetComponent<ApcPowerReceiverComponent>(machine);
        em.System<PowerReceiverSystem>().SetNeedsPower(machine, !powered, receiver);
        receiver.Powered = powered;
        em.Dirty(machine, receiver);
        var changed = new PowerChangedEvent(powered, powered ? receiver.Load : 0);
        em.EventBus.RaiseLocalEvent(machine, ref changed);
    }

}
