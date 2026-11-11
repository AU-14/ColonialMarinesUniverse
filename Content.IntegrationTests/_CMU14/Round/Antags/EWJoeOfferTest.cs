using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Offer Firearms: without a directive Offer refuses; with one, a loose firearm taken in through the
/// action's do-after leaves play and ticks the directive, a held one is refused, and the directive
/// completes when enough have been offered.
/// </summary>
[TestFixture]
public sealed class EWJoeOfferTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Revolver = "RMCWeaponRevolverM44";
    private const string EWMindRole = "MindRoleEWJoe";
    private const string OfferObjective = "CMUEWJoeOfferFirearmsObjective";
    private const string Sentry = "RMCSentry";
    private const string Drone = "CMUCombatDrone";
    private const string Attachment = "RMCAttachmentUnderbarrelFlareGun";

    [Test]
    public async Task OfferedFirearmsLeavePlayAndTickTheDirective()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, objective = default, first = default, second = default;
        await server.WaitAssertion(() =>
        {
            var minds = entities.System<MindSystem>();
            var objectives = entities.System<ObjectivesSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var mind = minds.CreateMind(null, "Early Warning Joe");
            minds.TransferTo(mind, joe);
            entities.System<SharedRoleSystem>().MindAddRole(mind, EWMindRole, silent: true);
            entities.EnsureComponent<EWJoeComponent>(joe);

            // exactly two firearms exist, so the directive (min 2, max 3) asks for two
            first = entities.SpawnEntity(Revolver, EWJoeTestHelpers.TileEast(entities, map, 1));
            second = entities.SpawnEntity(Revolver, EWJoeTestHelpers.TileEast(entities, map, 1));

            var refused = new EWJoeOfferActionEvent { Performer = joe, Target = first };
            entities.EventBus.RaiseLocalEvent(joe, refused);
            Assert.That(refused.Handled, Is.False, "no directive asks for it");

            Assert.That(objectives.TryCreateObjective((mind, mind.Comp), OfferObjective, out var created), "offer directive");
            objective = created!.Value;
            minds.AddObjective(mind, mind.Comp, objective);
            Assert.That(entities.GetComponent<EWJoeOfferConditionComponent>(objective).Target, Is.EqualTo(2));

            var offer = new EWJoeOfferActionEvent { Performer = joe, Target = first };
            entities.EventBus.RaiseLocalEvent(joe, offer);
            Assert.That(offer.Handled, "the offering starts");
        });

        // the three-second take-in
        await pair.RunTicksSync((int) Math.Ceiling(3.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var objectives = entities.System<ObjectivesSystem>();
            var minds = entities.System<MindSystem>();
            Assert.That(minds.TryGetMind(joe, out var mindId, out var mind));
            Assert.That(entities.Deleted(first), "the offered firearm left play");
            Assert.That(entities.GetComponent<EWJoeOfferConditionComponent>(objective).Offered, Is.EqualTo(1));
            Assert.That(objectives.GetProgress(objective, (mindId, mind)), Is.EqualTo(0.5f).Within(0.01f));

            // a firearm in someone's hand is not loose
            var holder = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 1));
            var hands = entities.System<SharedHandsSystem>();
            var held = entities.SpawnEntity(Revolver, EWJoeTestHelpers.TileEast(entities, map, 1));
            Assert.That(hands.TryPickupAnyHand(holder, held));
            var refused = new EWJoeOfferActionEvent { Performer = joe, Target = held };
            entities.EventBus.RaiseLocalEvent(joe, refused);
            Assert.That(refused.Handled, Is.False, "a held firearm is refused");

            // the second offering completes the directive; nothing counts past it
            var offering = entities.System<EWJoeOfferSystem>();

            // machinery, bodies and parts are never offerings, even with a directive open
            var coords = EWJoeTestHelpers.TileEast(entities, map, 2);
            Assert.Multiple(() =>
            {
                Assert.That(offering.IsOfferable(second), "a loose revolver is an offering");
                Assert.That(offering.IsOfferable(entities.SpawnEntity(Sentry, coords)), Is.False, "a sentry is not");
                Assert.That(offering.IsOfferable(entities.SpawnEntity(Drone, coords)), Is.False, "a combat drone is not");
                Assert.That(offering.IsOfferable(entities.SpawnEntity(Attachment, coords)), Is.False, "an underbarrel attachment is not");
            });

            offering.Offer((joe, entities.GetComponent<EWJoeComponent>(joe)), second);
            Assert.That(objectives.IsCompleted(objective, (mindId, mind)), "two offered");
            Assert.That(offering.OpenOffers(joe, held), Is.Empty, "a finished directive takes nothing more");
        });

        await pair.CleanReturnAsync();
    }
}
