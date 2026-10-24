using System.Linq;
using Content.Server.CMU14.Hygiene;
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

}
