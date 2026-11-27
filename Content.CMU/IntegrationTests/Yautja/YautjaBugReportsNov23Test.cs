using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Yautja;
using Content.Shared._RMC14.Campfire;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.ActionBlocker;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Roles;
using Content.Shared.Temperature;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaBugReportsNov23Test : InteractionTest
{
    // bug 1482: an uncharged war axe can't leave the hand (cmss13), the chain holds it within 6 tiles and is lost when
    // the axe can't follow, so it can't be yanked back from across the map or another level
    [Test]
    public async Task WarAxeNeedsBloodToThrowAndItsChainHasALength()
    {
        var axeNet = await PlaceInHands("CMUYautjaWarAxe");
        EntityUid axe = default;
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<YautjaComponent>(SPlayer);
            axe = SEntMan.GetEntity(axeNet);
            Assert.That(Server.System<ActionBlockerSystem>().CanThrow(SPlayer, axe), Is.False,
                "it refuses to leave the hand until it's charged with prey blood");

            SEntMan.GetComponent<YautjaChainedWeaponComponent>(axe).Charged = true;
            Assert.That(Server.System<ActionBlockerSystem>().CanThrow(SPlayer, axe), Is.True);

            Assert.That(Server.System<SharedHandsSystem>().TryDrop(SPlayer, axe, checkActionBlocker: false), Is.True);
        });
        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            var chained = SEntMan.GetComponent<YautjaChainedWeaponComponent>(axe);
            Assert.That(chained.LinkedTo, Is.EqualTo(SPlayer), "dropping it on the floor chains it to the hunter");

            var xform = Server.System<SharedTransformSystem>();
            var at = xform.GetMapCoordinates(SPlayer);
            xform.SetMapCoordinates(SPlayer, new MapCoordinates(at.Position + new Vector2(15, 0), at.MapId));
        });
        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            var xform = Server.System<SharedTransformSystem>();
            var distance = (xform.GetMapCoordinates(axe).Position - xform.GetMapCoordinates(SPlayer).Position).Length();
            Assert.That(distance, Is.LessThanOrEqualTo(6.1f), "the chain drags the axe along instead of stretching");
            Assert.That(SEntMan.GetComponent<YautjaChainedWeaponComponent>(axe).LinkedTo, Is.EqualTo(SPlayer));

            SEntMan.System<SharedMapSystem>().CreateMap(out var mapId);
            xform.SetMapCoordinates(SPlayer, new MapCoordinates(Vector2.Zero, mapId));
        });
        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<YautjaChainedWeaponComponent>(axe).LinkedTo, Is.Null,
                "a chain can't reach another level, it's lost");
        });
    }

    // bug 1511: cmss13 gives the plasma pistol heat_source, so it lights braziers
    [Test]
    public async Task PlasmaPistolLightsABrazier()
    {
        await SpawnTarget("CMUYautjaBrazier");
        var pistol = await PlaceInHands("CMUYautjaPlasmaPistol");
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<YautjaComponent>(SPlayer);
            var hot = new IsHotEvent();
            SEntMan.EventBus.RaiseLocalEvent(SEntMan.GetEntity(pistol), hot);
            Assert.That(hot.IsHot, Is.True);
        });

        await Interact();
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CampfireComponent>(STarget!.Value).Lit, Is.True));
    }

    // bug 1511: the hunter ship's empty braziers were static props that could never be filled or lit
    [Test]
    public async Task HunterShipEmptyBrazierFillsWithWood()
    {
        await SpawnTarget("CMUHunterShipPlacedObjStructurePropBrazierFrameBrazierFrameSouthOffset0x12");
        await InteractUsing("RMCWood", 5);
        AssertPrototype("CMUYautjaBrazier");
    }

    // bug 1509: butchered xeno meat burnt yautja with 30u sulfuric acid. cmss13 never runs chemicals on yautja
    [Test]
    public async Task YautjaDigestXenoMeatWithoutTheAcid()
    {
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<YautjaComponent>(SPlayer);
            var bite = new Solution();
            bite.AddReagent("CMUXenoBlood", 6);
            bite.AddReagent("SulfuricAcid", 5);
            bite.AddReagent("UncookedAnimalProteins", 10);
            var food = SEntMan.SpawnEntity("CMUYautjaFoodMeatXeno", SEntMan.GetCoordinates(TargetCoords));
            var ev = new IngestingEvent(food, bite, false);
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, ref ev);

            Assert.That(bite.GetTotalPrototypeQuantity("CMUXenoBlood").Float(), Is.Zero);
            Assert.That(bite.GetTotalPrototypeQuantity("SulfuricAcid").Float(), Is.Zero);
            Assert.That(bite.GetTotalPrototypeQuantity("Protein").Float(), Is.EqualTo(10f), "the meat itself still feeds them");
        });
    }

    // bug 1507: a prey that bleeds out after the fight still counts for the hunter, and the honour is the player's,
    // not the body's, so it survives the hunter's body being gone at round end
    [Test]
    public async Task HonourGoesToTheLastHunterThatHurtThePreyAndStaysWithThePlayer()
    {
        await SpawnTarget("CMMobHuman");
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<YautjaComponent>(SPlayer);
            var minds = Server.System<SharedMindSystem>();
            if (!minds.TryGetMind(SPlayer, out _, out _))
            {
                var session = Server.ResolveDependency<Robust.Server.Player.IPlayerManager>().Sessions.First();
                var mindId = minds.CreateMind(session.UserId);
                minds.TransferTo(mindId, SPlayer);
            }

            var victim = STarget!.Value;
            var hit = new DamageSpecifier();
            hit.DamageDict["Slash"] = 10;
            Server.System<DamageableSystem>().TryChangeDamage(victim, hit, true, origin: SPlayer);

            // bleeding out: no origin on the death itself
            Server.System<MobStateSystem>().ChangeMobState(victim, MobState.Dead);

            Assert.That(SEntMan.GetComponent<YautjaTrophyRecordComponent>(SPlayer).Score, Is.GreaterThanOrEqualTo(1));

            Assert.That(minds.TryGetMind(SPlayer, out _, out var mind), Is.True);
            var user = mind!.UserId!.Value;
            var scores = Server.System<YautjaHonorBankSystem>().CollectScores();
            Assert.That(scores.GetValueOrDefault(user), Is.GreaterThanOrEqualTo(1), "credited to the player right away");
        });
    }

    // bug 1513: preserve tribals carried the planet Shaman's RMC field medicine
    [Test]
    public async Task PreserveShamanCarriesNoModernMedicine()
    {
        await Server.WaitAssertion(() =>
        {
            var protoMan = Server.ResolveDependency<IPrototypeManager>();
            var job = protoMan.Index<JobPrototype>("CMUJobYautjaHuntTribalShaman");
            Assert.That(job.StartingGear?.Id, Is.EqualTo("CMUGearYautjaHuntTribalShaman"));

            var gear = protoMan.Index<StartingGearPrototype>(job.StartingGear!.Value);
            var items = gear.Equipment.Values.Select(p => p.Id)
                .Concat(gear.Storage.Values.SelectMany(v => v).Select(p => p.Id))
                .ToList();
            foreach (var modern in new[]
                     {
                         "RMCGlassesMedicalHUDGlasses", "RMCPouchFirstAidSplintsGauzeOintment", "CMHealthAnalyzer",
                         "CMFirstAidKitFilledRobust", "CMFirstAidKitSurgeryFilled", "CMGauze10", "CMOintment10",
                     })
            {
                Assert.That(items, Does.Not.Contain(modern));
            }

            var console = new YautjaHuntConsoleComponent();
            var tribal = console.HuntCallOptions.Where(c => c.Id.StartsWith("tribal"))
                .SelectMany(c => c.Spawns.Concat(c.Tiers.SelectMany(t => t.Spawns)))
                .Select(e => e.EntityPrototype?.Id).ToList();
            Assert.That(tribal, Does.Contain("CMUYautjaHuntTribalShaman"));
            Assert.That(tribal, Does.Not.Contain("AU14MobTribalShaman"));
        });
    }

    // bug 1492: a marine's client couldn't see the hive, so the door predicted open on bump and snapped shut again
    [Test]
    public async Task ResinDoorDoesNotPredictOpenForANonHiveClient()
    {
        await SpawnTarget("DoorXenoResin");
        await Server.WaitAssertion(() =>
        {
            var hive = SEntMan.SpawnEntity("CMXenoHive", MapCoordinates.Nullspace);
            Server.System<SharedXenoHiveSystem>().SetHive(STarget!.Value, hive);
        });
        await RunTicks(5);

        await Client.WaitAssertion(() =>
        {
            var attempt = new BeforeDoorOpenedEvent { User = CPlayer };
            CEntMan.EventBus.RaiseLocalEvent(CTarget!.Value, attempt);
            Assert.That(attempt.Cancelled, Is.True, "the client mustn't open a hive door for a marine");
        });
        await Server.WaitAssertion(() =>
        {
            var attempt = new BeforeDoorOpenedEvent { User = SPlayer };
            SEntMan.EventBus.RaiseLocalEvent(STarget!.Value, attempt);
            Assert.That(attempt.Cancelled, Is.True);
        });
    }
}
