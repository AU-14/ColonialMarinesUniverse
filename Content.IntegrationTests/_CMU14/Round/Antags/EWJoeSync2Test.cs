using System.Linq;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Surrender;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// The second master sync brought surrender (raised hands): a Joe that surrenders and then uses a hostile
/// ability lowers its hands, and drawing the machete with raised hands works instead of failing.
/// </summary>
[TestFixture]
public sealed class EWJoeSync2Test
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private static readonly EntProtoId SweepAction = "CMUActionEWJoeSweep";

    [Test]
    public async Task HostileAbilitiesAndTheMacheteEndASurrender()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var actions = entities.System<SharedActionsSystem>();
            var arsenal = entities.System<EWJoeArsenalSystem>();
            var hands = entities.System<SharedHandsSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            var victim = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 1));

            foreach (var held in hands.EnumerateHeld(joe).ToArray())
            {
                hands.TryDrop(joe, held);
            }

            // a sweep from raised hands drops them
            entities.EnsureComponent<CMUSurrenderComponent>(joe);
            var sweep = actions.AddAction(joe, SweepAction.Id);
            Assert.That(sweep, Is.Not.Null);
            var action = sweep!.Value;
            actions.PerformAction(joe, (action, entities.GetComponent<ActionComponent>(action)),
                new EWJoeSweepActionEvent { Target = victim });
            Assert.That(entities.HasComponent<CMUSurrenderComponent>(joe), Is.False, "the sweep ended the surrender");

            // raised hands refuse what is put in them; the machete lowers them first
            entities.EnsureComponent<CMUSurrenderComponent>(joe);
            var summon = new EWJoeMacheteActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, summon);
            Assert.That(arsenal.FindMachete(joe), Is.Not.Null, "the machete reached a hand");
            Assert.That(entities.HasComponent<CMUSurrenderComponent>(joe), Is.False, "drawing it ended the surrender");
        });

        await pair.CleanReturnAsync();
    }
}
