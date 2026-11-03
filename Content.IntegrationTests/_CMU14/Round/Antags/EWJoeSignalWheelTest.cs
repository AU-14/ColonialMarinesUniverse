using System.Linq;
using Content.Client._RMC14.Actions;
using Content.Client.CMU14.Round.Antags.EarlyWarning;
using Content.Client.UserInterface.Systems.Actions;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Action-bar consolidation: every Early Warning Joe ability is either in a Signal wheel or one of the few
/// direct actions, each wheel arrives no later than its first ability, and on the Joe's own client the hotbar
/// holds the wheels and direct actions while the grouped abilities wait in their wheel.
/// </summary>
[TestFixture]
public sealed class EWJoeSignalWheelTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";

    /// <summary>Abilities that stay on the bar by design (action-bar report §4).</summary>
    private static readonly HashSet<string> Direct = new() { "CMUActionEWJoeMachete", "CMUActionEWJoeStatus" };

    private static readonly Dictionary<EWJoeAbilityGroup, string> Wheels = new()
    {
        [EWJoeAbilityGroup.Gaze] = "CMUActionEWJoeWheelGaze",
        [EWJoeAbilityGroup.Hands] = "CMUActionEWJoeWheelHands",
        [EWJoeAbilityGroup.Machine] = "CMUActionEWJoeWheelMachine",
        [EWJoeAbilityGroup.Engine] = "CMUActionEWJoeWheelEngine",
    };

    [Test]
    public async Task EveryAbilityHasAWheelThatArrivesWithIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protos = server.ResolveDependency<IPrototypeManager>();
            var factory = server.ResolveDependency<IComponentFactory>();
            var table = protos.Index<EWJoeLevelsPrototype>("CMUEWJoeLevels");

            var wheelLevel = new Dictionary<string, int>();
            for (var level = 0; level < table.Levels.Count; level++)
            {
                foreach (var action in table.Levels[level].Actions)
                {
                    if (Wheels.ContainsValue(action.Id))
                        wheelLevel.TryAdd(action.Id, level);
                }
            }

            for (var level = 0; level < table.Levels.Count; level++)
            {
                foreach (var action in table.Levels[level].Actions)
                {
                    if (Direct.Contains(action.Id) || Wheels.ContainsValue(action.Id))
                        continue;

                    var proto = protos.Index(action);
                    Assert.That(proto.TryGetComponent(out EWJoeAbilityGroupComponent? group, factory),
                        $"{action.Id} is neither in a Signal wheel nor a direct action: it would land on the hotbar");

                    var wheel = Wheels[group!.Group];
                    Assert.That(wheelLevel.TryGetValue(wheel, out var arrives) && arrives <= level,
                        $"{action.Id} (level {level}) is in the {group.Group} wheel, which is not granted by then");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TheJoesHotbarHoldsTheWheelsAndTheWheelsHoldTheRest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        // attach at level 0, then level up: later abilities arrive as runtime grants (OnActionAdded), not
        // through the default load on attach
        NetEntity netJoe = default;
        EntityUid serverJoe = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            serverJoe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            netJoe = entities.GetNetEntity(serverJoe);
            server.PlayerMan.SetAttachedEntity(pair.Player!, serverJoe);
            var comp = entities.EnsureComponent<EWJoeComponent>(serverJoe);
            entities.System<EWJoeRuleSystem>().ApplyLevel((serverJoe, comp), 0);
        });
        await Sync(pair);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            entities.System<EWJoeRuleSystem>().ApplyLevel((serverJoe, entities.GetComponent<EWJoeComponent>(serverJoe)), 2);
        });
        await Sync(pair);

        // the player drags Drown onto the bar: SetAction saves the bar through RMC's action order. The headless
        // test client never enters the gameplay screen, where the controller hooks runtime grants, so the bar
        // being saved is built as play would have it: every action outside the wheels, plus the pin.
        EntityUid drown = default;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var joe = entities.GetEntity(netJoe);
            var hotbar = client.ResolveDependency<IUserInterfaceManager>().GetUIController<ActionUIController>();
            drown = entities.System<EWJoeSignalWheelSystem>().GetWheelActions(joe, EWJoeAbilityGroup.Hands)
                .First(a => entities.GetComponent<MetaDataComponent>(a.Owner).EntityPrototype?.ID == "CMUActionEWJoeDrown").Owner;
            // the re-sort's rule, directly: with Drown pinned in the saved order, every other wheel ability is
            // hidden and nothing outside the wheels is (TEST-01: the headless bar itself starts empty)
            var held = entities.System<Content.Client.Actions.ActionsSystem>().GetActions(joe).ToList();
            var hiddenIds = entities.System<RMCActionsSystem>().CMUHideUnpinnedWheelActions(held,
                System.Collections.Immutable.ImmutableArray.Create<EntProtoId>("CMUActionEWJoeDrown"),
                System.Collections.Immutable.ImmutableArray<EntProtoId>.Empty);
            Assert.Multiple(() =>
            {
                Assert.That(hiddenIds.Select(id => id.Id), Does.Not.Contain("CMUActionEWJoeDrown"), "a pinned ability stays on the bar");
                Assert.That(hiddenIds.Select(id => id.Id), Does.Contain("CMUActionEWJoeSweep"), "an unpinned wheel ability is hidden");
                Assert.That(hiddenIds.Select(id => id.Id).Where(id => id.StartsWith("CMUActionEWJoeWheel")), Is.Empty, "the wheels themselves are never hidden");
            });

            var bar = new List<EntityUid?>();
            foreach (var action in entities.EntityQuery<ActionComponent>(true))
            {
                if (action.AttachedEntity == joe && !entities.HasComponent<EWJoeAbilityGroupComponent>(action.Owner))
                    bar.Add(action.Owner);
            }

            bar.Add(drown);
            entities.System<RMCActionsSystem>().ActionsChanged(bar);
        });

        // levels 3-5 arrive after the order was saved: RMC re-sorts the bar from that order, and must not
        // append the new wheel abilities just because the order has never heard of them
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            entities.System<EWJoeRuleSystem>().ApplyLevel((serverJoe, entities.GetComponent<EWJoeComponent>(serverJoe)), 5);
        });
        await Sync(pair);

        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var joe = entities.GetEntity(netJoe);
            var wheels = entities.System<EWJoeSignalWheelSystem>();
            var hotbar = client.ResolveDependency<IUserInterfaceManager>().GetUIController<ActionUIController>();

            Assert.Multiple(() =>
            {
                Assert.That(wheels.GetWheelActions(joe, EWJoeAbilityGroup.Gaze), Has.Count.EqualTo(3), "Judgement, Optical Zoom, Sense Penitent");
                Assert.That(wheels.GetWheelActions(joe, EWJoeAbilityGroup.Hands), Has.Count.EqualTo(5), "Drown, Sweep, Slam, Choke, Throw");
                Assert.That(wheels.GetWheelActions(joe, EWJoeAbilityGroup.Machine), Has.Count.EqualTo(5), "Disrupt, Offer, Assimilate, Scour, Unwind");
                Assert.That(wheels.GetWheelActions(joe, EWJoeAbilityGroup.Engine), Has.Count.EqualTo(5), "Overclock, Coolant, Consecrate, Z-Leap, Saturate");
            });

            var visible = new List<string>();
            var hidden = new List<string>();
            foreach (var action in entities.EntityQuery<ActionComponent>(true))
            {
                if (action.AttachedEntity != joe)
                    continue;

                var id = entities.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID;
                if (id == null || !id.StartsWith("CMUActionEWJoe"))
                    continue;

                (hotbar.CMUIsOnHotbar(action.Owner) ? visible : hidden).Add(id);
            }

            Assert.Multiple(() =>
            {
                Assert.That(visible, Is.EquivalentTo(Wheels.Values.Concat(Direct).Append("CMUActionEWJoeDrown")),
                    "the bar holds the four wheels, the machete, Signal Status and the pinned Drown");
                Assert.That(hidden, Has.Count.EqualTo(17), "the other seventeen grouped abilities wait in their wheels");
                Assert.That(hidden, Does.Contain("CMUActionEWJoeSaturate"), "an ability earned after the pin stays in its wheel");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static async Task Sync(Pair.TestPair pair)
    {
        await pair.RunUntilSynced();
        await pair.RunTicksSync(10);
        await pair.RunUntilSynced();
    }
}
