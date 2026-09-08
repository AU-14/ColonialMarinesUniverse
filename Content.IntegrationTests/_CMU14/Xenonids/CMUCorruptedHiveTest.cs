using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._CMU14.Xenonids.CorruptedHive;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests._CMU14.Xenonids;

[TestFixture]
public sealed class CMUCorruptedHiveTest
{
    [TestCase(XenoEggState.Item, false)]
    [TestCase(XenoEggState.Grown, true)]
    public async Task CorruptedCipheringConvertsLooseAndPlantedEggs(XenoEggState state, bool anchored)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid egg = default;
        EntityUid parasite = default;
        EntityUid corruptedHive = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            egg = entities.SpawnEntity("XenoEgg", map.GridCoords);
            var eggComp = entities.GetComponent<XenoEggComponent>(egg);
            eggComp.State = state;

            if (anchored)
            {
                var transform = entities.System<SharedTransformSystem>();
                transform.AnchorEntity((egg, entities.GetComponent<TransformComponent>(egg)));
            }

            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<CMUCipherableXenoEggComponent>(egg), Is.True);
                Assert.That(entities.HasComponent<InjectableSolutionComponent>(egg), Is.True);
            });

            entities.System<ReactiveSystem>().DoEntityReaction(
                egg,
                new Solution("CMUCorruptedCipherToxin", FixedPoint2.New(1)),
                ReactionMethod.Injection);

            var parasiteQuery = entities.EntityQueryEnumerator<CMUCorruptedParasiteComponent>();
            Assert.That(parasiteQuery.MoveNext(out parasite, out _), Is.True);
            Assert.That(parasiteQuery.MoveNext(out _, out _), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<MetaDataComponent>(parasite).EntityPrototype?.ID,
                    Is.EqualTo("CMUCorruptedXenoParasite"));
                Assert.That(entities.HasComponent<ParasiteAIComponent>(parasite), Is.True);
                Assert.That(entities.HasComponent<GhostRoleComponent>(parasite), Is.True);
                Assert.That(entities.HasComponent<GhostTakeoverAvailableComponent>(parasite), Is.True);
                Assert.That(entities.System<GhostRoleSystem>().GhostRoles.Select(role => role.Owner),
                    Does.Contain(parasite));
            });

            var ghostRole = entities.GetComponent<GhostRoleComponent>(parasite);
            var timeRequirement = ghostRole.Requirements!.OfType<RoleTimeRequirement>().Single();
            Assert.Multiple(() =>
            {
                Assert.That(timeRequirement.Role.Id, Is.EqualTo("AUJobThreatMember"));
                Assert.That(timeRequirement.Time, Is.EqualTo(TimeSpan.FromHours(5)));
            });

            var hive = entities.System<SharedXenoHiveSystem>().GetHive(parasite);
            Assert.That(hive, Is.Not.Null);
            corruptedHive = hive!.Value.Owner;
            Assert.That(entities.GetComponent<MetaDataComponent>(corruptedHive).EntityPrototype?.ID,
                Is.EqualTo("CMUCorruptedHive"));
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.Deleted(egg), Is.True);

            if (!entities.Deleted(parasite))
                entities.DeleteEntity(parasite);
            if (!entities.Deleted(corruptedHive))
                entities.DeleteEntity(corruptedHive);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task QueenDecliningPublishesParasiteGhostRole()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var player = server.PlayerMan.Sessions.Single();
        var originalAttached = player.AttachedEntity;

        EntityUid queen = default;
        EntityUid primeHive = default;
        EntityUid egg = default;
        EntityUid parasite = default;
        EntityUid corruptedHive = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entities = server.EntMan;
                var hiveSystem = entities.System<SharedXenoHiveSystem>();

                queen = entities.SpawnEntity("CMXenoQueen", map.GridCoords);
                primeHive = entities.SpawnEntity("CMXenoHive", map.GridCoords);
                hiveSystem.SetHive(queen, primeHive);
                server.PlayerMan.SetAttachedEntity(player, queen);
                Assert.That(entities.HasComponent<ActorComponent>(queen), Is.True);

                entities.System<MobStateSystem>().ChangeMobState(queen, MobState.Dead);

                egg = entities.SpawnEntity("XenoEgg", map.GridCoords);
                entities.System<ReactiveSystem>().DoEntityReaction(
                    egg,
                    new Solution("CMUCorruptedCipherToxin", FixedPoint2.New(1)),
                    ReactionMethod.Injection);

                var parasiteQuery = entities.EntityQueryEnumerator<CMUCorruptedParasiteComponent>();
                Assert.That(parasiteQuery.MoveNext(out parasite, out var corrupted), Is.True);
                Assert.That(corrupted.ReservedFor, Is.EqualTo(player.UserId));
                Assert.That(corrupted.OfferId, Is.Not.Zero);
                Assert.That(entities.HasComponent<GhostTakeoverAvailableComponent>(parasite), Is.False);
                Assert.That(entities.System<GhostRoleSystem>().GhostRoles.Select(role => role.Owner),
                    Does.Not.Contain(parasite));

                entities.EventBus.RaiseEvent(
                    EventSource.Local,
                    new CMUCorruptedParasiteClaimChoiceEvent(
                        entities.GetNetEntity(queen),
                        entities.GetNetEntity(parasite),
                        corrupted.OfferId,
                        false));

                Assert.Multiple(() =>
                {
                    Assert.That(corrupted.ReservedFor, Is.Null);
                    Assert.That(corrupted.ReservationExpiresAt, Is.Null);
                    Assert.That(corrupted.OfferId, Is.Zero);
                    Assert.That(entities.HasComponent<ParasiteAIComponent>(parasite), Is.True);
                    Assert.That(entities.HasComponent<GhostRoleComponent>(parasite), Is.True);
                    Assert.That(entities.HasComponent<GhostTakeoverAvailableComponent>(parasite), Is.True);
                    Assert.That(entities.System<GhostRoleSystem>().GhostRoles.Select(role => role.Owner),
                        Does.Contain(parasite));
                });

                corruptedHive = hiveSystem.GetHive(parasite)!.Value.Owner;
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(player, originalAttached);
                var entities = server.EntMan;
                foreach (var entity in new[] { egg, parasite, queen, primeHive, corruptedHive })
                {
                    if (entity.Valid && !entities.Deleted(entity))
                        entities.DeleteEntity(entity);
                }
            });

            await pair.RunTicksSync(2);
            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task OpeningEggCannotBeConverted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var egg = entities.SpawnEntity("XenoEgg", map.GridCoords);

            try
            {
                entities.GetComponent<XenoEggComponent>(egg).State = XenoEggState.Opening;
                entities.System<ReactiveSystem>().DoEntityReaction(
                    egg,
                    new Solution("CMUCorruptedCipherToxin", FixedPoint2.New(1)),
                    ReactionMethod.Injection);

                Assert.That(entities.GetComponent<CMUCipherableXenoEggComponent>(egg).Converted, Is.False);
                Assert.That(entities.Deleted(egg), Is.False);

                var parasiteQuery = entities.EntityQueryEnumerator<CMUCorruptedParasiteComponent>();
                Assert.That(parasiteQuery.MoveNext(out _, out _), Is.False);
            }
            finally
            {
                entities.DeleteEntity(egg);
            }
        });

        await pair.CleanReturnAsync();
    }
}
