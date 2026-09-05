using Content.Shared.Stacks;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Medical.Diagnostics;

[TestFixture]
public sealed class FieldTreatmentStackIconTest
{
    [Test]
    public async Task StackIconsResolveTheirDeclaredStatesThroughClientResources()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var resources = pair.Client.ResolveDependency<IResourceCache>();
            var sprites = pair.Client.EntMan.System<SpriteSystem>();
            var checkedIcons = 0;
            foreach (var stack in prototypes.EnumeratePrototypes<StackPrototype>())
            {
                if (stack.Icon is not SpriteSpecifier.Rsi icon ||
                    !icon.RsiPath.ToString().EndsWith("/field_treatments.rsi", StringComparison.Ordinal))
                    continue;

                var resource = resources.GetResource<RSIResource>(icon.RsiPath, useFallback: false);
                Assert.That(resource.RSI.TryGetState(icon.RsiState, out var state), Is.True,
                    $"{stack.ID} must resolve its declared stack icon state.");
                using var control = new TextureRect { Texture = sprites.Frame0(icon) };
                Assert.That(control.Texture, Is.SameAs(state!.Frame0),
                    $"{stack.ID} must display its own RSI frame, not an error texture.");
                checkedIcons++;
            }
            Assert.That(checkedIcons, Is.GreaterThanOrEqualTo(17),
                "The client prototype/resource mount must include the complete field-treatment stack family.");
        });
        await pair.CleanReturnAsync();
    }
}
