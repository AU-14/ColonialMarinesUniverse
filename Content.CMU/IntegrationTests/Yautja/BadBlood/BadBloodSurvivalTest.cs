using Content.IntegrationTests.Pair;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Body;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// Wave 3: Bad Blood hunger, thirst, diet and the penalties for ignoring them. Honourable hunters must not change
[TestFixture]
public sealed class BadBloodSurvivalTest
{
    private const string Hunter = "CMUMobYautja";
    private const string HunterBracer = "CMUYautjaBracer";
    private const string BadBloodBracer = "CMUYautjaBadBloodBracer";
    private const string BadBloodStomach = "CMUOrganYautjaBadBloodStomach";

    private const string CookedHuman = "CMUFoodMeatPreyHumanCooked";
    private const string CookedXeno = "CMUFoodMeatPreyXenoCooked";
    private const string MreFood = "CMMREFoodCracker";
    private const string VendingFood = "RMCFoodSnackBurrito";
    private const string Pill = "CMPillBicaridine";
    private const string JuiceCarton = "DrinkJuiceOrangeCarton";

    private const string Water = "Water";
    private const string Juice = "JuiceOrange";
    private const string Nutriment = "Nutriment";

    private static readonly ProtoId<TagPrototype> PreyCookedTag = "CMUYautjaPreyCooked";

    // hunger: Starving at or below 50, Peckish to 110, fed to 380, well fed above. thirst: Parched 50, Thirsty 98
    private const float StarvingHunger = 20f;
    private const float PeckishHunger = 80f;
    private const float FedHunger = 200f;
    private const float WellFedHunger = 450f;
    private const float ParchedThirst = 20f;
    private const float FedThirst = 250f;

    // ECS-105
    [Test]
    public async Task BadBloodHungerAndThirstDrainAtTheBadBloodRate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid badBlood = default;
        EntityUid hunter = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            badBlood = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
            hunter = entMan.SpawnEntity(Hunter, map.GridCoords.Offset(new Vector2(0, 2)));

            Assert.Multiple(() =>
            {
                Assert.That(GetValue(entMan, badBlood, SatiationSystem.Hunger), Is.InRange(300f, 380f),
                    "A Bad Blood must start partly drained, not at a random human value.");
                Assert.That(GetValue(entMan, badBlood, SatiationSystem.Thirst), Is.InRange(300f, 378f));
            });

            foreach (var uid in new[] { badBlood, hunter })
                SetSatiation(entMan, uid, FedHunger, FedThirst);
        });

        await pair.RunSeconds(30);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                // -0.2 / 1.5 a second for hunger, -0.1 a second for thirst
                Assert.That(FedHunger - GetValue(entMan, badBlood, SatiationSystem.Hunger), Is.EqualTo(4f).Within(0.5f),
                    "Bad Blood hunger must drain at 8 per minute.");
                Assert.That(FedThirst - GetValue(entMan, badBlood, SatiationSystem.Thirst), Is.EqualTo(3f).Within(0.5f),
                    "Bad Blood thirst must drain at 6 per minute.");

                // -0.02 / 1.5 and -0.02
                Assert.That(FedHunger - GetValue(entMan, hunter, SatiationSystem.Hunger), Is.EqualTo(0.4f).Within(0.2f),
                    "An honourable hunter's hunger must not change.");
                Assert.That(FedThirst - GetValue(entMan, hunter, SatiationSystem.Thirst), Is.EqualTo(0.6f).Within(0.2f),
                    "An honourable hunter's thirst must not change.");
            });
        });

        await Delete(pair, badBlood, hunter);
        await pair.CleanReturnAsync();
    }

    // ECS-107
    [Test]
    public async Task CookRecipeTableResolvesAndCookedPreyIsTagged()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var protoMan = server.ProtoMan;
            var tags = entMan.System<TagSystem>();
            var recipes = protoMan.EnumeratePrototypes<CMUYautjaPreyCookRecipePrototype>().ToList();

            Assert.That(recipes, Is.Not.Empty);
            Assert.That(recipes.Select(recipe => recipe.Cooked.Id), Is.SupersetOf(new[] { CookedHuman, CookedXeno }));

            foreach (var recipe in recipes)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(protoMan.HasIndex<EntityPrototype>(recipe.Input.Id), Is.True,
                        $"{recipe.ID} cooks {recipe.Input.Id}, which does not exist.");
                    Assert.That(protoMan.HasIndex<EntityPrototype>(recipe.Cooked.Id), Is.True,
                        $"{recipe.ID} makes {recipe.Cooked.Id}, which does not exist.");
                    Assert.That(recipe.Count, Is.GreaterThan(0));
                });

                var cooked = entMan.SpawnEntity(recipe.Cooked.Id, map.GridCoords);
                Assert.That(tags.HasTag(cooked, PreyCookedTag), Is.True,
                    $"{recipe.Cooked.Id} must carry the tag the Bad Blood stomach accepts.");
                entMan.DeleteEntity(cooked);
            }
        });

        await pair.CleanReturnAsync();
    }

    // ECS-106
    [Test]
    public async Task BadBloodStomachOnlyTakesCookedPreyAndPills()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<BodySystem>();
            var ingestion = entMan.System<IngestionSystem>();

            var badBlood = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
            var hunter = entMan.SpawnEntity(Hunter, map.GridCoords.Offset(new Vector2(0, 2)));

            var badBloodStomachs = body.EnumerateOrgans<StomachComponent>(badBlood).ToList();
            var hunterStomachs = body.EnumerateOrgans<StomachComponent>(hunter).ToList();
            Assert.That(badBloodStomachs, Has.Count.EqualTo(1));
            Assert.That(hunterStomachs, Has.Count.EqualTo(1));

            var badBloodStomach = new Entity<StomachComponent>(badBloodStomachs[0].Owner, badBloodStomachs[0].Comp2);
            var hunterStomach = new Entity<StomachComponent>(hunterStomachs[0].Owner, hunterStomachs[0].Comp2);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<MetaDataComponent>(badBloodStomach.Owner).EntityPrototype?.ID,
                    Is.EqualTo(BadBloodStomach));
                Assert.That(body.EnumerateOrgans<OrganComponent>(badBlood).Count(),
                    Is.EqualTo(body.EnumerateOrgans<OrganComponent>(hunter).Count()),
                    "Swapping the stomach must not drop any other organ.");
            });

            var spawned = new List<EntityUid> { badBlood, hunter };
            foreach (var (food, allowed) in new[]
                     {
                         (MreFood, false), (VendingFood, false), (CookedHuman, true), (CookedXeno, true), (Pill, true),
                     })
            {
                var item = entMan.SpawnEntity(food, map.GridCoords);
                spawned.Add(item);

                Assert.Multiple(() =>
                {
                    Assert.That(ingestion.IsDigestibleBy(item, badBloodStomach), Is.EqualTo(allowed),
                        $"Bad Blood stomach and {food}.");
                    Assert.That(ingestion.IsDigestibleBy(item, hunterStomach), Is.True,
                        $"An honourable hunter must still be able to eat {food}.");
                });
            }

            foreach (var uid in spawned)
                entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    // FLOW-107
    [Test]
    public async Task BadBloodsOnlySwallowTheWaterInADrink()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var badBlood = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
            var hunter = entMan.SpawnEntity(Hunter, map.GridCoords.Offset(new Vector2(0, 2)));
            var carton = entMan.SpawnEntity(JuiceCarton, map.GridCoords);
            var steak = entMan.SpawnEntity(CookedHuman, map.GridCoords);

            var badBloodSip = Mix(Juice, Water);
            var badBloodEv = new IngestingEvent(carton, badBloodSip, false);
            entMan.EventBus.RaiseLocalEvent(badBlood, ref badBloodEv);

            var hunterSip = Mix(Juice, Water);
            var hunterEv = new IngestingEvent(carton, hunterSip, false);
            entMan.EventBus.RaiseLocalEvent(hunter, ref hunterEv);

            var bite = Mix(Nutriment, Water);
            var biteEv = new IngestingEvent(steak, bite, false);
            entMan.EventBus.RaiseLocalEvent(badBlood, ref biteEv);

            Assert.Multiple(() =>
            {
                Assert.That(badBloodSip.GetTotalPrototypeQuantity(Juice), Is.EqualTo(FixedPoint2.Zero),
                    "A Bad Blood must get nothing but water out of a drink.");
                Assert.That(badBloodSip.GetTotalPrototypeQuantity(Water), Is.EqualTo(FixedPoint2.New(5)),
                    "The water in the drink must still go down.");
                Assert.That(badBloodSip.Volume, Is.EqualTo(FixedPoint2.New(5)));

                Assert.That(hunterSip.GetTotalPrototypeQuantity(Juice), Is.EqualTo(FixedPoint2.New(5)),
                    "An honourable hunter drinks what it likes.");
                Assert.That(bite.GetTotalPrototypeQuantity(Nutriment), Is.EqualTo(FixedPoint2.New(5)),
                    "Only drinks are filtered, cooked prey keeps its nutrition.");
            });

            foreach (var uid in new[] { badBlood, hunter, carton, steak })
                entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    // ECS-108: bracer regen, speed, starvation damage and limb regrowth at each threshold
    [Test]
    public async Task HungerPenaltiesFollowTheThresholds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var survival = entMan.System<BadBloodSurvivalSystem>();
            var badBlood = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
            var hunter = entMan.SpawnEntity(Hunter, map.GridCoords.Offset(new Vector2(0, 2)));
            var movement = entMan.GetComponent<MovementSpeedModifierComponent>(badBlood);
            var damage = entMan.GetComponent<SatiationDamageComponent>(badBlood);

            SetSatiation(entMan, badBlood, FedHunger, FedThirst);
            var fedSpeed = movement.SprintSpeedModifier;
            Assert.Multiple(() =>
            {
                Assert.That(RegenMultiplier(entMan, badBlood), Is.EqualTo(1f));
                Assert.That(survival.IsStarving(badBlood), Is.False);
                Assert.That(survival.GetLimbRegrowDelayMultiplier(badBlood), Is.EqualTo(1f));
                Assert.That(damage.Satiations[SatiationSystem.Hunger].Current, Is.Null);
                Assert.That(damage.Satiations[SatiationSystem.Thirst].Current, Is.Null);
            });

            SetSatiation(entMan, badBlood, WellFedHunger, FedThirst);
            Assert.Multiple(() =>
            {
                Assert.That(RegenMultiplier(entMan, badBlood), Is.EqualTo(1.25f), "Well fed.");
                Assert.That(movement.SprintSpeedModifier, Is.EqualTo(fedSpeed).Within(0.001f));
            });

            SetSatiation(entMan, badBlood, PeckishHunger, FedThirst);
            Assert.Multiple(() =>
            {
                Assert.That(RegenMultiplier(entMan, badBlood), Is.EqualTo(0.75f), "Peckish.");
                Assert.That(movement.SprintSpeedModifier, Is.EqualTo(fedSpeed * 0.85f).Within(0.001f), "Peckish.");
                Assert.That(survival.IsStarving(badBlood), Is.False);
                Assert.That(damage.Satiations[SatiationSystem.Hunger].Current, Is.Null);
            });

            SetSatiation(entMan, badBlood, StarvingHunger, FedThirst);
            Assert.Multiple(() =>
            {
                Assert.That(RegenMultiplier(entMan, badBlood), Is.EqualTo(0.5f), "Starving.");
                Assert.That(movement.SprintSpeedModifier, Is.EqualTo(fedSpeed * 0.6f).Within(0.001f), "Starving.");
                Assert.That(survival.IsStarving(badBlood), Is.True);
                Assert.That(survival.GetLimbRegrowDelayMultiplier(badBlood), Is.EqualTo(1.5f));
                Assert.That(damage.Satiations[SatiationSystem.Hunger].Current?.DamageDict["Blunt"],
                    Is.EqualTo(FixedPoint2.New(0.15)), "Starving must hurt.");
            });

            SetSatiation(entMan, badBlood, FedHunger, ParchedThirst);
            Assert.Multiple(() =>
            {
                Assert.That(RegenMultiplier(entMan, badBlood), Is.EqualTo(0.5f), "Parched.");
                Assert.That(movement.SprintSpeedModifier, Is.EqualTo(fedSpeed * 0.6f).Within(0.001f), "Parched.");
                Assert.That(survival.IsStarving(badBlood), Is.True, "Parched counts as starving.");
                Assert.That(damage.Satiations[SatiationSystem.Thirst].Current?.DamageDict["Blunt"],
                    Is.EqualTo(FixedPoint2.New(0.15)), "Being parched must hurt.");
            });

            SetSatiation(entMan, hunter, StarvingHunger, ParchedThirst);
            Assert.Multiple(() =>
            {
                Assert.That(RegenMultiplier(entMan, hunter), Is.EqualTo(1f),
                    "An honourable hunter's bracer regen must not depend on hunger.");
                Assert.That(survival.IsStarving(hunter), Is.False);
                Assert.That(survival.GetLimbRegrowDelayMultiplier(hunter), Is.EqualTo(1f));
                Assert.That(entMan.HasComponent<SatiationDamageComponent>(hunter), Is.False);
            });

            entMan.DeleteEntity(badBlood);
            entMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }

    // ECS-108: the Starving cloak blocker
    [Test]
    public async Task StarvingBadBloodCannotCloak()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var badBlood = SpawnCloaker(entMan, BadBloodTestHelpers.GruntPrototype, BadBloodBracer, map.GridCoords,
                out var badBloodBracer);
            var hunter = SpawnCloaker(entMan, Hunter, HunterBracer, map.GridCoords.Offset(new Vector2(0, 3)),
                out var hunterBracer);

            SetSatiation(entMan, badBlood, StarvingHunger, FedThirst);
            SetSatiation(entMan, hunter, StarvingHunger, ParchedThirst);

            PressCloak(entMan, badBlood, badBloodBracer);
            PressCloak(entMan, hunter, hunterBracer);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.System<YautjaCloakSystem>().GetDamageOverTimeBlocker(badBlood),
                    Is.EqualTo(YautjaCloakDotBlocker.Starving));
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(badBlood), Is.False,
                    "A starving Bad Blood must not be able to raise the cloak.");
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True,
                    "Hunger must not stop an honourable hunter from cloaking.");
            });

            SetSatiation(entMan, badBlood, FedHunger, FedThirst);
            PressCloak(entMan, badBlood, badBloodBracer);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(badBlood), Is.True,
                "The cloak must come back once the Bad Blood has eaten.");

            entMan.DeleteEntity(badBlood);
            entMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }

    private static float GetValue(IEntityManager entMan, EntityUid uid, ProtoId<SatiationTypePrototype> type)
    {
        var ent = new Entity<SatiationComponent>(uid, entMan.GetComponent<SatiationComponent>(uid));
        var value = entMan.System<SatiationSystem>().GetValueOrNull(ent, type);
        Assert.That(value, Is.Not.Null);
        return value ?? 0f;
    }

    private static void SetSatiation(IEntityManager entMan, EntityUid uid, float hunger, float thirst)
    {
        var satiation = entMan.System<SatiationSystem>();
        var ent = new Entity<SatiationComponent>(uid, entMan.GetComponent<SatiationComponent>(uid));
        satiation.SetValue(ent, SatiationSystem.Hunger, hunger);
        satiation.SetValue(ent, SatiationSystem.Thirst, thirst);
    }

    private static float RegenMultiplier(IEntityManager entMan, EntityUid user)
    {
        var ev = new YautjaBracerRegenModifyEvent(user);
        entMan.EventBus.RaiseLocalEvent(user, ref ev);
        return ev.Multiplier;
    }

    private static Solution Mix(string first, string second)
    {
        var solution = new Solution();
        solution.AddReagent(first, FixedPoint2.New(5));
        solution.AddReagent(second, FixedPoint2.New(5));
        return solution;
    }

    private static EntityUid SpawnCloaker(
        IEntityManager entMan,
        string mob,
        string bracerPrototype,
        EntityCoordinates coords,
        out EntityUid bracer)
    {
        var inventory = entMan.System<InventorySystem>();
        var user = entMan.SpawnEntity(mob, coords);

        if (inventory.TryUnequip(user, "gloves", out var oldBracer, silent: true, force: true))
            entMan.DeleteEntity(oldBracer.Value);

        bracer = entMan.SpawnEntity(bracerPrototype, coords);
        Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
        entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 1500;
        return user;
    }

    private static void PressCloak(IEntityManager entMan, EntityUid user, EntityUid bracer)
    {
        var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
        var cloak = new YautjaToggleCloakActionEvent { Performer = user, Action = comp.ToggleCloakAction!.Value };
        entMan.EventBus.RaiseLocalEvent(bracer, cloak);
    }

    private static Task Delete(TestPair pair, params EntityUid[] entities)
    {
        var server = pair.Server;
        return server.WaitPost(() =>
        {
            foreach (var uid in entities)
                server.EntMan.DeleteEntity(uid);
        });
    }
}
