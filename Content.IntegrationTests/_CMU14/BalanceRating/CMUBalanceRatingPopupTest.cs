using Content.Client.CMU14.UserInterface;
using Content.Client.CMU14.BalanceRating;
using Content.Shared.CMU14.BalanceRating;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.BalanceRating;

[TestFixture]
public sealed class CMUBalanceRatingPopupTest
{
    private const string TargetId = "RMCXenoBoiler";

    [Test]
    public async Task ExpirationCanRemovePopupDuringUiFrame()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true });
        await pair.Client.WaitAssertion(() =>
        {
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            var timing = pair.Client.ResolveDependency<IGameTiming>();
            var parent = new BoxContainer();
            var popup = new CMUBalanceRatingPopup(1, CMUBalanceRatingTarget.Xeno,
                CMUBalanceRatingMetric.Power, TargetId, "Boiler", timing.RealTime);
            var expired = 0;
            // The controller removes expired prompts from their parent in this callback.
            popup.Expired += () =>
            {
                expired++;
                popup.Orphan();
            };
            parent.AddChild(popup);
            parent.AddChild(new Control());
            ui.StateRoot.AddChild(parent);
            try
            {
                var frame = ui.GetType().GetMethod("FrameUpdate")!;
                frame.Invoke(ui, [new FrameEventArgs(0.05f)]);
                frame.Invoke(ui, [new FrameEventArgs(0.05f)]);
                Assert.That(expired, Is.EqualTo(1));
                Assert.That(popup.Parent, Is.Null);
                Assert.That(parent.ChildCount, Is.EqualTo(1));
            }
            finally
            {
                parent.Orphan();
                parent.ReleaseChildren();
                popup.ReleaseChildren();
            }
        });
        await pair.CleanReturnAsync();
    }

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
                popup.ReleaseChildren();
            }
        });

        await pair.CleanReturnAsync();
    }
}
