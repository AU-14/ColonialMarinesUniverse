using Content.Shared.Actions;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaActionSpriteTest
{
    [TestCase("CMUActionYautjaToggleLantern", false, "lantern_on_framed")]
    [TestCase("CMUActionYautjaToggleLantern", true, "lantern_off_framed")]
    [TestCase("CMUActionYautjaToggleVisor", false, "visor_framed")]
    [TestCase("CMUActionYautjaToggleVisor", true, "visor_on_framed")]
    [TestCase("CMUActionYautjaTranslator", false, "translator_framed")]
    [TestCase("CMUActionYautjaAudioPanel", false, "looc_toggle_framed")]
    [TestCase("CMUActionYautjaRaiseThrall", false, "bracer1")]
    [TestCase("CMUActionYautjaHellhoundGorge", false, "headbite")]
    [TestCase("CMUActionYautjaHellhoundSenseOwner", false, "mark_hosts")]
    public async Task ActionIconsUseCurrentSpriteLayers(string id, bool active, string state)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Client.WaitAssertion(() =>
        {
            var prototype = pair.Client.ResolveDependency<IPrototypeManager>().Index<EntityPrototype>(id);
            var icon = ReadIcon(prototype, pair.Client.EntMan.ComponentFactory, active);
            Assert.That(icon, Is.Not.Null);
            Assert.That(icon!.RsiState, Is.EqualTo(state));
        });
        await pair.CleanReturnAsync();
    }

    internal static SpriteSpecifier.Rsi? ReadIcon(EntityPrototype prototype, IComponentFactory factory, bool active = false)
    {
        if (!prototype.TryGetComponent<SpriteComponent>(out var sprite, factory) ||
            !sprite.LayerMapTryGet(active ? ActionVisuals.IconToggled : ActionVisuals.Icon, out var index))
            return null;

        var layer = sprite[index];
        var rsi = layer.ActualRsi;
        if (rsi == null)
            return null;

        return new SpriteSpecifier.Rsi(new ResPath(rsi.Path.ToString().Replace("/Textures/", "")), layer.RsiState.Name);
    }
}
