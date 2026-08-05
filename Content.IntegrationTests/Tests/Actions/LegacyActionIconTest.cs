#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Actions;

[TestFixture]
public sealed class LegacyActionIconTest : GameTest
{
    private static readonly EntProtoId ActionPrototype = "ActionFindParasite";

    [Test]
    public async Task LegacyIconLoadsWithoutSpriteComponent()
    {
        await Client.WaitAssertion(() =>
        {
            var prototype = CProtoMan.Index(ActionPrototype);

            Assert.Multiple(() =>
            {
                Assert.That(
                    prototype.TryComp<ActionComponent>(out var action, CEntMan.ComponentFactory),
                    Is.True,
                    $"{ActionPrototype} did not load its Action component.");
                Assert.That(
                    prototype.TryComp<SpriteComponent>(out _, CEntMan.ComponentFactory),
                    Is.False,
                    $"{ActionPrototype} should exercise the legacy icon fallback.");

                var icon = action?.Icon as SpriteSpecifier.Rsi;
                Assert.That(icon, Is.Not.Null, $"{ActionPrototype} did not load its legacy RSI icon.");
                Assert.That(icon?.RsiPath, Is.EqualTo(new ResPath("_RMC14/Actions/observer_actions.rsi")));
                Assert.That(icon?.RsiState, Is.EqualTo("find_parasite"));
                Assert.That(CEntMan.System<SpriteSystem>().Frame0(icon!), Is.Not.Null);
            });
        });
    }

    [TestCase(
        "ActionTogglePropCalling",
        "RMC_observeraction_not_joining",
        "RMC_observeraction_joining")]
    [TestCase(
        "ActionCallProps",
        "RMC_observeraction_call_props",
        null)]
    public async Task LegacyIconPopulatesInheritedSpriteLayers(
        string prototypeId,
        string iconState,
        string? toggledIconState)
    {
        EntityUid action = default;

        await Client.WaitAssertion(() =>
        {
            action = CEntMan.SpawnEntity(prototypeId, MapCoordinates.Nullspace);
        });
        await Client.WaitRunTicks(1);

        await Client.WaitAssertion(() =>
        {
            try
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(action);
                var spriteSystem = CEntMan.System<SpriteSystem>();

                Assert.That(
                    spriteSystem.TryGetLayer((action, sprite), ActionVisuals.Icon, out _, false),
                    Is.True,
                    $"{prototypeId} did not have its base action icon layer.");
                Assert.That(
                    spriteSystem.LayerGetRsiState(
                        (action, sprite),
                        spriteSystem.LayerMapGet((action, sprite), ActionVisuals.Icon)).Name,
                    Is.EqualTo(iconState));

                if (toggledIconState == null)
                    return;

                Assert.That(
                    spriteSystem.TryGetLayer((action, sprite), ActionVisuals.IconToggled, out _, false),
                    Is.True,
                    $"{prototypeId} did not have its toggled action icon layer.");
                Assert.That(
                    spriteSystem.LayerGetRsiState(
                        (action, sprite),
                        spriteSystem.LayerMapGet((action, sprite), ActionVisuals.IconToggled)).Name,
                    Is.EqualTo(toggledIconState));
            }
            finally
            {
                CEntMan.DeleteEntity(action);
            }
        });
    }
}
