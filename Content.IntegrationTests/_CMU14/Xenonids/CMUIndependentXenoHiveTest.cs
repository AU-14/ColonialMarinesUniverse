using System.Collections.Generic;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server._RMC14.Admin;
using Content.Server._RMC14.TacticalMap;
using Content.Server._RMC14.Xenonids.Hive;
using Content.Shared._CMU14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Maths;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Xenonids;

[TestFixture]
public sealed class CMUIndependentXenoHiveTest
{
    private static readonly string[] HunterShipEggs =
    [
        "CMUHunterShipPlacedBaseItemEggItemSouthOffset1x2",
        "CMUHunterShipPlacedBaseItemEggItemSouthOffset6x7",
        "CMUHunterShipPlacedBaseItemEggItemSouthOffset8x13",
        "CMUHunterShipPlacedBaseItemEggItemSouthOffsetNeg5x9",
        "CMUHunterShipPlacedBaseItemEggItemSouthOffsetNeg7x15",
        "CMUHunterShipPlacedBaseItemEggItemSouthVariant02Offset1xNeg2",
        "CMUHunterShipPlacedBaseItemEggItemSouthVariant02Offset9x10",
        "CMUHunterShipPlacedBaseItemEggItemSouthVariant02OffsetNeg7x11",
    ];

    [Test]
    public async Task HunterShipHivesUseSeparateColorsAndNpcFactions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        Assert.That(prototypes.HasIndex<EntityPrototype>("CMUHunterShipAlphaHive") &&
                    prototypes.HasIndex<EntityPrototype>("CMUHunterShipForsakenHive"), Is.True);

        Exception? callbackException = null;
        Color alphaColor = default;
        Color forsakenColor = default;
        HashSet<ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype>> alphaFactions = [];
        HashSet<ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype>> forsakenFactions = [];
        bool hivesAreHostile = false;

        await server.WaitPost(() =>
        {
            EntityUid alphaHive = EntityUid.Invalid;
            EntityUid forsakenHive = EntityUid.Invalid;
            EntityUid alphaParasite = EntityUid.Invalid;
            EntityUid forsakenParasite = EntityUid.Invalid;

            try
            {
                var entMan = server.EntMan;
                var hives = entMan.System<XenoHiveSystem>();
                alphaHive = hives.CreateHive("Hunter Ship Alpha Hive", "CMUHunterShipAlphaHive");
                forsakenHive = hives.CreateHive("Hunter Ship Forsaken Hive", "CMUHunterShipForsakenHive");
                alphaParasite = entMan.SpawnEntity("CMXenoParasite", MapCoordinates.Nullspace);
                forsakenParasite = entMan.SpawnEntity("CMXenoParasite", MapCoordinates.Nullspace);
                hives.SetHive(alphaParasite, alphaHive);
                hives.SetHive(forsakenParasite, forsakenHive);

                alphaColor = entMan.GetComponent<HiveComponent>(alphaHive).HiveColor;
                forsakenColor = entMan.GetComponent<HiveComponent>(forsakenHive).HiveColor;
                alphaFactions = new(entMan.GetComponent<NpcFactionMemberComponent>(alphaParasite).Factions);
                forsakenFactions = new(entMan.GetComponent<NpcFactionMemberComponent>(forsakenParasite).Factions);

                var npcFaction = entMan.System<NpcFactionSystem>();
                hivesAreHostile = !npcFaction.IsEntityFriendly(
                                       (alphaParasite, entMan.GetComponent<NpcFactionMemberComponent>(alphaParasite)),
                                       (forsakenParasite, entMan.GetComponent<NpcFactionMemberComponent>(forsakenParasite))) &&
                                   !npcFaction.IsEntityFriendly(
                                       (forsakenParasite, entMan.GetComponent<NpcFactionMemberComponent>(forsakenParasite)),
                                       (alphaParasite, entMan.GetComponent<NpcFactionMemberComponent>(alphaParasite)));
            }
            catch (Exception e)
            {
                callbackException = e;
            }
            finally
            {
                var entMan = server.EntMan;
                if (alphaParasite.IsValid()) entMan.DeleteEntity(alphaParasite);
                if (forsakenParasite.IsValid()) entMan.DeleteEntity(forsakenParasite);
                if (alphaHive.IsValid()) entMan.DeleteEntity(alphaHive);
                if (forsakenHive.IsValid()) entMan.DeleteEntity(forsakenHive);
            }
        });

        var checksPass = callbackException is null &&
                         alphaColor == Color.FromHex("#ff4040") &&
                         forsakenColor == Color.FromHex("#cc8ec4") &&
                         alphaFactions.Contains("CMUXenoAlpha") &&
                         !alphaFactions.Contains("RMCXeno") &&
                         forsakenFactions.Contains("CMUXenoForsaken") &&
                         !forsakenFactions.Contains("RMCXeno") &&
                         !alphaFactions.Overlaps(forsakenFactions) &&
                         hivesAreHostile;

        Assert.That(checksPass, Is.True,
            callbackException?.ToString() ??
            $"alpha={alphaColor}; forsaken={forsakenColor}; alphaFactions={string.Join(',', alphaFactions)}; forsakenFactions={string.Join(',', forsakenFactions)}; hostile={hivesAreHostile}");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipSpecimensUseGameplayPrototypesAndHiveAssignments()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var components = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.That(prototypes.TryIndex<EntityPrototype>(
                "CMUHunterShipObjEffectAlienEggForsakenEggGrowingSouth", out var egg), Is.True);
            Assert.That(prototypes.TryIndex<EntityPrototype>(
                "CMUHunterShipObjEffectAlienWeedsNodeForsakenWeednodeSouth", out var weeds), Is.True);

            Assert.That(egg!.TryComp<XenoEggComponent>(out var eggComp, components), Is.True);
            Assert.That(egg.TryComp<CMUHunterShipHiveAssignmentComponent>(out var eggAssignment, components), Is.True);
            Assert.That(weeds!.TryComp<HiveWeedsComponent>(out _, components), Is.True);
            Assert.That(weeds.TryComp<CMUHunterShipHiveAssignmentComponent>(out var weedsAssignment, components), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(eggComp!.State, Is.EqualTo(XenoEggState.Growing));
                Assert.That(eggComp.CanSpawnGhostParasite, Is.False);
                Assert.That(eggComp.CurrentSprite, Is.EqualTo("_CMU14/HunterShip/mob/xenos/effects.rsi"));
                Assert.That(eggAssignment!.Hive, Is.EqualTo(CMUHunterShipHiveKind.Forsaken));
                Assert.That(weedsAssignment!.Hive, Is.EqualTo(CMUHunterShipHiveKind.Forsaken));
            });

            foreach (var prototypeId in HunterShipEggs)
            {
                Assert.That(prototypes.TryIndex<EntityPrototype>(prototypeId, out var itemEgg), Is.True,
                    prototypeId);
                Assert.That(itemEgg!.TryComp<XenoEggComponent>(out var itemComp, components), Is.True,
                    prototypeId);
                Assert.That(itemEgg.TryComp<CMUHunterShipHiveAssignmentComponent>(out var assignment, components), Is.True,
                    prototypeId);
                Assert.That(itemComp!.CurrentSprite, Is.EqualTo("_CMU14/HunterShip/mob/xenos/effects.rsi"),
                    prototypeId);
                Assert.That(assignment!.Hive, Is.EqualTo(prototypeId.Contains("Variant02")
                    ? CMUHunterShipHiveKind.Forsaken
                    : CMUHunterShipHiveKind.Alpha), prototypeId);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipHatchedXenosAreIncludedInAdminCount()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hives = entMan.System<XenoHiveSystem>();
            var alphaHive = hives.CreateHive("Hunter Ship Alpha Hive", "CMUHunterShipAlphaHive");
            var forsakenHive = hives.CreateHive("Hunter Ship Forsaken Hive", "CMUHunterShipForsakenHive");
            var alphaXeno = entMan.SpawnEntity("CMXenoParasite", MapCoordinates.Nullspace);
            var forsakenXeno = entMan.SpawnEntity("CMXenoParasite", MapCoordinates.Nullspace);

            try
            {
                hives.SetHive(alphaXeno, alphaHive);
                hives.SetHive(forsakenXeno, forsakenHive);

                var state = RMCAdminEui.CreateState(entMan, default);
                Assert.That(state.Xenos.Count, Is.EqualTo(2));
            }
            finally
            {
                entMan.DeleteEntity(alphaXeno);
                entMan.DeleteEntity(forsakenXeno);
                entMan.DeleteEntity(alphaHive);
                entMan.DeleteEntity(forsakenHive);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipTacticalMapOnlyShowsOwnHive()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid alphaHive = default;
        EntityUid forsakenHive = default;
        EntityUid alphaXeno = default;
        EntityUid forsakenXeno = default;
        EntityUid alphaStructure = default;
        EntityUid forsakenStructure = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hives = entMan.System<XenoHiveSystem>();
                var tacticalMaps = entMan.System<TacticalMapSystem>();
                entMan.EnsureComponent<TacticalMapComponent>(map.Grid);

                alphaHive = hives.CreateHive("Hunter Ship Alpha Hive", "CMUHunterShipAlphaHive");
                forsakenHive = hives.CreateHive("Hunter Ship Forsaken Hive", "CMUHunterShipForsakenHive");
                alphaXeno = entMan.SpawnEntity("CMXenoQueen", map.GridCoords);
                forsakenXeno = entMan.SpawnEntity("CMXenoQueen", map.GridCoords);
                alphaStructure = entMan.SpawnEntity("HiveCoreXeno", map.GridCoords);
                forsakenStructure = entMan.SpawnEntity("HiveCoreXeno", map.GridCoords);

                hives.SetHive(alphaXeno, alphaHive);
                hives.SetHive(forsakenXeno, forsakenHive);
                hives.SetHive(alphaStructure, alphaHive);
                hives.SetHive(forsakenStructure, forsakenHive);

                tacticalMaps.RefreshTracked(alphaXeno);
                tacticalMaps.RefreshTracked(forsakenXeno);
                tacticalMaps.RefreshTracked(alphaStructure);
                tacticalMaps.RefreshTracked(forsakenStructure);
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var tacticalMaps = entMan.System<TacticalMapSystem>();
                var tacticalMap = entMan.GetComponent<TacticalMapComponent>(map.Grid);
                var alphaUser = entMan.GetComponent<TacticalMapUserComponent>(alphaXeno);
                var forsakenUser = entMan.GetComponent<TacticalMapUserComponent>(forsakenXeno);

                tacticalMaps.UpdateUserData((alphaXeno, alphaUser), tacticalMap);
                tacticalMaps.UpdateUserData((forsakenXeno, forsakenUser), tacticalMap);

                Assert.Multiple(() =>
                {
                    Assert.That(alphaUser.XenoBlips, Does.ContainKey(alphaXeno.Id));
                    Assert.That(alphaUser.XenoBlips, Does.Not.ContainKey(forsakenXeno.Id));
                    Assert.That(alphaUser.XenoStructureBlips, Does.ContainKey(alphaStructure.Id));
                    Assert.That(alphaUser.XenoStructureBlips, Does.Not.ContainKey(forsakenStructure.Id));
                    Assert.That(forsakenUser.XenoBlips, Does.ContainKey(forsakenXeno.Id));
                    Assert.That(forsakenUser.XenoBlips, Does.Not.ContainKey(alphaXeno.Id));
                    Assert.That(forsakenUser.XenoStructureBlips, Does.ContainKey(forsakenStructure.Id));
                    Assert.That(forsakenUser.XenoStructureBlips, Does.Not.ContainKey(alphaStructure.Id));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                if (entMan.EntityExists(alphaXeno))
                    entMan.DeleteEntity(alphaXeno);

                if (entMan.EntityExists(forsakenXeno))
                    entMan.DeleteEntity(forsakenXeno);

                if (entMan.EntityExists(alphaStructure))
                    entMan.DeleteEntity(alphaStructure);

                if (entMan.EntityExists(forsakenStructure))
                    entMan.DeleteEntity(forsakenStructure);

                if (entMan.EntityExists(alphaHive))
                    entMan.DeleteEntity(alphaHive);

                if (entMan.EntityExists(forsakenHive))
                    entMan.DeleteEntity(forsakenHive);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipMapBootstrapsIndependentSpecimenHives()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var ticker = entMan.System<GameTicker>();

        await server.WaitAssertion(() =>
        {
            var map = prototypes.Index<GameMapPrototype>("CMUYautjaHunterShip");
            var options = DeserializationOptions.Default with { InitializeMaps = true };
            Assert.DoesNotThrow(() => ticker.LoadGameMap(map, out _, options));
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            EntityUid? alphaHive = null;
            EntityUid? forsakenHive = null;

            var bootstraps = entMan.EntityQueryEnumerator<CMUHunterShipHiveBootstrapComponent>();
            while (bootstraps.MoveNext(out _, out var bootstrap))
            {
                alphaHive ??= bootstrap.AlphaHive;
                forsakenHive ??= bootstrap.ForsakenHive;
            }

            Assert.That(alphaHive, Is.Not.Null, "Hunter Ship station must create its Alpha hive.");
            Assert.That(forsakenHive, Is.Not.Null, "Hunter Ship station must create its Forsaken hive.");

            var alphaAssignments = 0;
            var forsakenAssignments = 0;
            var assignments = entMan.EntityQueryEnumerator<CMUHunterShipHiveAssignmentComponent, HiveMemberComponent>();
            while (assignments.MoveNext(out _, out var assignment, out var member))
            {
                if (assignment.Hive == CMUHunterShipHiveKind.Alpha)
                {
                    alphaAssignments++;
                    Assert.That(member.Hive, Is.EqualTo(alphaHive));
                }
                else
                {
                    forsakenAssignments++;
                    Assert.That(member.Hive, Is.EqualTo(forsakenHive));
                }
            }

            Assert.That(alphaAssignments, Is.GreaterThan(0));
            Assert.That(forsakenAssignments, Is.GreaterThan(0));
        });

        await pair.CleanReturnAsync();
    }
}
