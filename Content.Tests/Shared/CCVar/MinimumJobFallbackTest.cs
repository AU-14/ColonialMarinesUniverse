using Content.Shared.CCVar;
using NUnit.Framework;
using Robust.Shared.Configuration;

namespace Content.Tests.Shared.CCVar;

[TestFixture]
public sealed class MinimumJobFallbackTest
{
    [Test]
    public void DefaultDoesNotIgnorePlayerPreferences()
    {
        Assert.That(CCVars.GameMinimumJobFallback.DefaultValue, Is.EqualTo(MinimumJobFallback.None));
    }

    [Test]
    public void UnsafeFallbackDoesNotPersistAcrossServerRestarts()
    {
        Assert.That(CCVars.GameMinimumJobFallback.Flags.HasFlag(CVar.ARCHIVE), Is.False);
    }
}
