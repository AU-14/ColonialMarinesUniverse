using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Reaper;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Xenonids;

// BUG-667: a click that slipped off a moving hivemate landed on its weeds, which count as same-hive,
// so the mantle went onto the floor and the cooldown burned anyway
[TestFixture]
[TestOf(typeof(XenoReaperSystem))]
public sealed class CMUCarrionMantleTargetTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: CMXenoReaper
          id: CMUTestCarrionMantleReaper
          components:
          - type: XenoReaper
            fleshResin: 500
        """;

    [Test]
    public async Task MantleOnlyLandsOnXenos()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var hives = entities.System<SharedXenoHiveSystem>();
            var actions = entities.System<SharedActionsSystem>();

            var hive = entities.SpawnEntity("CMXenoHive", map.GridCoords);
            var reaper = entities.SpawnEntity("CMUTestCarrionMantleReaper", map.GridCoords);
            var hivemate = entities.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(1, 0)));
            var weeds = entities.SpawnEntity("XenoWeeds", map.GridCoords.Offset(new Vector2(1, 0)));
            hives.SetHive(reaper, hive);
            hives.SetHive(hivemate, hive);
            hives.SetSameHive(reaper, weeds);

            var action = actions.GetActions(reaper)
                .Single(a => entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == "ActionXenoCarrionMantle");
            var reaperComp = entities.GetComponent<XenoReaperComponent>(reaper);
            var startingFlesh = reaperComp.FleshResin;

            var missed = new XenoCarrionMantleActionEvent
            {
                Performer = reaper,
                Target = weeds,
                Action = (action.Owner, action.Comp),
            };
            entities.EventBus.RaiseLocalEvent(reaper, missed);
            Assert.That(missed.Handled, Is.False, "mantling weeds must not count as a use");
            Assert.That(reaperComp.FleshResin, Is.EqualTo(startingFlesh), "a miss must not spend flesh resin");
            Assert.That(entities.HasComponent<XenoCarrionMantleComponent>(weeds), Is.False);

            var hit = new XenoCarrionMantleActionEvent
            {
                Performer = reaper,
                Target = hivemate,
                Action = (action.Owner, action.Comp),
            };
            entities.EventBus.RaiseLocalEvent(reaper, hit);
            Assert.That(hit.Handled, Is.True);
            Assert.That(entities.HasComponent<XenoCarrionMantleComponent>(hivemate), Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
