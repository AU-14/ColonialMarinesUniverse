using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Charge.CursorCharge;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.ManageHive.Boons;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberEighteenXenoRegressionTest : GameTest
{
    private static readonly ProtoId<NpcFactionPrototype> Govfor = "GOVFOR";

    // the adaptability boon gathered every caste of the same tier, pathogen ones included,
    // so a normal hive could swap into neomorphs and blights
    [Test]
    public async Task AdaptabilityBoonDoesNotOfferPathogenCastes()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hiveSystem = SEntMan.System<SharedXenoHiveSystem>();
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var boon = SEntMan.SpawnEntity(null, map.GridCoords);
            var warrior = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords);
            hiveSystem.SetHive(boon, hive);
            hiveSystem.SetHive(warrior, hive);

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new HiveBoonActivateAdaptabilityEvent { Boon = boon });

            var offered = SEntMan.GetComponent<XenoEvolutionComponent>(warrior).EvolvesToWithoutPoints;
            Assert.Multiple(() =>
            {
                // sanity, the boon still works for normal castes
                Assert.That(offered, Does.Contain(new EntProtoId("CMXenoLurker")));
                Assert.That(offered, Does.Not.Contain(new EntProtoId("CMU14XenoNeomorph")));
                Assert.That(offered, Does.Not.Contain(new EntProtoId("CMU14XenoBlight")));
            });

            SEntMan.DeleteEntity(warrior);
            SEntMan.DeleteEntity(boon);
            SEntMan.DeleteEntity(hive);
        });
    }
}
