using Content.IntegrationTests.Fixtures;

namespace Content.IntegrationTests._CMU14.Localization;

[TestFixture]
public sealed class CMUChemicalContractLocalizationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task ChemicalContractTextIsLocalized()
    {
        await Server.WaitAssertion(() =>
        {
            var localization = Server.ResolveDependency<ILocalizationManager>();

            Assert.Multiple(() =>
            {
                Assert.That(localization.GetString("cmu-paper-header-experiment"),
                    Does.Contain("Official Weyland-Yutani Document"));
                Assert.That(localization.GetString("cmu-paper-subheader-experiment", ("NAME", "Test Compound")),
                    Does.Contain("Test Compound"));
                Assert.That(localization.GetString("cmu-paper-contract-experiment",
                        ("EXP", "X123"),
                        ("NAME", "Test Compound")),
                    Does.Contain("X123").And.Contain("Test Compound"));
                Assert.That(localization.GetString("research-chem-catalyst"), Does.Contain("Catalysts"));
                Assert.That(localization.GetString("cmu-paper-contract-footer"),
                    Does.Contain("Research Data Terminal"));
            });
        });
    }
}
