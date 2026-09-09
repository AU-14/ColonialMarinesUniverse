using Content.Client.CMU14.BalanceRating;
using Content.Shared.CMU14.BalanceRating;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.BalanceRating;

[TestFixture]
public sealed class CMUBalanceRatingPopupTest
{
    private const string TargetId = "RMCXenoBoiler";

    [Test]
    public async Task EntityTargetResolvesPrototypeIcon()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });
        var timing = pair.Client.ResolveDependency<IGameTiming>();
        var systems = pair.Client.ResolveDependency<IEntitySystemManager>();

        await pair.Client.WaitAssertion(() =>
        {
            var popup = new CMUBalanceRatingPopup(
                1,
                CMUBalanceRatingTarget.Xeno,
                CMUBalanceRatingMetric.Power,
                TargetId,
                "Boiler",
                timing.RealTime + TimeSpan.FromSeconds(30));

            try
            {
                var sprites = systems.GetEntitySystem<SpriteSystem>();
                var icon = popup.FindControl<TextureRect>("TargetIcon").Texture;

                Assert.That(icon, Is.Not.Null);
                Assert.That(icon, Is.SameAs(sprites.Frame0(new SpriteSpecifier.EntityPrototype(TargetId))));
                Assert.That(icon, Is.Not.SameAs(sprites.GetFallbackState().Frame0));
            }
            finally
            {
                popup.DisposeAllChildren();
            }
        });

        await pair.CleanReturnAsync();
    }
}
