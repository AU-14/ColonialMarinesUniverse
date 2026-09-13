using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Nutrition;

/// <summary>
/// Tests the mechanics of hunger and thirst.
/// </summary>
public sealed class HungerThirstTest : InteractionTest
{
    private readonly EntProtoId _drink = "DrinkLemonadeGlass";
    private readonly EntProtoId _food = "FoodCakeVanillaSlice";
    protected override string PlayerPrototype => "MobHuman";

    /// <summary>
    /// Tests that hunger and thirst values decrease over time (low means hungrier and thirstier).
    /// Tests that hunger and thirst values increase when eating/drinking (high means less hungry and thirsty).
    /// </summary>
    [Test]
    public async Task HungerThirstIncreaseDecreaseTest()
    {
        // Ensure that the player can breathe and not suffocate
        await AddAtmosphere();

        var satiationComponent = Comp<SatiationComponent>(Player);
        var entity = new Entity<SatiationComponent>(SPlayer, satiationComponent);
        var satiationSystem = SEntMan.System<SatiationSystem>();
        var ingestionSystem = SEntMan.System<IngestionSystem>();

        const string okayThresholdKey = "Okay";

        // Set initial value
        Assert.That(satiationSystem.GetKeysForType(entity, SatiationSystem.Hunger), Contains.Item(okayThresholdKey));
        satiationSystem.SetValue(entity, SatiationSystem.Hunger, okayThresholdKey);
        Assert.That(satiationSystem.GetKeysForType(entity, SatiationSystem.Thirst), Contains.Item(okayThresholdKey));
        satiationSystem.SetValue(entity, SatiationSystem.Thirst, okayThresholdKey);

        // Ensure hunger and thirst value decrease over time (the Urist gets hungrier/thirstier)
        var previousHungerValue = satiationSystem.GetValueOrNull(entity, SatiationSystem.Hunger);
        Assert.That(previousHungerValue, Is.Not.Null);
        var previousThirstValue = satiationSystem.GetValueOrNull(entity, SatiationSystem.Thirst);
        Assert.That(previousThirstValue, Is.Not.Null);

        // Simulate long enough for both update loops to run
        await RunSeconds(2);

        var currentHungerValue = satiationSystem.GetValueOrNull(entity, SatiationSystem.Hunger);
        Assert.That(currentHungerValue, Is.Not.Null.And.LessThan(previousHungerValue), "Hunger value did not decrease over time");
        previousHungerValue = currentHungerValue;

        var currentThirstValue = satiationSystem.GetValueOrNull(entity, SatiationSystem.Thirst);
        Assert.That(currentThirstValue, Is.Not.Null.And.LessThan(previousThirstValue), "Thirst value did not decrease over time");
        previousThirstValue = currentThirstValue;

        // Now we spawn food in the Urist's hand
        await PlaceInHands(_food);

        // We eat the food in hand
        await UseInHand();

        // To see a change in hunger, we need to wait at least 30 seconds
        await RunSeconds(30);

        // We ensure the food is fully eaten
        var foodEaten = HandSys.GetActiveItem((SPlayer, Hands));
        Assert.That(foodEaten, Is.Null, "Food item did not disappear after eating it");

        // Ensure that the hunger value has increased (The Urist is less hungry)
        Assert.That(satiationSystem.GetValueOrNull(entity, SatiationSystem.Hunger), Is.GreaterThan(previousHungerValue!), "Hunger value did not increase after eating food");

        // Now we spawn a drink in the Urist's hand
        var drink = await PlaceInHands(_drink);

        // Get the solution that can be consumed
        Assert.That(ingestionSystem.CanConsume(SPlayer, SPlayer, ToServer(drink), out var solution, out _),
            "Unable to get the solution or the entity can not be consumed");

        // Find the initial amount of solution in the drink
        var initialSolutionVolume = solution.Value.Comp.Solution.Volume;

        // We drink the drink in hand
        await UseInHand();

        // To see a change in thirst, we need to wait at least 30 seconds
        await RunSeconds(30);

        // Ensure the solution volume has decreased
        Assert.That(solution.Value.Comp.Solution.Volume, Is.LessThan(initialSolutionVolume), "Solution volume did not decrease after drinking");

        // Ensure that the thirst value has increased (The Urist is less thirsty)
        Assert.That(satiationSystem.GetValueOrNull(entity, SatiationSystem.Thirst), Is.GreaterThan(previousThirstValue!), "Thirst value did not increase after drinking");

        // Make sure that the glass did not get deleted after drinking from it
        var glass = HandSys.GetActiveItem((SPlayer, Hands));
        Assert.That(glass, Is.Not.Null, "Glass got deleted after drinking from it");
    }

    [Test]
    public async Task RMCDrinkAutoConsumesUntilEmpty()
    {
        var originalPreference = Client.CfgMan.GetCVar(CCVars.CMUAutoIngestEnabled);
        await SetAutoIngestPreference(true);

        var drink = await PlaceInHands("RMCDrinkAlcoholGrenadine");
        var serverDrink = ToServer(drink);
        var edible = SEntMan.GetComponent<EdibleComponent>(serverDrink);
        var ingestion = SEntMan.System<IngestionSystem>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(edible.Edible, Is.EqualTo(IngestionSystem.Drink));
            Assert.That(edible.Solution, Is.EqualTo("drink"));
            Assert.That(edible.DestroyOnEmpty, Is.False);
            Assert.That(edible.TransferAmount, Is.EqualTo(FixedPoint2.New(5)));
        }

        Assert.That(ingestion.CanConsume(SPlayer, SPlayer, serverDrink, out var solution, out _), Is.True);

        try
        {
            await UseInHand();
            await RunSeconds(30);

            Assert.That(solution.Value.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero),
                "Drinks should continue consuming after the first sip until empty.");
        }
        finally
        {
            await SetAutoIngestPreference(originalPreference);
        }
    }

    [Test]
    public async Task AutoIngestCanBeDisabledForFoodAndDrink()
    {
        var originalPreference = Client.CfgMan.GetCVar(CCVars.CMUAutoIngestEnabled);
        await SetAutoIngestPreference(false);

        try
        {
            await AssertConsumesOnePortion("RMCDrinkAlcoholGrenadine");
            await AssertConsumesOnePortion(_food);
        }
        finally
        {
            await SetAutoIngestPreference(originalPreference);
        }
    }

    [Test]
    public async Task HydrationConsumerOverloadUsesHydration()
    {
        var drink = await PlaceInHands("DrinkWaterJug");
        var serverDrink = ToServer(drink);
        var ingestion = SEntMan.System<IngestionSystem>();

        var hydration = ingestion.TotalHydration((serverDrink, null));
        var nutrition = ingestion.TotalNutrition((serverDrink, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hydration, Is.GreaterThan(0f));
            Assert.That(nutrition, Is.Zero);
            Assert.That(ingestion.TotalHydration((serverDrink, null), SPlayer), Is.EqualTo(hydration));
        }
    }

    private async Task AssertConsumesOnePortion(EntProtoId prototype)
    {
        var item = await PlaceInHands(prototype);
        var serverItem = ToServer(item);
        var edible = SEntMan.GetComponent<EdibleComponent>(serverItem);
        var ingestion = SEntMan.System<IngestionSystem>();

        Assert.That(ingestion.CanConsume(SPlayer, SPlayer, serverItem, out var solution, out _), Is.True);
        Assert.That(edible.TransferAmount, Is.Not.Null);
        var initialVolume = solution.Value.Comp.Solution.Volume;

        await UseInHand();
        await RunSeconds(3);

        Assert.That(solution.Value.Comp.Solution.Volume, Is.EqualTo(initialVolume - edible.TransferAmount!.Value),
            $"{prototype} should consume exactly one portion when automatic ingestion is disabled.");

        await Pair.DeleteEntityTreeLeafFirst(serverItem);
    }

    private async Task SetAutoIngestPreference(bool enabled)
    {
        await Client.WaitPost(() => Client.CfgMan.SetCVar(CCVars.CMUAutoIngestEnabled, enabled));
        await Pair.ReallyBeIdle(5);
        await Server.WaitAssertion(() =>
        {
            var netConfig = Server.ResolveDependency<INetConfigurationManager>();
            Assert.That(netConfig.GetClientCVar(Server.PlayerMan.Sessions[0].Channel, CCVars.CMUAutoIngestEnabled),
                Is.EqualTo(enabled));
        });
    }
}
