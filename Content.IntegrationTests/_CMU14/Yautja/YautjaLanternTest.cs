using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Light.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaLanternTest
{
    [Test]
    public async Task YautjaLanternActionHudMatchesCmss13ToggleIconFacts()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var lantern = prototypes.Index<EntityPrototype>("CMUYautjaLantern");
            var action = prototypes.Index<EntityPrototype>("CMUActionYautjaToggleLantern");

            Assert.That(lantern.TryGetComponent<HandheldLightComponent>(out var handheld, factory), Is.True);
            Assert.That(handheld!.ToggleAction, Is.EqualTo("CMUActionYautjaToggleLantern"),
                "CMSS13 special-cases /flashlight/lantern/yautja to use actions_yautja.dmi instead of the generic flashlight action HUD.");

            Assert.That(action.TryGetComponent<ActionComponent>(out var actionComp, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(actionComp!.UseDelay, Is.EqualTo(TimeSpan.FromSeconds(1)),
                    "CMSS13 turn_light() defaults to a 1 second COOLDOWN_LIGHT for flashlight toggles.");
                Assert.That(actionComp.Icon, Is.EqualTo(YautjaActionIcon("lantern_on_framed")),
                    "CMSS13 displays the lantern_on overlay while the light is off.");
                Assert.That(actionComp.IconOn, Is.EqualTo(YautjaActionIcon("lantern_off_framed")),
                    "CMSS13 displays the lantern_off overlay while the light is on.");
            });
        });

        await client.WaitAssertion(() =>
        {
            var cache = client.ResolveDependency<IResourceCache>();
            var rsiPath = new ResPath("/Textures/_CMU14/Yautja/actions.rsi");

            Assert.That(cache.TryGetResource<RSIResource>(rsiPath, out var resource), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(resource!.RSI.TryGetState("lantern_on_framed", out _), Is.True,
                    "Yautja lantern off-state action should use the CMSS13 actions_yautja.dmi lantern_on overlay framed by pred_template.");
                Assert.That(resource.RSI.TryGetState("lantern_off_framed", out _), Is.True,
                    "Yautja lantern on-state action should use the CMSS13 actions_yautja.dmi lantern_off overlay framed by pred_template.");
                Assert.That(resource.RSI.TryGetState("pred_template", out _), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaLanternToggleActionKeepsRuntimeLightCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<ActionContainerSystem>();
            var actionSystem = entMan.System<SharedActionsSystem>();
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var lantern = entMan.SpawnEntity("CMUYautjaLantern", MapCoordinates.Nullspace);

            try
            {
                var handheld = entMan.GetComponent<HandheldLightComponent>(lantern);
                var itemActions = new GetItemActionsEvent(actions, user, lantern);
                entMan.EventBus.RaiseLocalEvent(lantern, itemActions);

                Assert.That(handheld.ToggleActionEntity, Is.Not.Null);
                Assert.That(itemActions.Actions, Does.Contain(handheld.ToggleActionEntity.Value));
                Assert.That(entMan.GetComponent<MetaDataComponent>(handheld.ToggleActionEntity.Value).EntityPrototype?.ID,
                    Is.EqualTo("CMUActionYautjaToggleLantern"));

                var action = actionSystem.GetAction(handheld.ToggleActionEntity);
                Assert.That(action, Is.Not.Null);
                Assert.That(action!.Value.Comp.UseDelay, Is.EqualTo(TimeSpan.FromSeconds(1)));
            }
            finally
            {
                if (!entMan.Deleted(user))
                    entMan.DeleteEntity(user);
                if (!entMan.Deleted(lantern))
                    entMan.DeleteEntity(lantern);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static SpriteSpecifier.Rsi YautjaActionIcon(string state)
    {
        return new SpriteSpecifier.Rsi(new ResPath("_CMU14/Yautja/actions.rsi"), state);
    }
}
