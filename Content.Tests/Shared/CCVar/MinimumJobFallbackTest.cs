using Content.Shared.CCVar;
using NUnit.Framework;

namespace Content.Tests.Shared.CCVar;

[TestFixture]
public sealed class MinimumJobFallbackTest
{
    [Test]
    public void DefaultDoesNotIgnorePlayerPreferences()
    {
        Assert.That(CCVars.GameMinimumJobFallback.DefaultValue, Is.EqualTo(MinimumJobFallback.None));
    }
}
