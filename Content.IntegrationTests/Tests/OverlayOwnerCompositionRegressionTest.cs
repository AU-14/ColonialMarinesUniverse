using System.Reflection;
using Content.Client.Nutrition;
using Content.Client.Nutrition.EntitySystems;
using Content.Client.Overlays;
using Content.Client.Weather;
using Content.IntegrationTests.Fixtures;
using Content.Shared.StatusEffectNew;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(NutritionOverlaySystem))]
[TestOf(typeof(StencilOverlaySystem))]
public sealed class OverlayOwnerCompositionRegressionTest : GameTest
{
    [Test]
    public async Task StartupComposesOneOverlayAndShutdownRemovesIt()
    {
        await Client.WaitAssertion(() =>
        {
            var overlays = Client.ResolveDependency<IOverlayManager>();
            var nutritionOwner = Client.System<NutritionOverlaySystem>();
            var stencilOwner = Client.System<StencilOverlaySystem>();

            Assert.That(overlays.TryGetOverlay<NutritionOverlay>(out var nutrition), Is.True);
            Assert.That(overlays.TryGetOverlay<StencilOverlay>(out var stencil), Is.True);
            AssertOwnerComposition(nutrition!, stencil!);

            try
            {
                nutritionOwner.Shutdown();
                stencilOwner.Shutdown();

                Assert.Multiple(() =>
                {
                    Assert.That(overlays.HasOverlay<NutritionOverlay>(), Is.False);
                    Assert.That(overlays.HasOverlay<StencilOverlay>(), Is.False);
                });
            }
            finally
            {
                if (!overlays.HasOverlay<NutritionOverlay>())
                    nutritionOwner.Initialize();

                if (!overlays.HasOverlay<StencilOverlay>())
                    stencilOwner.Initialize();
            }

            Assert.That(overlays.TryGetOverlay<NutritionOverlay>(out var restoredNutrition), Is.True);
            Assert.That(overlays.TryGetOverlay<StencilOverlay>(out var restoredStencil), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(restoredNutrition, Is.Not.SameAs(nutrition));
                Assert.That(restoredStencil, Is.Not.SameAs(stencil));
            });
            AssertOwnerComposition(restoredNutrition!, restoredStencil!);
        });
    }

    private void AssertOwnerComposition(NutritionOverlay nutrition, StencilOverlay stencil)
    {
        Assert.Multiple(() =>
        {
            Assert.That(GetPrivate<ClientSatiationSystem>(nutrition, "_satiation"),
                Is.SameAs(Client.System<ClientSatiationSystem>()));
            Assert.That(GetPrivate<EntityLookupSystem>(stencil, "_entLookup"),
                Is.SameAs(Client.System<EntityLookupSystem>()));
            Assert.That(GetPrivate<StatusEffectsSystem>(stencil, "_statusEffects"),
                Is.SameAs(Client.System<StatusEffectsSystem>()));
            Assert.That(GetPrivate<WeatherSystem>(stencil, "_weather"),
                Is.SameAs(Client.System<WeatherSystem>()));
        });
    }

    private static T GetPrivate<T>(object instance, string field)
    {
        return (T) instance.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }
}
