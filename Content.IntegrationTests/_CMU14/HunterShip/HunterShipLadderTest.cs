using System.Collections.Generic;
using System.Linq;
using Content.Server._CMU14.ZLevels.Core;
using Content.Server.Verbs;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.HunterShip;

[TestFixture]
public sealed class HunterShipLadderTest
{
    [Test]
    public async Task MiddleDeckLaddersOfferBothDirectionsAndPeekAtTheCorrectDeck()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var lowerMap = await pair.CreateTestMap();
        var middleMap = await pair.CreateTestMap();
        var upperMap = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid ladder = default;
        EntityUid network = default;
        AlternativeVerb climbUpVerb = default!;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transforms = entMan.System<SharedTransformSystem>();
            var verbs = entMan.System<VerbSystem>();
            var zLevels = entMan.System<CMUZLevelsSystem>();
            var zNetwork = zLevels.CreateZNetwork();
            network = zNetwork.Owner;

            Assert.That(zLevels.TryAddMapsIntoZNetwork(zNetwork, new Dictionary<EntityUid, int>
            {
                [lowerMap.MapUid] = -1,
                [middleMap.MapUid] = 0,
                [upperMap.MapUid] = 1,
            }), Is.True);

            user = entMan.SpawnEntity("CMMobHuman", middleMap.GridCoords);
            entMan.EnsureComponent<EyeComponent>(user);
            ladder = entMan.SpawnEntity(null, middleMap.GridCoords);
            var ladderComp = entMan.EnsureComponent<CMUZLevelLadderComponent>(ladder);
            ladderComp.Delay = TimeSpan.Zero;
            ladderComp.Offset = 1;
            ladderComp.CanMoveUp = true;
            ladderComp.CanMoveDown = true;

            var localVerbs = verbs.GetLocalVerbs(ladder, user, typeof(AlternativeVerb), force: true).ToArray();
            var climbUp = Loc.GetString("cmu-zlevel-ladder-climb-up");
            var climbDown = Loc.GetString("cmu-zlevel-ladder-climb-down");
            var lookUp = Loc.GetString("cmu-zlevel-ladder-look-up");
            var lookDown = Loc.GetString("cmu-zlevel-ladder-look-down");

            Assert.Multiple(() =>
            {
                Assert.That(localVerbs.Select(verb => verb.Text), Does.Contain(climbUp));
                Assert.That(localVerbs.Select(verb => verb.Text), Does.Contain(climbDown));
                Assert.That(localVerbs.Select(verb => verb.Text), Does.Contain(lookUp));
                Assert.That(localVerbs.Select(verb => verb.Text), Does.Contain(lookDown));
            });

            localVerbs.Single(verb => verb.Text == lookDown).Act!.Invoke();
            var watching = entMan.GetComponent<CMUZLevelLadderWatchingComponent>(user);
            Assert.That(watching.Offset, Is.EqualTo(-1));
            Assert.That(transforms.GetMapCoordinates(watching.PeekTarget!.Value).MapId, Is.EqualTo(lowerMap.MapId),
                "Looking down from the middle deck must target the lower deck, not the ladder's default upward offset.");

            localVerbs.Single(verb => verb.Text == lookUp).Act!.Invoke();
            watching = entMan.GetComponent<CMUZLevelLadderWatchingComponent>(user);
            Assert.That(watching.Offset, Is.EqualTo(1));
            Assert.That(transforms.GetMapCoordinates(watching.PeekTarget!.Value).MapId, Is.EqualTo(upperMap.MapId));

            climbUpVerb = localVerbs.OfType<AlternativeVerb>().Single(verb => verb.Text == climbUp);
            localVerbs.Single(verb => verb.Text == climbDown).Act!.Invoke();
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transforms = entMan.System<SharedTransformSystem>();
            Assert.That(transforms.GetMapCoordinates(user).MapId, Is.EqualTo(lowerMap.MapId));

            transforms.SetCoordinates(user, middleMap.GridCoords);
            climbUpVerb.Act!.Invoke();
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transforms = entMan.System<SharedTransformSystem>();
            Assert.That(transforms.GetMapCoordinates(user).MapId, Is.EqualTo(upperMap.MapId));

            foreach (var uid in new[] { user, ladder, network })
            {
                if (!entMan.Deleted(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AuthoredMiddleDeckLaddersAreBidirectional()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = entMan.ComponentFactory;

            foreach (var id in new[]
            {
                "CMUHunterShipPlacedCMUYautjaStructureYautjaMachinesLadder11SouthOffset0x17",
                "CMUHunterShipPlacedCMUYautjaStructureYautjaMachinesLadder11SouthOffset0x2",
            })
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryGetComponent<CMUZLevelLadderComponent>(out var ladder, factory), Is.True, id);
                Assert.Multiple(() =>
                {
                    Assert.That(ladder!.CanMoveUp, Is.True, id);
                    Assert.That(ladder.CanMoveDown, Is.True, id);
                });
            }
        });

        await pair.CleanReturnAsync();
    }
}
