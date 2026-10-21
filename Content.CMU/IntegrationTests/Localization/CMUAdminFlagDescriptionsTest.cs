using Content.IntegrationTests.Fixtures;
using Content.Shared.Administration;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.Localization;

/// <summary>
///     Every permission shows what it does when hovered in the permissions panel, so a new flag needs a description.
/// </summary>
[TestFixture]
public sealed class CMUAdminFlagDescriptionsTest : GameTest
{
    [Test]
    public async Task EveryFlagHasADescription()
    {
        await Client.WaitAssertion(() =>
        {
            var loc = Client.ResolveDependency<ILocalizationManager>();
            foreach (var flag in AdminFlagsHelper.AllFlags)
            {
                var key = $"cmu-admin-flag-desc-{flag.ToString().ToLowerInvariant()}";
                Assert.That(loc.HasString(key), Is.True, $"Permission {flag} has no hover description ({key}).");
            }
        });
    }
}
