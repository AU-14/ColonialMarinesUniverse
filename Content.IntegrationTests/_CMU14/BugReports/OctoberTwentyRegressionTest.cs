using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Marines.Roles.Ranks;
using Content.Server.CMU14.Marines.Roles.Ranks;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Tracker;
using Content.Shared._RMC14.UserInterface;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Audio.Jukebox;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Marines.Roles.Ranks;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Round.Antags.Replicant;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberTwentyRegressionTest : GameTest
{
    private static readonly ProtoId<RankPrototype> CivilianOfficer = "RMCRankCivilianOfficer";
    private static readonly ProtoId<RankPrototype> CivilianDoctor = "RMCRankCivilianDoctor";
    private static readonly ProtoId<NpcFactionPrototype> Govfor = "GOVFOR";
    private static readonly ProtoId<NpcFactionPrototype> Colonist = "AUColonist";
    private static readonly EntProtoId HealAction = "ActionXenoHeal";

    // the rmc ration still said TSE and UNMC on our maps
    [Test]
    public async Task TweRationUsesOurFactionNames()
    {
        await Server.WaitAssertion(() =>
        {
            var proto = SProtoMan.Index<EntityPrototype>("RMCMRETSE");
            Assert.That(proto.Name, Is.EqualTo("TWE ORP ration"));
            Assert.That(proto.Description, Does.Not.Contain("UNMC").And.Not.Contain("TSE"));
        });
    }

    // the jukebox had no xeno block, so xenos could play songs
    [Test]
    public async Task XenosCannotOpenTheJukebox()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var ui = Server.System<RMCUserInterfaceSystem>();
            var jukebox = SEntMan.SpawnEntity("Jukebox", map.GridCoords);
            var xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(ui.CanOpenUI((jukebox, null), (xeno, null), JukeboxUiKey.Key), Is.False);
            Assert.That(ui.CanOpenUI((jukebox, null), (human, null), JukeboxUiKey.Key), Is.True);
        });
    }

    // world actions refused any target on another map, and every floor is its own map
    [Test]
    public async Task QueenHealReachesAnotherFloor()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var zLevels = Server.System<CMUZLevelsSystem>();
            var actions = Server.System<SharedActionsSystem>();
            var network = zLevels.CreateZNetwork();
            var lower = maps.CreateMap(runMapInit: true);
            var upper = maps.CreateMap(runMapInit: true);
            var unrelated = maps.CreateMap(runMapInit: true);
            try
            {
                SEntMan.EnsureComponent<MapGridComponent>(lower);
                SEntMan.EnsureComponent<MapGridComponent>(upper);
                SEntMan.EnsureComponent<MapGridComponent>(unrelated);
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int> { [lower] = 0, [upper] = 1 }), Is.True);

                var queen = SEntMan.SpawnEntity("CMXenoQueen", new EntityCoordinates(lower, Vector2.Zero));
                // the heal comes with the ovipositor
                var heal = actions.AddAction(queen, HealAction);
                Assert.That(heal, Is.Not.Null);
                var world = (heal!.Value, SEntMan.GetComponent<WorldTargetActionComponent>(heal.Value));

                Assert.That(actions.ValidateWorldTarget(queen, new EntityCoordinates(upper, new Vector2(3, 3)), world), Is.True);
                Assert.That(actions.ValidateWorldTarget(queen, new EntityCoordinates(unrelated, new Vector2(3, 3)), world), Is.False);
                SEntMan.DeleteEntity(queen);
            }
            finally
            {
                SEntMan.DeleteEntity(lower);
                SEntMan.DeleteEntity(upper);
                SEntMan.DeleteEntity(unrelated);
                SEntMan.DeleteEntity(network);
            }
        });
    }

    // dying took the queen's trackable marker away and reviving never gave it back
    [Test]
    public async Task RevivedQueenIsTrackableAgain()
    {
        var map = await Pair.CreateTestMap();
        EntityUid queen = default;
        await Server.WaitAssertion(() =>
        {
            queen = SEntMan.SpawnEntity("CMXenoQueen", map.GridCoords);
            Assert.That(SEntMan.HasComponent<RMCTrackableComponent>(queen), Is.True);
            Server.System<MobStateSystem>().ChangeMobState(queen, MobState.Dead);
        });
        await RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RMCTrackableComponent>(queen), Is.False, "dead queens stop being tracked");
            Server.System<MobStateSystem>().ChangeMobState(queen, MobState.Alive);
        });
        await RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RMCTrackableComponent>(queen), Is.True);
            SEntMan.DeleteEntity(queen);
        });
    }

    // jobs without ranks had nothing to go back to, so a removed chevron's rank stuck
    [Test]
    public async Task RemovedChevronTakesItsRankWithIt()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var changers = Server.System<RankChangerSystem>();
            var ranks = Server.System<RankSystem>();

            var unranked = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var chevron = SEntMan.SpawnEntity(null, map.GridCoords);
            var changer = SEntMan.AddComponent<RankChangerComponent>(chevron);
            changer.Rank = CivilianOfficer;
            changers.ApplyRank(unranked, changer);
            Assert.That(SEntMan.GetComponent<RankComponent>(unranked).Rank, Is.EqualTo(CivilianOfficer));
            changers.RevertRank(unranked, changer);
            Assert.That(SEntMan.GetComponent<RankComponent>(unranked).Rank, Is.Null);

            var ranked = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            ranks.SetRank(ranked, CivilianDoctor);
            changers.ApplyRank(ranked, changer);
            changers.RevertRank(ranked, changer);
            Assert.That(SEntMan.GetComponent<RankComponent>(ranked).Rank, Is.EqualTo(CivilianDoctor));
        });
    }

    // the copy only took name and looks, examine and huds still read the replicant's own job
    [Test]
    public async Task ReplicantCopiesRankAndFaction()
    {
        var map = await Pair.CreateTestMap();
        EntityUid replicant = default;
        await Server.WaitAssertion(() =>
        {
            var ranks = Server.System<RankSystem>();
            var factions = Server.System<NpcFactionSystem>();
            replicant = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            ranks.SetRank(replicant, CivilianOfficer);
            ranks.SetRank(target, CivilianDoctor);
            factions.ClearFactions(replicant);
            factions.AddFaction(replicant, Govfor);
            factions.ClearFactions(target);
            factions.AddFaction(target, Colonist);

            var comp = SEntMan.AddComponent<ReplicantComponent>(replicant);
            comp.ChannelDelay = TimeSpan.FromSeconds(0.1);
            var picked = new ReplicantPickedMessage(SEntMan.GetNetEntity(target))
            {
                Actor = replicant,
                Entity = SEntMan.GetNetEntity(replicant),
                UiKey = ReplicantUiKey.Key,
            };
            SEntMan.EventBus.RaiseLocalEvent(replicant, picked);
        });
        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<ReplicantComponent>(replicant).Transformed, Is.True);
            Assert.That(SEntMan.GetComponent<RankComponent>(replicant).Rank, Is.EqualTo(CivilianDoctor));
            var factions = SEntMan.GetComponent<NpcFactionMemberComponent>(replicant).Factions;
            Assert.That(factions, Does.Contain(Colonist));
            Assert.That(factions, Does.Not.Contain(Govfor));
        });
    }

    // a new heart put into a corpse kept beating, so the defib said the heart was already beating
    [Test]
    public async Task FreshHeartInACorpseCanBeDefibbed()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var index = Server.System<CMUMedicalBodyIndexSystem>();
            var bodies = Server.System<SharedBodySystem>();
            var hearts = Server.System<SharedHeartSystem>();
            var coordinates = Server.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);

            var patient = SEntMan.SpawnEntity("CMMobHuman", coordinates);
            Assert.That(index.TryGetOrgan<HeartComponent>(patient, out var original), Is.True);
            Server.System<MobStateSystem>().ChangeMobState(patient, MobState.Dead);

            Assert.That(index.TryGetOrganPart(original, out var part), Is.True);
            var slot = index.GetOrganSlots(part).Single(entry => entry.Organ == original).SlotId;
            Assert.That(bodies.RemoveOrgan(original), Is.True);
            var fresh = SEntMan.SpawnEntity("CMUOrganHumanHeart", coordinates);
            Assert.That(bodies.InsertOrgan(part, fresh, slot), Is.True);

            Assert.That(SEntMan.GetComponent<HeartComponent>(fresh).Stopped, Is.True);
            Assert.That(hearts.TryPrepareDefibrillation(patient, false, out _, out var reason), Is.True, reason);
        });
    }
}
