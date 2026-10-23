using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CMU14.Fighter;

namespace Content.IntegrationTests._CMU14.Fighter;

[TestFixture]
public sealed class FighterPadPackTest : InteractionTest
{
    [Test]
    public async Task DeployedPadFoldsBackUpAndKeepsItsSide()
    {
        await SpawnTarget("CMUFighterLandingPadGovfor");

        await InteractUsing(Wrench);
        await InteractUsing(Screw);
        await InteractUsing(Wrench);

        await AssertEntityLookup(new EntitySpecifier("CMUFighterLandingPadFolded", 1));
        await Server.WaitAssertion(() =>
        {
            var query = SEntMan.EntityQueryEnumerator<FighterIFFComponent, MetaDataComponent>();
            var found = false;
            while (query.MoveNext(out _, out var iff, out var meta))
            {
                if (meta.EntityPrototype?.ID != "CMUFighterLandingPadFolded")
                    continue;

                found = true;
                // whoever folds it, it unfolds as the same side's pad again
                Assert.That(iff.Faction, Is.EqualTo("govfor"));
            }

            Assert.That(found, Is.True);
        });
    }
}
