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
using Content.Shared.Chasm;
using Content.Shared.StepTrigger.Systems;
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

    // the lowered vehicle lift should swallow thrown things like the ASRS lift, but never a vehicle
    [Test]
    public async Task LoweredVehicleLiftTakesItemsButNotVehicles()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var lift = SEntMan.SpawnEntity("VehicleLift", map.GridCoords);
            var item = SEntMan.SpawnEntity("CMCrowbar", map.GridCoords);
            var tank = SEntMan.SpawnEntity("VehicleTank", map.GridCoords.Offset(new Vector2(20, 20)));

            var itemStep = new StepTriggeredOffEvent(lift, item);
            SEntMan.EventBus.RaiseLocalEvent(lift, ref itemStep);
            var tankStep = new StepTriggeredOffEvent(lift, tank);
            SEntMan.EventBus.RaiseLocalEvent(lift, ref tankStep);

            Assert.That(SEntMan.HasComponent<ChasmFallingComponent>(item), Is.True, "items fall in");
            Assert.That(SEntMan.HasComponent<ChasmFallingComponent>(tank), Is.False, "vehicles stay up");
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

}
