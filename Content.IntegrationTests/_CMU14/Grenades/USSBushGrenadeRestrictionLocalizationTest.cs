using Content.IntegrationTests.Fixtures;
using Robust.Shared.Localization;

namespace Content.IntegrationTests._CMU14.Grenades;

[TestFixture]
public sealed class USSBushGrenadeRestrictionLocalizationTest : GameTest
{
    private const string MessageId = "rmc-grenade-blocked-before-hijack";

    [Test]
    public void RestrictionMessageResolvesWithoutFallback()
    {
        var localization = Server.ResolveDependency<ILocalizationManager>();

        Assert.That(localization.HasString(MessageId), Is.True);
        Assert.That(localization.GetString(MessageId),
            Is.EqualTo("Grenades cannot be armed aboard the USS George W. Bush until a dropship hijack begins."));
    }
}
