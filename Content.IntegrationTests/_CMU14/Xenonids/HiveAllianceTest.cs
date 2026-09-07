using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;

namespace Content.IntegrationTests.CMU14.Xenonids;

[TestFixture]
[TestOf(typeof(SharedXenoHiveSystem))]
public sealed class HiveAllianceTest : GameTest
{
    private EntityUid _hive;
    private EntityUid _otherHive;
    private EntityUid _xeno;
    private EntityUid _first;
    private EntityUid _second;
    private EntityUid _outsider;
    private readonly List<EntityUid> _spawned = new();
    private EntityUid? _originalAttached;

    [TestCase("GOVFOR")]
    [TestCase("AUWeYu")]
    [TestCase("CLF")]
    public async Task FactionAlliancesReplicateForEveryMemberAndTrackMembership(string faction)
    {
        await SetUpEntities(faction);
        await AssertAllies(false, false);

        await Server.WaitPost(() =>
            Server.System<SharedXenoHiveSystem>().SetHiveFactionAlly(faction, _hive, true));
        await AssertAllies(true, true);

        // An alliance must also cover people who arrive after it was made.
        EntityUid lateJoiner = default;
        await Server.WaitPost(() =>
        {
            lateJoiner = Spawn("MobHuman", SEntMan.GetComponent<TransformComponent>(_first).Coordinates);
            SetFaction(lateJoiner, faction);
        });
        await AssertAlly(lateJoiner, true);

        // Membership changes on an already replicated component must reach clients too.
        await Server.WaitPost(() => Server.System<NpcFactionSystem>().RemoveFaction(_first, faction));
        await AssertAllies(false, true);
        await Server.WaitPost(() => Server.System<NpcFactionSystem>().AddFaction(_first, faction));
        await AssertAllies(true, true);

        // Viewing the same people from a different hive must not reveal the ally marker.
        await Server.WaitPost(() => Server.System<SharedXenoHiveSystem>().SetHive(_xeno, _otherHive));
        await AssertAllies(true, true, viewerAllied: false);
        await Server.WaitPost(() => Server.System<SharedXenoHiveSystem>().SetHive(_xeno, _hive));

        await Server.WaitPost(() =>
            Server.System<SharedXenoHiveSystem>().SetHiveFactionAlly(faction, _hive, false));
        await AssertAllies(false, false);
        await AssertAlly(lateJoiner, false);
    }

    [Test]
    public async Task PersonalAlliancesReplicateAndBulkRemovalPreservesFactionAllies()
    {
        await SetUpEntities("CLF");
        await AssertAllies(false, false);

        await Server.WaitPost(() =>
        {
            var hive = Server.System<SharedXenoHiveSystem>();
            hive.SetHiveIndividualAlly(_first, _hive, true);
            hive.SetHiveIndividualAlly(_second, _hive, true);
            // Personal allies do not need faction membership.
            Server.System<NpcFactionSystem>().ClearFactions(_first);
        });
        await AssertAllies(true, true);

        await Server.WaitPost(() =>
            Server.System<SharedXenoHiveSystem>().SetHiveIndividualAlly(_first, _hive, false));
        await AssertAllies(false, true);

        await Server.WaitPost(() =>
        {
            var hive = Server.System<SharedXenoHiveSystem>();
            hive.SetHiveIndividualAlly(_first, _hive, true);
            hive.SetHiveFactionAlly("CLF", _hive, true);
        });
        await AssertAllies(true, true);

        await Server.WaitPost(() => Server.System<SharedXenoHiveSystem>().ClearHiveIndividualAllies(_hive));
        await AssertAllies(false, true);

        await Server.WaitPost(() => Server.System<SharedXenoHiveSystem>().SetHiveFactionAlly("CLF", _hive, false));
        await AssertAllies(false, false);
    }

    private async Task SetUpEntities(string faction)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            _originalAttached = ServerSession!.AttachedEntity;
            _hive = Spawn("CMUCorruptedHive", map.GridCoords);
            _otherHive = Spawn("CMUAlphaHive", map.GridCoords);
            _xeno = Spawn("CMXenoRunner", map.GridCoords);
            _first = Spawn("MobHuman", map.GridCoords);
            _second = Spawn("MobHuman", map.GridCoords);
            _outsider = Spawn("MobHuman", map.GridCoords);
            SetFaction(_first, faction);
            SetFaction(_second, faction);
            Server.System<NpcFactionSystem>().ClearFactions(_outsider);
            Server.System<SharedXenoHiveSystem>().SetHive(_xeno, _hive);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, _xeno);
        });
        await Pair.RunUntilSynced();
    }

    private EntityUid Spawn(string prototype, EntityCoordinates coordinates)
    {
        var entity = SEntMan.SpawnEntity(prototype, coordinates);
        _spawned.Add(entity);
        return entity;
    }

    private void SetFaction(EntityUid entity, string faction)
    {
        var factions = Server.System<NpcFactionSystem>();
        factions.ClearFactions(entity);
        factions.AddFaction(entity, faction);
    }

    private async Task AssertAllies(bool first, bool second, bool viewerAllied = true)
    {
        await AssertAlly(_first, first, viewerAllied);
        await AssertAlly(_second, second, viewerAllied);
        await AssertAlly(_outsider, false, viewerAllied);
    }

    private async Task AssertAlly(EntityUid target, bool allied, bool viewerAllied = true)
    {
        NetEntity targetNet = default;
        NetEntity hiveNet = default;
        NetEntity otherHiveNet = default;
        NetEntity xenoNet = default;
        await Server.WaitAssertion(() =>
        {
            targetNet = SEntMan.GetNetEntity(target);
            hiveNet = SEntMan.GetNetEntity(_hive);
            otherHiveNet = SEntMan.GetNetEntity(_otherHive);
            xenoNet = SEntMan.GetNetEntity(_xeno);
            AssertState(SEntMan, target, _hive, _otherHive, _xeno, allied, viewerAllied);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var clientTarget = CEntMan.GetEntity(targetNet);
            var clientXeno = CEntMan.GetEntity(xenoNet);
            AssertState(CEntMan, clientTarget, CEntMan.GetEntity(hiveNet), CEntMan.GetEntity(otherHiveNet),
                clientXeno, allied, viewerAllied);

            Assert.That(CEntMan.HasComponent<StatusIconComponent>(clientTarget), Is.True);
            var icons = new GetStatusIconsEvent(new List<StatusIconData>());
            CEntMan.EventBus.RaiseLocalEvent(clientTarget, ref icons);
            Assert.That(icons.StatusIcons.Contains(CProtoMan.Index<FactionIconPrototype>("CMUXenoHiveAlly")),
                Is.EqualTo(allied && viewerAllied), "Only the allied hive should see the marker.");

            var ownIcons = new GetStatusIconsEvent(new List<StatusIconData>());
            CEntMan.EventBus.RaiseLocalEvent(clientXeno, ref ownIcons);
            Assert.That(ownIcons.StatusIcons.Contains(CProtoMan.Index<FactionIconPrototype>("CMUXenoHiveAlly")),
                Is.False, "Hive members already have their own xeno HUD indicators.");
        });
    }

    private static void AssertState(IEntityManager entities, EntityUid target, EntityUid hive,
        EntityUid otherHive, EntityUid xeno, bool allied, bool viewerAllied)
    {
        var hives = entities.System<SharedXenoHiveSystem>();
        Assert.That(hives.IsAllyOfHive(target, hive), Is.EqualTo(allied));
        Assert.That(hives.IsAllyOfHive(target, otherHive), Is.False);
        var attack = new AttackAttemptEvent(xeno, target);
        entities.EventBus.RaiseLocalEvent(xeno, attack);
        Assert.That(attack.Cancelled, Is.EqualTo(allied && viewerAllied),
            "Server and predicted client attacks must agree about hive allies.");
    }

    [TearDown]
    public async Task CleanUpEntities()
    {
        await Server.WaitPost(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, _originalAttached);
            foreach (var entity in Enumerable.Reverse(_spawned))
                SEntMan.DeleteEntity(entity);
        });
    }
}
