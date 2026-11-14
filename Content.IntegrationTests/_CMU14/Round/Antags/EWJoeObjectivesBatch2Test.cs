// CMU14
using System.Linq;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.SurveillanceCamera;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Power;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.DamageTracking;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mind;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.Whitelist;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// N9 (ARCH-02, COUPLING-02, ARCH-03 batch 2): a collect directive whose items are gone is withdrawn and its
/// slot offered again after the retry delay; the player's reroll is limited to once per level or the
/// cooldown; Offer is whitelist-driven (firearms stay guns-only, ordnance takes a grenade) and its action can
/// target ordnance and structures (FLOW-14); Purge the Nest consumes xeno, pathogen and biomorph structures
/// the Joe Offers, by weight, and is refused with none; Blind the Watch and Silence the Colony meld cameras and
/// telecomms servers into the river, once each (ARCH-11, FLOW-13); Darken a District
/// never pins a dark area, is refused on a dark planet, completes on a blackout the Joe caused, and resets
/// when power returns.
/// </summary>
[TestFixture]
public sealed class EWJoeObjectivesBatch2Test
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string EWMindRole = "MindRoleEWJoe";
    private const string Revolver = "RMCWeaponRevolverM44";
    private const string Grenade = "CMGrenadeHighExplosive";
    private const string KillCountObjective = "CMUEWJoeKillCountObjective";
    private const string FirearmsObjective = "CMUEWJoeOfferFirearmsObjective";
    private const string OrdnanceObjective = "CMUEWJoeOfferOrdnanceObjective";
    private const string PurgeObjective = "CMUEWJoePurgeNestObjective";
    private const string BlindObjective = "CMUEWJoeBlindTheWatchObjective";
    private const string SilenceObjective = "CMUEWJoeSilenceColonyObjective";
    private const string OfferAction = "CMUActionEWJoeOffer";
    private const string Camera = "RMCSurveillanceCameraAlmayer";
    private const string TelecomServer = "CMTelecomServer";
    private const string ResinWall = "WallXenoResin";
    private const string FleshNest = "AU14BiomorphFleshNest";
    private const string HiveCore = "HiveCoreXeno";
    private const string Welder = "CMWelder";
    private const string Human = "CMMobHuman";
    private const string BearWitnessObjective = "CMUEWJoeBearWitnessObjective";
    private const string Sentry = "RMCSentry";
    private const string CollectGunsObjective = "CMUEWJoeTestBatch2CollectGunsObjective";
    private const string DarkenObjective = "CMUEWJoeTestBatch2DarkenObjective";
    private const string AreaLit = "RMCAreaLv624GroundJungleSouthEastJungle";
    private const string AreaDark = "RMCAreaLv624GroundJungleSouthCentralJungle";
    private const string AreaAlwaysPowered = "RMCAreaLv624LandingConsole";
    private const string Apc = "CMApc";

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: CMUEWJoeBaseCollectObjective
  id: CMUEWJoeTestBatch2CollectGunsObjective
  components:
  - type: EWJoeCollectCondition
    min: 2
    max: 2
    title: cmu-ew-joe-objective-collect-intel-title
    description: cmu-ew-joe-objective-collect-intel-description
    whitelist:
      components:
      - Gun

- type: entity
  parent: CMUEWJoeDarkenDistrictObjective
  id: CMUEWJoeTestBatch2DarkenObjective
  components:
  - type: EWJoeDarkenCondition
    darkDuration: 2
    title: cmu-ew-joe-objective-darken-district-title
    description: cmu-ew-joe-objective-darken-district-description
""";

    private static (EntityUid Joe, Entity<MindComponent> Mind, EWJoeComponent Comp) SpawnJoe(IEntityManager entities, EntityCoordinates coords)
    {
        var minds = entities.System<MindSystem>();
        var joe = entities.SpawnEntity(ColonyJoe, coords);
        var mind = minds.CreateMind(null, "Early Warning Joe");
        minds.TransferTo(mind, joe);
        entities.System<SharedRoleSystem>().MindAddRole(mind, EWMindRole, silent: true);
        var comp = entities.EnsureComponent<EWJoeComponent>(joe);
        return (joe, mind, comp);
    }

    private static EntityUid Assign(IEntityManager entities, Entity<MindComponent> mind, EWJoeComponent comp, string proto)
    {
        Assert.That(entities.System<ObjectivesSystem>().TryCreateObjective(mind, proto, out var objective), $"{proto} is assigned");
        entities.System<MindSystem>().AddObjective(mind, mind.Comp, objective!.Value);
        comp.CurrentObjectives.Add(objective.Value);
        return objective.Value;
    }

    [Test]
    public async Task StaleCollectDirectiveIsWithdrawnAndReoffered()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var ladder = entities.System<EWJoeObjectiveSystem>();
            var (joe, mind, comp) = SpawnJoe(entities, map.GridCoords);

            var first = entities.SpawnEntity(Revolver, EWJoeTestHelpers.TileEast(entities, map, 2));
            entities.SpawnEntity(Revolver, EWJoeTestHelpers.TileEast(entities, map, 2));

            var kill = Assign(entities, mind, comp, KillCountObjective);
            var collect = Assign(entities, mind, comp, CollectGunsObjective);

            ladder.Evaluate((joe, comp));
            Assert.That(comp.CurrentObjectives, Does.Contain(collect), "two guns exist: still possible");

            // one of the two is gone: two can never be obtained now
            entities.DeleteEntity(first);
            ladder.Evaluate((joe, comp));

            var state = entities.GetComponent<EWJoeDirectiveStateComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(comp.CurrentObjectives, Does.Not.Contain(collect), "the stale directive left the offer");
                Assert.That(mind.Comp.Objectives, Does.Not.Contain(collect), "and the mind");
                Assert.That(entities.Deleted(collect), "withdrawn directives are deleted");
                Assert.That(comp.CurrentObjectives, Does.Contain(kill), "the kill count is always possible");
                Assert.That(state.PendingRefill, "the slot is owed");
                Assert.That(comp.NextOfferAttempt, Is.EqualTo(timing.CurTime + comp.OfferRetryDelay), "after the retry delay");
            });

            // before the delay nothing is offered
            ladder.Evaluate((joe, comp));
            Assert.That(comp.CurrentObjectives, Has.Count.EqualTo(1));

            // fresh guns on the planet; the retry delay passes and the slot is filled from the level-0 pool
            entities.SpawnEntity(Revolver, EWJoeTestHelpers.TileEast(entities, map, 2));
            entities.SpawnEntity(Revolver, EWJoeTestHelpers.TileEast(entities, map, 2));
            comp.NextOfferAttempt = TimeSpan.Zero;
            ladder.Evaluate((joe, comp));
            Assert.That(comp.CurrentObjectives.Any(o => o != kill && entities.HasComponent<EWJoeOfferConditionComponent>(o)),
                "a new directive was offered in the withdrawn one's place");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RerollIsOncePerLevelOrAfterTheCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var ladder = entities.System<EWJoeObjectiveSystem>();
            var (joe, mind, comp) = SpawnJoe(entities, map.GridCoords);
            var ent = (joe, comp);

            for (var i = 0; i < 4; i++)
                entities.SpawnEntity(Revolver, EWJoeTestHelpers.TileEast(entities, map, 2));

            var kill = Assign(entities, mind, comp, KillCountObjective);
            Assert.That(ladder.RerollReady(ent), "a fresh level has its reroll");

            Assert.That(ladder.TryReroll(ent, 0), "the kill count is swapped");
            var replacement = comp.CurrentObjectives.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entities.Deleted(kill), "the old directive is withdrawn");
                Assert.That(replacement, Is.Not.EqualTo(kill));
                Assert.That(mind.Comp.Objectives, Does.Contain(replacement));
                Assert.That(ladder.RerollReady(ent), Is.False, "spent for this level");
            });

            Assert.That(ladder.TryReroll(ent, 0), Is.False, "a second request on the same level is refused");
            Assert.That(comp.CurrentObjectives.Single(), Is.EqualTo(replacement));

            // the level lasting the whole cooldown earns another
            var state = entities.GetComponent<EWJoeDirectiveStateComponent>(joe);
            state.LastReroll = timing.CurTime - state.RerollCooldown - TimeSpan.FromSeconds(1);
            Assert.That(ladder.RerollReady(ent), "the cooldown ran out");

            // a finished directive is not swapped, ready or not
            comp.CompletedCredit[replacement] = 1;
            Assert.That(ladder.TryReroll(ent, 0), Is.False, "finished directives stay");
            comp.CompletedCredit.Clear();

            // a new level has its own reroll even inside the cooldown
            state.LastReroll = timing.CurTime;
            Assert.That(ladder.RerollReady(ent), Is.False);
            comp.Level = 1;
            Assert.That(ladder.RerollReady(ent), "a new level, a new reroll");
            comp.Level = 0;
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OfferIsWhitelistDriven()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var offering = entities.System<EWJoeOfferSystem>();
            var objectives = entities.System<ObjectivesSystem>();
            var (joe, mind, comp) = SpawnJoe(entities, map.GridCoords);

            var coords = EWJoeTestHelpers.TileEast(entities, map, 2);
            var revolver = entities.SpawnEntity(Revolver, coords);
            entities.SpawnEntity(Revolver, coords);
            var grenade = entities.SpawnEntity(Grenade, coords);

            var firearms = Assign(entities, mind, comp, FirearmsObjective);
            var ordnance = Assign(entities, mind, comp, OrdnanceObjective);
            var firearmsCond = entities.GetComponent<EWJoeOfferConditionComponent>(firearms);
            var ordnanceCond = entities.GetComponent<EWJoeOfferConditionComponent>(ordnance);

            Assert.Multiple(() =>
            {
                Assert.That(offering.IsOfferable(revolver, firearmsCond), "a loose revolver is a firearm");
                Assert.That(offering.IsOfferable(grenade, firearmsCond), Is.False, "a grenade is not a firearm (O3 stays guns-only)");
                Assert.That(offering.IsOfferable(grenade, ordnanceCond), "a grenade is ordnance");
                Assert.That(offering.IsOfferable(revolver, ordnanceCond), Is.False, "a revolver is not ordnance");
                Assert.That(ordnanceCond.Target, Is.EqualTo(1), "one explosive exists, so one is asked for");
            });

            offering.Offer((joe, comp), grenade);
            Assert.Multiple(() =>
            {
                Assert.That(ordnanceCond.Offered, Is.EqualTo(1), "the grenade ticked the ordnance directive");
                Assert.That(firearmsCond.Offered, Is.Zero, "and not the firearms one");
                Assert.That(objectives.IsCompleted(ordnance, mind), "ordnance offered");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OfferActionTargetsOrdnanceAndStructures()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(protos.Index<EntityPrototype>(OfferAction).TryGetComponent<EntityTargetActionComponent>(out var target, entities.ComponentFactory),
                "Offer targets entities");
            var whitelist = entities.System<EntityWhitelistSystem>();

            // FLOW-14: the client-side whitelist is the union of what Offer directives want; the server still filters
            var x = 2;
            Assert.Multiple(() =>
            {
                foreach (var proto in new[] { Grenade, Revolver, Camera, TelecomServer, ResinWall })
                {
                    var uid = entities.SpawnEntity(proto, EWJoeTestHelpers.TileEast(entities, map, x++));
                    Assert.That(whitelist.CheckBoth(uid, target!.Blacklist, target.Whitelist), $"Offer can target {proto}");
                }

                var sentry = entities.SpawnEntity(Sentry, EWJoeTestHelpers.TileEast(entities, map, x));
                Assert.That(whitelist.CheckBoth(sentry, target!.Blacklist, target.Whitelist), Is.False, "never a sentry");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PurgeTheNestConsumesThreatStructuresByWeight()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var objectives = entities.System<ObjectivesSystem>();
            var offering = entities.System<EWJoeOfferSystem>();
            var (joe, mind, comp) = SpawnJoe(entities, map.GridCoords);

            Assert.That(objectives.TryCreateObjective(mind, PurgeObjective, out _), Is.False,
                "no threat structures on the planet (an ape round): refused");

            // lay the floor first as one contiguous strip: a lone tile would be split onto its own grid by the
            // engine's grid splitting when the next tile goes down, un-anchoring what stands on it
            for (var x = 2; x <= 7; x++)
                EWJoeTestHelpers.TileEast(entities, map, x);

            // RB-31: a hive core (weight 3, the old minimum on its own) is excluded altogether
            var core = entities.SpawnEntity(HiveCore, EWJoeTestHelpers.TileEast(entities, map, 7));
            Assert.That(objectives.TryCreateObjective(mind, PurgeObjective, out _), Is.False,
                "a lone hive core does not count toward the target: still refused");

            var resin = entities.SpawnEntity(ResinWall, EWJoeTestHelpers.TileEast(entities, map, 2));
            var mycelium = entities.SpawnEntity("CMU14XenoMyceliumWall", EWJoeTestHelpers.TileEast(entities, map, 3));
            var flesh = entities.SpawnEntity("AU14BiomorphFleshWall", EWJoeTestHelpers.TileEast(entities, map, 4));

            var purge = Assign(entities, mind, comp, PurgeObjective);
            var offer = entities.GetComponent<EWJoeOfferConditionComponent>(purge);
            // with the core counted, six would be available and the roll would run 3-5
            Assert.That(offer.Target, Is.EqualTo(3), "three weighted structures exist (the core not among them), so three are asked for");

            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<TransformComponent>(resin).Anchored, "the resin wall is anchored");
                Assert.That(offering.IsOfferable(resin, offer), "and still wanted: the default filter is off");
                Assert.That(offering.IsOfferable(mycelium, offer), "pathogen mycelium is wanted too");
                Assert.That(offering.IsOfferable(flesh, offer), "and biomorph flesh");
                Assert.That(offering.IsOfferable(entities.SpawnEntity(Revolver, EWJoeTestHelpers.TileEast(entities, map, 5)), offer),
                    Is.False, "a firearm is no threat structure");
                Assert.That(offering.IsOfferable(core, offer), Is.False, "a hive core can never be offered");
                Assert.That(offering.OpenOffers(joe, core), Is.Empty, "no directive wants it");
                Assert.That(offering.OfferDelayFor(comp, resin), Is.EqualTo(EWJoeOfferSystem.StructureOfferDelay),
                    "an anchored structure takes the long offering");
            });

            // offering it directly does nothing
            offering.Offer((joe, comp), core);
            Assert.Multiple(() =>
            {
                Assert.That(entities.Deleted(core) || entities.IsQueuedForDeletion(core), Is.False, "the core stands");
                Assert.That(offer.Offered, Is.Zero, "and nothing was counted");
            });

            // consumed like an item, not melded
            offering.Offer((joe, comp), resin);
            Assert.Multiple(() =>
            {
                Assert.That(entities.Deleted(resin) || entities.IsQueuedForDeletion(resin), "the resin wall is consumed");
                Assert.That(entities.HasComponent<EWJoeMeldedStructureComponent>(resin), Is.False, "not melded");
                Assert.That(offer.Offered, Is.EqualTo(1), "xeno resin counts one");
            });

            // a flesh nest (not a core) is still worth three
            var nest = entities.SpawnEntity(FleshNest, EWJoeTestHelpers.TileEast(entities, map, 6));
            Assert.That(offering.Weight(nest, offer), Is.EqualTo(3));
            offering.Offer((joe, comp), nest);
            Assert.Multiple(() =>
            {
                Assert.That(offer.Offered, Is.EqualTo(4), "the flesh nest counted three");
                Assert.That(entities.Deleted(nest) || entities.IsQueuedForDeletion(nest), "and was consumed");
                Assert.That(objectives.IsCompleted(purge, mind), "the nest is purged");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlindTheWatchMeldsEachCameraOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, objective = default, camera = default, spare = default;
        await server.WaitAssertion(() =>
        {
            var objectives = entities.System<ObjectivesSystem>();
            (joe, var mind, var comp) = SpawnJoe(entities, map.GridCoords);

            Assert.That(objectives.TryCreateObjective(mind, BlindObjective, out _), Is.False, "no cameras on the planet: refused");

            // one tile from the Joe on the same grid, inside the offering's 1.5-tile reach (TileEast(1) is ~1.58 away)
            camera = entities.SpawnEntity(Camera, map.GridCoords.Offset(new System.Numerics.Vector2(1f, 0f)));
            spare = entities.SpawnEntity(Camera, map.GridCoords.Offset(new System.Numerics.Vector2(1f, 0f)));
            objective = Assign(entities, mind, comp, BlindObjective);

            // RB-35: Blind the Watch asks for 2-4 cameras
            Assert.That(entities.GetComponent<EWJoeOfferConditionComponent>(objective).Min, Is.EqualTo(2));
            Assert.That(entities.GetComponent<EWJoeOfferConditionComponent>(objective).Max, Is.EqualTo(4));
            // both cameras are asked for, so the directive is still open after the first
            entities.GetComponent<EWJoeOfferConditionComponent>(objective).Target = 2;

            var offer = new EWJoeOfferActionEvent { Performer = joe, Target = camera };
            entities.EventBus.RaiseLocalEvent(joe, offer);
            Assert.That(offer.Handled, "a camera is no item, but a Meld directive wants it: the offering starts");
        });

        // RB-31: a structure's offering takes six seconds, not an item's three
        await pair.RunTicksSync((int) Math.Ceiling(3.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<EWJoeMeldedStructureComponent>(camera), Is.False, "not melded after three and a half seconds");
            Assert.That(entities.GetComponent<EWJoeOfferConditionComponent>(objective).Offered, Is.Zero, "nor counted");
        });

        await pair.RunTicksSync((int) Math.Ceiling(3 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var offering = entities.System<EWJoeOfferSystem>();
            var condition = entities.GetComponent<EWJoeOfferConditionComponent>(objective);
            Assert.That(entities.Deleted(camera) || entities.IsQueuedForDeletion(camera), Is.False, "a melded camera stays where it hangs");
            Assert.That(entities.System<SharedPointLightSystem>().TryGetLight(camera, out var light), "it carries a light");
            Assert.Multiple(() =>
            {
                Assert.That(entities.TryGetComponent(camera, out EWJoeMeldedStructureComponent? melded) && melded.MeldedBy == joe,
                    "marked as melded by the Joe");
                Assert.That(light!.Enabled, "lit");
                Assert.That(light.Color, Is.EqualTo(EWJoeMeldedStructureSystem.MeldColor), "in the river's orange");
                Assert.That(light.Energy, Is.EqualTo(EWJoeMeldedStructureSystem.MeldEnergy).Within(0.01f), "bright");
                Assert.That(light.Radius, Is.EqualTo(EWJoeMeldedStructureSystem.MeldRadius).Within(0.01f), "and wide");
                Assert.That(entities.GetComponent<SurveillanceCameraComponent>(camera).Active, Is.False, "and watching no more");
                Assert.That(condition.Offered, Is.EqualTo(1), "one camera counted");
            });

            // power returning or an EMP wearing off cannot wake it
            entities.System<SurveillanceCameraSystem>().SetActive(camera, true);
            Assert.That(entities.GetComponent<SurveillanceCameraComponent>(camera).Active, Is.False, "a melded camera stays off");

            // given once: neither the action nor a direct offering counts it again
            var again = new EWJoeOfferActionEvent { Performer = joe, Target = camera };
            entities.EventBus.RaiseLocalEvent(joe, again);
            offering.Offer((joe, entities.GetComponent<EWJoeComponent>(joe)), camera);
            Assert.Multiple(() =>
            {
                Assert.That(again.Handled, Is.False, "a melded camera cannot be offered again");
                Assert.That(condition.Offered, Is.EqualTo(1), "nor counted twice");
                Assert.That(offering.IsOfferable(spare, condition), "the other camera is still wanted");
            });

            // a camera off the planet the directive was given on (a ship) is never wanted
            var shipMapUid = entities.System<SharedMapSystem>().CreateMap(out var shipMap);
            var shipCamera = entities.SpawnEntity(Camera, new MapCoordinates(System.Numerics.Vector2.Zero, shipMap));
            Assert.That(offering.OpenOffers(joe, shipCamera), Is.Empty, "a ship's camera does not finish a planet directive");
            Assert.That(offering.OpenOffers(joe, spare), Is.Not.Empty, "while one on the planet does");
            entities.DeleteEntity(shipMapUid);

            // the Joe cannot burn out its own meld (no repair-and-re-offer farm)
            var joeWelder = entities.SpawnEntity(Welder, map.GridCoords);
            var joeInteraction = new InteractUsingEvent(joe, joeWelder, camera, entities.GetComponent<TransformComponent>(camera).Coordinates);
            entities.EventBus.RaiseLocalEvent(camera, joeInteraction);
            Assert.That(joeInteraction.Handled, Is.False, "the Joe's own welder does not start a repair");
            entities.DeleteEntity(joeWelder);

            // RB-30: someone with a lit welder burns the meld out
            // a tile east, beside the camera and clear of the Joe (two bodies on one spot push apart and break the do-after)
            var repairerCoords = EWJoeTestHelpers.TileEast(entities, map, 1);
            var repairer = entities.SpawnEntity(Human, repairerCoords);
            var welder = entities.SpawnEntity(Welder, repairerCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(repairer, welder, checkActionBlocker: false), "the welder is in hand");
            Assert.That(entities.System<ItemToggleSystem>().TryActivate((welder, null), repairer), "and lit");
            var interaction = new InteractUsingEvent(repairer, welder, camera, entities.GetComponent<TransformComponent>(camera).Coordinates);
            entities.EventBus.RaiseLocalEvent(camera, interaction);
            Assert.That(interaction.Handled, "a lit welder starts the repair");
        });

        // the repair is a ten-second burn: not done halfway
        await pair.RunTicksSync((int) Math.Ceiling(5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<EWJoeMeldedStructureComponent>(camera), "still melded five seconds in");
        });

        await pair.RunTicksSync((int) Math.Ceiling(5.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var offering = entities.System<EWJoeOfferSystem>();
            var condition = entities.GetComponent<EWJoeOfferConditionComponent>(objective);
            var lights = entities.System<SharedPointLightSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<EWJoeMeldedStructureComponent>(camera), Is.False, "the meld is burned out");
                Assert.That(entities.GetComponent<SurveillanceCameraComponent>(camera).Active, "the camera watches again");
                Assert.That(!lights.TryGetLight(camera, out var light) || !light.Enabled || light.Color != EWJoeMeldedStructureSystem.MeldColor,
                    "the river's light is gone");
                Assert.That(offering.IsOfferable(camera, condition), Is.False,
                    "a repaired camera never counts twice for the directive it already counted for");
            });

            // and it watches for good: nothing still refuses it
            entities.System<SurveillanceCameraSystem>().SetActive(camera, false);
            entities.System<SurveillanceCameraSystem>().SetActive(camera, true);
            Assert.That(entities.GetComponent<SurveillanceCameraComponent>(camera).Active, "a repaired camera can be switched back on");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SilenceTheColonyMeldsTelecommsServers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid telecom = default;
        await server.WaitAssertion(() =>
        {
            var objectives = entities.System<ObjectivesSystem>();
            var offering = entities.System<EWJoeOfferSystem>();
            var (joe, mind, comp) = SpawnJoe(entities, map.GridCoords);

            Assert.That(objectives.TryCreateObjective(mind, SilenceObjective, out _), Is.False, "no servers on the planet: refused");

            telecom = entities.SpawnEntity(TelecomServer, EWJoeTestHelpers.TileEast(entities, map, 1));
            var objective = Assign(entities, mind, comp, SilenceObjective);
            var condition = entities.GetComponent<EWJoeOfferConditionComponent>(objective);
            Assert.That(condition.Target, Is.EqualTo(1), "one server exists, so one is asked for");
            Assert.That(offering.IsOfferable(telecom, condition), "an anchored server is wanted by a Meld directive");

            // FLOW-13: the server cannot be destroyed, but it can be melded
            offering.Offer((joe, comp), telecom);
            Assert.Multiple(() =>
            {
                Assert.That(entities.Deleted(telecom) || entities.IsQueuedForDeletion(telecom), Is.False, "the server stays");
                Assert.That(entities.HasComponent<EWJoeMeldedStructureComponent>(telecom), "melded");
                Assert.That(entities.HasComponent<TelecomServerComponent>(telecom), Is.False, "and no longer a server the radio counts");
                Assert.That(condition.Offered, Is.EqualTo(1));
                Assert.That(objectives.IsCompleted(objective, mind), "the colony is silenced");
                Assert.That(offering.IsOfferable(telecom, condition), Is.False, "a melded server is not wanted again");
            });
        });

        // the server's own LitOnPowered must not put the river's light out
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<SharedPointLightSystem>().TryGetLight(telecom, out var light), "it carries a light");
            Assert.Multiple(() =>
            {
                Assert.That(light!.Enabled, "still lit");
                Assert.That(light.Color, Is.EqualTo(EWJoeMeldedStructureSystem.MeldColor), "in the river's orange");
            });

            // RB-30: the repair (the welder's do-after skipped) makes it a server again, in its own light
            var melded = entities.GetComponent<EWJoeMeldedStructureComponent>(telecom);
            Assert.That(melded.WasTelecomServer, "the meld remembered it was a server");
            Assert.That(melded.HadLight, "and that it had a light of its own");
            var ownColor = melded.LightColor;
            entities.System<EWJoeMeldedStructureSystem>().Repair((telecom, melded));
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<EWJoeMeldedStructureComponent>(telecom), Is.False, "the marker is gone");
                Assert.That(entities.HasComponent<TelecomServerComponent>(telecom), "a server the radio counts again");
                Assert.That(light.Color, Is.EqualTo(ownColor), "its own light is back");
                Assert.That(light.Color, Is.Not.EqualTo(EWJoeMeldedStructureSystem.MeldColor));
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>RB-33: Bear Witness asks for 4-6 people and one cast counts at most two of them.</summary>
    [Test]
    public async Task BearWitnessCountsAtMostTwoPerCast()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid joe = default, objective = default;
        var witnesses = new List<EntityUid>();
        await server.WaitAssertion(() =>
        {
            (joe, var mind, var comp) = SpawnJoe(entities, map.GridCoords);
            for (var i = 0; i < 6; i++)
                witnesses.Add(entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 2)));

            objective = Assign(entities, mind, comp, BearWitnessObjective);
            var tally = entities.GetComponent<EWJoeTallyConditionComponent>(objective);
            Assert.Multiple(() =>
            {
                Assert.That(tally.Target, Is.InRange(4, 6), "four to six people");
                Assert.That(tally.MaxPerCast, Is.EqualTo(2));
            });
            tally.Target = 4;

            // one cast's cone reaches three people: two count
            var tallies = entities.System<EWJoeTallySystem>();
            for (var i = 0; i < 3; i++)
                tallies.Credit((joe, comp), EWJoeTallyKind.Witness, witnesses[i], 1);
            Assert.Multiple(() =>
            {
                Assert.That(tally.Count, Is.EqualTo(2), "a cast counts at most two");
                Assert.That(tally.Counted, Does.Not.Contain(witnesses[2]), "the third is not spent");
            });
        });

        // the next cast is a later tick
        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            var tally = entities.GetComponent<EWJoeTallyConditionComponent>(objective);
            var tallies = entities.System<EWJoeTallySystem>();

            // the same two again, then the third: only the third is new
            for (var i = 0; i < 3; i++)
                tallies.Credit((joe, comp), EWJoeTallyKind.Witness, witnesses[i], 1);
            Assert.That(tally.Count, Is.EqualTo(3), "repeats do not use up the cast, and the one left out counts now");

            tallies.Credit((joe, comp), EWJoeTallyKind.Witness, witnesses[3], 1);
            tallies.Credit((joe, comp), EWJoeTallyKind.Witness, witnesses[4], 1);
            Assert.That(tally.Count, Is.EqualTo(4), "the second of this cast finishes the directive; nothing counts past it");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DarkenADistrictNeedsAJoeCausedBlackout()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid apcLit = default, apcDark = default, apcAlways = default;
        EntityCoordinates litCoords = default;
        await server.WaitPost(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            var areas = entities.System<AreaSystem>();
            var grid = entities.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            // anchor on a tile the test map really has and keep the row contiguous: disconnected floor islands
            // are split off onto new grids (without area data) by the engine's grid splitting
            var origin = map.Tile.GridIndices;
            for (var x = 1; x <= 4; x++)
                maps.SetTile(map.Grid, origin + new Vector2i(x, 0), map.Tile.Tile);
            EntityCoordinates Tile(int x) => maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, origin + new Vector2i(x, 0));

            // the Joe stands in the lit district; a dark one and an always-powered one lie east
            areas.ReplaceArea(grid, origin, AreaLit);
            litCoords = Tile(0);
            var darkCoords = Tile(2);
            areas.ReplaceArea(grid, origin + new Vector2i(2, 0), AreaDark);
            var alwaysCoords = Tile(4);
            areas.ReplaceArea(grid, origin + new Vector2i(4, 0), AreaAlwaysPowered);

            apcLit = entities.SpawnEntity(Apc, litCoords);
            apcDark = entities.SpawnEntity(Apc, darkCoords);
            apcAlways = entities.SpawnEntity(Apc, alwaysCoords);
        });

        // the power system registers each APC with its area
        await pair.RunTicksSync(5);

        var darken = entities.System<EWJoeDarkenSystem>();
        try
        {
            EntityUid objective = default;
            EntityUid joe = default;
            Entity<MindComponent> mind = default;
            await server.WaitAssertion(() =>
            {
                var lit = entities.GetComponent<RMCApcComponent>(apcLit).Area;
                var dark = entities.GetComponent<RMCApcComponent>(apcDark).Area;
                var always = entities.GetComponent<RMCApcComponent>(apcAlways).Area;
                Assert.That(lit, Is.Not.Null, "the lit district's APC is registered");
                Assert.That(dark, Is.Not.Null);
                Assert.That(always, Is.Not.Null);
                Assert.That(entities.GetComponent<AreaComponent>(always!.Value).AlwaysPowered, "the landing console area is always powered");

                var powered = new HashSet<EntityUid>();
                darken.PoweredOverride = area => powered.Contains(area);

                (joe, mind, var comp) = SpawnJoe(entities, litCoords);
                var objectives = entities.System<ObjectivesSystem>();

                // a dark planet refuses it; an always-powered area never counts as a district to darken
                Assert.That(objectives.TryCreateObjective(mind, DarkenObjective, out _), Is.False, "every district is dark: refused");

                // only the lit district is ever pinned
                powered.Add(lit!.Value);
                objective = Assign(entities, mind, comp, DarkenObjective);
                var condition = entities.GetComponent<EWJoeDarkenConditionComponent>(objective);
                Assert.That(condition.Area, Is.EqualTo(lit), "the powered district is pinned, never the dark one");

                // armed while lit; the Joe stands inside it
                darken.Poll((objective, condition));
                Assert.That(condition.State, Is.EqualTo(EWJoeDarkenState.Armed));

                // RB-38: standing in the district is not the Joe's work; its own hit on the district's APC is
                Assert.That(darken.CausedByJoe(condition, joe, lit.Value), Is.False, "presence alone no longer counts");
                var stamp = entities.EnsureComponent<CMULastDamagedByComponent>(apcLit);
                stamp.Origin = joe;
                stamp.At = timing.CurTime;
                Assert.That(darken.CausedByJoe(condition, joe, lit.Value), "the Joe hit the APC that feeds it");
                stamp.Origin = apcDark;
                Assert.That(darken.CausedByJoe(condition, joe, lit.Value), Is.False, "something else's hit does not count");
                stamp.Origin = joe;

                // the Joe's blackout starts the timer
                powered.Clear();
                darken.Poll((objective, condition));
                Assert.That(condition.State, Is.EqualTo(EWJoeDarkenState.Dark), "a blackout after the Joe's own hit is its work");
                Assert.That(objectives.IsCompleted(objective, mind), Is.False, "darkness alone does not finish it");

                // the light returning resets it
                powered.Add(lit.Value);
                darken.Poll((objective, condition));
                Assert.That(condition.State, Is.EqualTo(EWJoeDarkenState.Armed), "power returned: back to armed");

                // dark again, and kept dark for the (test-short) duration
                powered.Clear();
                darken.Poll((objective, condition));
                Assert.That(condition.State, Is.EqualTo(EWJoeDarkenState.Dark));
                Assert.That(condition.DarkSince, Is.EqualTo(timing.CurTime));
            });

            await pair.RunTicksSync((int) Math.Ceiling(3.5 / timing.TickPeriod.TotalSeconds));

            await server.WaitAssertion(() =>
            {
                var condition = entities.GetComponent<EWJoeDarkenConditionComponent>(objective);
                Assert.That(condition.State, Is.EqualTo(EWJoeDarkenState.Complete), "dark for the whole duration");
                Assert.That(entities.System<ObjectivesSystem>().IsCompleted(objective, mind), "the district stayed dark");
            });
        }
        finally
        {
            darken.PoweredOverride = null;
        }

        await pair.CleanReturnAsync();
    }
}
