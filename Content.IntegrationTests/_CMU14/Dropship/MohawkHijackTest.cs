using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Server.Shuttles.Systems;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared._RMC14.Dropship;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkHijackTest
{
    [TestCase("omaha", false)]
    [TestCase("omaha", true)]
    [TestCase("midway", false)]
    [TestCase("midway", true)]
    public async Task HijackCrashesOnlyTheCabinAndDisablesBoarding(string variant, bool humanHijack)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        EntityUid ship = default;
        EntityUid targetMap = default;
        EntityUid lowerMap = default;
        EntityUid passenger = default;
        EntityUid hijacker = default;
        EntityUid bystander = default;
        EntityUid[] secondaryDecks = [];
        NetEntity[] secondaryNets = [];
        var targetPosition = new Vector2(25, -31);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            secondaryDecks = assembly.Decks.Values.ToArray();
            secondaryNets = secondaryDecks.Select(deck => entities.GetNetEntity(deck)).ToArray();
            var lower = assembly.Decks[-1];
            lowerMap = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            passenger = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(ship, 0.5f, 0.5f));
            hijacker = humanHijack ? passenger : entities.SpawnEntity("CMXenoQueen", new EntityCoordinates(ship, 1.5f, 0.5f));
#pragma warning disable RA0002 // Exercise the same accepted crash flight with both hijacker types.
            entities.EnsureComponent<DropshipHijackerComponent>(hijacker).IsHumanHijacker = humanHijack;
#pragma warning restore RA0002
            bystander = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(lower, -2.5f, 1.5f));
            var mechanisms = entities.System<MohawkSystem>();
            Assert.That(mechanisms.SetRampDeployed(ship, true, true), Is.True);
            Assert.That(mechanisms.SetHatchDeployed(ship, true, true), Is.True);
        });
        await pair.RunUntilSynced();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            targetMap = maps.CreateMap();
            var marker = entities.SpawnEntity(null, new EntityCoordinates(targetMap, targetPosition));
            entities.AddComponent<DropshipDestinationComponent>(marker);
            var nav = entities.EntityQuery<DropshipNavigationComputerComponent>()
                .Single(c => entities.GetComponent<TransformComponent>(c.Owner).GridUid == ship);
            var dropship = entities.GetComponent<DropshipComponent>(ship);
            // Exercise the accepted hijack flight without the unrelated orbital
            // explosion destroying the cabin's test passenger before assertion.
#pragma warning disable RA0002 // Isolate flight/deck deletion from the orbital crash explosion in this fixture.
            dropship.AnnouncedCrash = true;
            dropship.DidIncomingSound = true;
            dropship.DidExplosion = true;
#pragma warning restore RA0002
            entities.System<ShuttleSystem>().DefaultArrivalTime = 0.5f;
            Assert.That(entities.System<SharedDropshipSystem>().FlyTo((nav.Owner, nav), marker, hijacker,
                hijack: true, startupTime: 0.5f, hyperspaceTime: 2f), Is.True);
            Assert.That(entities.HasComponent<MultiDeckDropshipComponent>(ship), Is.False);
            Assert.That(entities.HasComponent<DropshipDeckComponent>(ship), Is.False);
            Assert.That(entities.GetComponent<MohawkMechanismsComponent>(ship).RampDeployed, Is.False);
            Assert.That(entities.GetComponent<MohawkMechanismsComponent>(ship).HatchDeployed, Is.False);
        });
        await pair.RunSeconds(8);
        await pair.RunUntilSynced();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<TransformComponent>(ship).MapUid, Is.EqualTo(targetMap),
                "The cabin must hit the hijack marker's floor, not the level above it.");
            Assert.That(entities.System<SharedTransformSystem>().GetWorldPosition(ship), Is.EqualTo(targetPosition));
            Assert.That(entities.GetComponent<TransformComponent>(passenger).GridUid, Is.EqualTo(ship));
            Assert.That(entities.GetComponent<TransformComponent>(bystander).MapUid, Is.EqualTo(lowerMap),
                "Deleting the underside must not delete a person standing underneath it.");
            foreach (var deck in secondaryDecks)
                Assert.That(entities.Deleted(deck), Is.True);
            var mechanisms = entities.System<MohawkSystem>();
            Assert.That(entities.GetComponent<DropshipComponent>(ship).Crashed, Is.True);
            Assert.That(mechanisms.SetRampDeployed(ship, true), Is.False);
            Assert.That(mechanisms.SetRampDeployed(ship, true, true), Is.False);
            Assert.That(mechanisms.SetHatchDeployed(ship, true, true), Is.False);
            Assert.That(entities.HasComponent<MohawkRampMovingComponent>(ship), Is.False);
            Assert.That(entities.EntityQuery<MohawkRampSegmentComponent>().Any(c => c.Lower), Is.False);
            foreach (var point in entities.GetComponent<DropshipComponent>(ship).AttachmentPoints)
                Assert.That(entities.GetComponent<TransformComponent>(point).GridUid, Is.EqualTo(ship));
        });
        await pair.Client.WaitAssertion(() =>
        {
            foreach (var deck in secondaryNets)
                Assert.That(pair.Client.EntMan.TryGetEntity(deck, out _), Is.False,
                    "Connected clients must also remove the discarded decks.");
        });
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.DeleteEntity(ship));
        await pair.CleanReturnAsync();
    }
}
