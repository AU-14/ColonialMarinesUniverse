using Content.Client.VendingMachines;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._RMC14.Marines.HyperSleep;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Light.Components;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Storage.Components;
using Content.Shared.Stacks;
using Content.Shared.VendingMachines;
using Content.Shared.Verbs;
using Content.Server.CMU14.Light;
using Content.Server.Medical;
using Content.Server.Power.Components;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Client.Popups;
using Robust.Client.GameObjects;
using Robust.Shared.Audio.Components;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Content.Shared.Power.Components;
using ServerPointLightComponent = Robust.Server.GameObjects.PointLightComponent;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.IntegrationTests.CMU14.HunterShip;

[TestFixture]
public sealed class HunterShipVisualRegressionTest
{

    [Test]
    public async Task HunterShipMiddleLadderCanClimbBothDirections()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var lower = await pair.CreateTestMap();
        var middle = await pair.CreateTestMap();
        var upper = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid ladder = default;
        EntityUid network = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var zLevels = entMan.System<CMUZLevelsSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var zNetwork = zLevels.CreateZNetwork();
            network = zNetwork.Owner;
            Assert.That(zLevels.TryAddMapsIntoZNetwork(zNetwork, new Dictionary<EntityUid, int>
            {
                [lower.MapUid] = 0,
                [middle.MapUid] = 1,
                [upper.MapUid] = 2,
            }), Is.True);

            ladder = entMan.SpawnEntity(
                "CMUHunterShipPlacedCMUYautjaStructureYautjaMachinesLadder11SouthOffset0x2",
                middle.GridCoords);
            user = entMan.SpawnEntity("CMMobHuman", middle.GridCoords);

            var verbs = new GetVerbsEvent<AlternativeVerb>(user, ladder, null, null, true, true, true, new());
            entMan.EventBus.RaiseLocalEvent(ladder, verbs, true);
            var climbUp = verbs.Verbs.SingleOrDefault(verb =>
                verb.Text == Loc.GetString("cmu-zlevel-ladder-climb-up"));
            var climbDown = verbs.Verbs.SingleOrDefault(verb =>
                verb.Text == Loc.GetString("cmu-zlevel-ladder-climb-down"));

            Assert.Multiple(() =>
            {
                Assert.That(climbUp, Is.Not.Null, "The middle Hunter Ship ladder must offer climbing up.");
                Assert.That(climbDown, Is.Not.Null, "The middle Hunter Ship ladder must offer climbing down.");
            });
            climbUp!.Act!.Invoke();
            Assert.That(transform.GetMapCoordinates(user).MapId, Is.EqualTo(middle.MapId));
        });

        await server.WaitRunTicks(130);
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();
            Assert.That(transform.GetMapCoordinates(user).MapId, Is.EqualTo(upper.MapId));
            transform.SetCoordinates(user, middle.GridCoords);

            var verbs = new GetVerbsEvent<AlternativeVerb>(user, ladder, null, null, true, true, true, new());
            entMan.EventBus.RaiseLocalEvent(ladder, verbs, true);
            var climbDown = verbs.Verbs.Single(verb =>
                verb.Text == Loc.GetString("cmu-zlevel-ladder-climb-down"));
            climbDown.Act!.Invoke();
        });

        await server.WaitRunTicks(130);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.System<SharedTransformSystem>().GetMapCoordinates(user).MapId,
                Is.EqualTo(lower.MapId));
        });

        await server.WaitPost(() =>
        {
            if (user != default && !server.EntMan.Deleted(user))
                server.EntMan.DeleteEntity(user);
            if (ladder != default && !server.EntMan.Deleted(ladder))
                server.EntMan.DeleteEntity(ladder);
            if (network != default && !server.EntMan.Deleted(network))
                server.EntMan.DeleteEntity(network);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipFoodStorageContainsEdibleFood()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid locker = default;
        EntityUid firstFood = default;
        Content.Shared.FixedPoint.FixedPoint2 initialFoodVolume = default;
        var priorAudio = new HashSet<EntityUid>();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            locker = entMan.SpawnEntity("CMUHunterShipMeatLockerFoodMeatx16", map.GridCoords);
            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(1, 0)));
            var storage = entMan.GetComponent<EntityStorageComponent>(locker);

            Assert.That(storage.Contents.ContainedEntities, Has.Count.EqualTo(16));
            firstFood = storage.Contents.ContainedEntities.First();

            foreach (var item in storage.Contents.ContainedEntities)
            {
                Assert.That(entMan.HasComponent<EdibleComponent>(item), Is.True);
                var edible = entMan.GetComponent<EdibleComponent>(item);
                Assert.That(edible.UseSound ?? server.ProtoMan.Index(edible.Edible).UseSound, Is.Not.Null);
            }

            foreach (var id in new[]
                     {
                         "CMUHunterShipPlacedFoodBreadMeatXenoSliceXenobreadsliceSouthOffset5x4",
                         "CMUHunterShipPlacedFoodMeatMeatSouthOffset0x5",
                         "CMUHunterShipPlacedFoodPieXenoXenomeatpieSouth",
                     })
            {
                var placed = entMan.SpawnEntity(id, map.GridCoords.Offset(new Vector2(2, 0)));
                Assert.That(entMan.HasComponent<EdibleComponent>(placed), Is.True);
                var edible = entMan.GetComponent<EdibleComponent>(placed);
                Assert.That(edible.UseSound ?? server.ProtoMan.Index(edible.Edible).UseSound, Is.Not.Null, id);
            }
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var storage = entMan.GetComponent<EntityStorageComponent>(locker);
            var food = entMan.System<IngestionSystem>();
            var storageSystem = entMan.System<EntityStorageSystem>();
            var interaction = entMan.System<SharedInteractionSystem>();

            Assert.That(food.CanConsume(hunter, firstFood), Is.True);
            Assert.That(storageSystem.Remove(firstFood, locker, storage), Is.True);
            Assert.That(entMan.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>().TryPickupAnyHand(hunter, firstFood), Is.True);
            var hunterXform = entMan.GetComponent<TransformComponent>(hunter);
            var foodXform = entMan.GetComponent<TransformComponent>(firstFood);
            Assert.That(food.EdibleVolume((firstFood, entMan.GetComponent<EdibleComponent>(firstFood))), Is.GreaterThan(Content.Shared.FixedPoint.FixedPoint2.Zero));
            Assert.That(food.HasMouthAvailable(hunter), Is.True);
            var body = entMan.System<Content.Shared.Body.BodySystem>();
            var stomachs = body.EnumerateOrgans<StomachComponent>(hunter).Select(organ => new Robust.Shared.GameObjects.Entity<StomachComponent>(organ.Owner, organ.Comp2)).ToList();
            Assert.That(stomachs.Count > 0, Is.True,
                "The Yautja body must have a stomach for the current ingestion system.");
            Assert.That(food.IsDigestibleBy(firstFood, stomachs!, out _), Is.True,
                "Hunter ship meat must be digestible by the Yautja stomach.");
            Assert.That(interaction.InRangeAndAccessible(hunter, firstFood, IngestionSystem.MaxFeedDistance), Is.True,
                "Food must be accessible within eating range.");
            Assert.That(entMan.System<Content.Shared.ActionBlocker.ActionBlockerSystem>().CanInteract(hunter, firstFood), Is.True);
            Assert.That(interaction.InRangeUnobstructed(hunter, firstFood), Is.True,
                $"hunter={hunterXform.Coordinates} food={foodXform.Coordinates}");
            var result = food.TryIngest(hunter, firstFood);
            Assert.That(result, Is.True,
                $"started={result} hunter={hunterXform.Coordinates} food={foodXform.Coordinates}");
            initialFoodVolume = food.EdibleVolume((firstFood, entMan.GetComponent<EdibleComponent>(firstFood)));
            priorAudio = entMan.QueryEntities<AudioComponent>().Select(audio => audio.Owner).ToHashSet();
        });

        await PoolManager.WaitUntil(server,
            () => server.EntMan.QueryEntities<AudioComponent>().Any(audio => !priorAudio.Contains(audio.Owner)),
            maxTicks: 120);
        await server.WaitAssertion(() =>
        {
            var food = server.EntMan.System<IngestionSystem>();
            Assert.That(food.EdibleVolume((firstFood, server.EntMan.GetComponent<EdibleComponent>(firstFood))), Is.LessThan(initialFoodVolume),
                "The bite must consume food and create a sound after the ingestion delay.");
        });

        await pair.CleanReturnAsync();
    }

}
