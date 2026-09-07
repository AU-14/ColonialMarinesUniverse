using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Nutrition;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class GeneratedReagentFlavorTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task GeneratedReagentHasResolvableFlavor()
    {
        await Server.WaitAssertion(() =>
        {
            const string id = "CMUServerLogFlavorRegression";
            var data = new GeneratedReagentData
            {
                ID = id,
                Name = "flavor regression reagent",
                Effects = new() { ["Antitoxic"] = 1 },
                Recipe = new() { ["Water"] = (1, false) },
            };
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(data));
            var reagent = SProtoMan.Index<ReagentPrototype>(id);
            Assert.That(reagent.Flavor, Is.Not.Null);
            Assert.That(SProtoMan.TryIndex<FlavorPrototype>(reagent.Flavor, out _), Is.True);
        });
    }
}
