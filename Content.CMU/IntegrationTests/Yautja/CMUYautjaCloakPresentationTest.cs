using System.Linq;
using System.Runtime.CompilerServices;
using Content.Client._RMC14.NightVision;
using Content.Client.CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Actions;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Humanoid;
using Moq;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUYautjaCloakPresentationTest
{
    [TestCase(null, YautjaInvisibilitySound.Modern)]
    [TestCase(null, YautjaInvisibilitySound.Retro)]
    [TestCase("gloves", YautjaInvisibilitySound.Retro)]
    [TestCase("back", YautjaInvisibilitySound.Modern)]
    public async Task DecloakRetainsTheOpticalSweepUntilTheBodyIsVisible(string? removeSlot, YautjaInvisibilitySound preset)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid bracer = default;
        NetEntity hunterNet = default;
        NetEntity actionNet = default;
        HashSet<HumanoidVisualLayers> hiddenBefore = default!;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            hunter = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            var inventory = entities.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(hunter, "gloves", out var worn), Is.True);
            bracer = worn!.Value;
            foreach (var (slot, prototype) in new[] { ("mask", "CMUYautjaMask"), ("back", "CMUYautjaCloakPack"),
                         ("outerClothing", "CMUYautjaClanArmor"), ("jumpsuit", "CMUYautjaBodyMesh"), ("shoes", "CMUYautjaClanGreaves") })
                Assert.That(inventory.TryEquip(hunter, entities.SpawnEntity(prototype, map.GridCoords), slot, silent: true, force: true), Is.True);
            var bracerComp = entities.GetComponent<YautjaBracerComponent>(bracer);
            bracerComp.InvisibilitySound = preset;
            entities.Dirty(bracer, bracerComp);
            server.PlayerMan.SetAttachedEntity(pair.Player!, hunter);
            hunterNet = entities.GetNetEntity(hunter);
            actionNet = entities.GetNetEntity(entities.System<SharedRMCActionsSystem>()
                .GetActionsWithEvent<YautjaToggleCloakActionEvent>(hunter).Single());
        });
        await pair.RunTicksSync(10);
        await client.WaitPost(() => hiddenBefore = new HashSet<HumanoidVisualLayers>(client.EntMan
            .GetComponent<HideableHumanoidLayersComponent>(client.EntMan.GetEntity(hunterNet)).PermanentlyHiddenLayers));
        await client.WaitPost(() => client.EntMan.RaisePredictiveEvent(new RequestPerformActionEvent(actionNet,
            client.ResolveDependency<IGameTiming>().CurTick)));
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await client.WaitAssertion(() => Assert.That(client.EntMan
            .GetComponent<HideableHumanoidLayersComponent>(client.EntMan.GetEntity(hunterNet)).PermanentlyHiddenLayers,
            Is.EquivalentTo(hiddenBefore), "Cloaking must preserve the composed silhouette instead of hiding hair separately."));
        if (removeSlot != null)
        {
            await server.WaitAssertion(() =>
                Assert.That(server.EntMan.System<InventorySystem>().TryUnequip(hunter, removeSlot, silent: true, force: true), Is.True));
        }
        else
            await client.WaitPost(() => client.EntMan.RaisePredictiveEvent(new RequestPerformActionEvent(actionNet,
                client.ResolveDependency<IGameTiming>().CurTick)));
        await pair.RunTicksSync(pair.SecondsToTicks(.25f));
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var localHunter = entities.GetEntity(hunterNet);
            Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(localHunter), Is.False,
                "Uncloaking must end gameplay invisibility immediately.");
            var cloak = entities.GetComponent<CMUYautjaOpticalCloakComponent>(localHunter);
            Assert.That(CMUYautjaPresentation.CloakCoverage(cloak, client.ResolveDependency<IGameTiming>().CurTime),
                Is.InRange(.5f, .99f), "The equipped sprite should still be mostly refracted near the start of the reverse sweep.");
            entities.System<CMUYautjaVisualsSystem>().FrameUpdate(0);
            var sprite = entities.GetComponent<SpriteComponent>(localHunter);
            Assert.That(entities.System<SpriteSystem>().TryGetPostShader(sprite, "cmu-yautja-optical-cloak", out _), Is.True,
                "The reverse sweep must survive removal of the active cloak and bracer.");
            if (removeSlot == null)
                Assert.That(DrawThermalVision(localHunter), Is.Zero,
                    "The mask's thermal pass must not paint the raw equipped sprite over the reverse sweep.");
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.5f));
        await client.WaitAssertion(() =>
        {
            client.EntMan.System<CMUYautjaVisualsSystem>().FrameUpdate(0);
            var localHunter = client.EntMan.GetEntity(hunterNet);
            var sprite = client.EntMan.GetComponent<SpriteComponent>(localHunter);
            Assert.That(client.EntMan.System<SpriteSystem>().TryGetPostShader(sprite, "cmu-yautja-optical-cloak", out _), Is.False,
                "The reverse sweep must release its shader when the body is fully visible.");
            if (removeSlot == null)
                Assert.That(DrawThermalVision(localHunter), Is.GreaterThan(0),
                    "Thermal highlighting must resume once the reverse sweep has finished.");
        });
        await pair.CleanReturnAsync();
    }

    private static int DrawThermalVision(EntityUid hunter)
    {
        var entities = IoCManager.Resolve<IEntityManager>();
        var xform = entities.GetComponent<TransformComponent>(hunter);
        var overlay = IoCManager.Resolve<IOverlayManager>().GetOverlay<NightVisionOverlay>();
        var world = new Mock<DrawingHandleWorld>(MockBehavior.Loose, Texture.White);
        var render = new Mock<IRenderHandle>();
        render.SetupGet(handle => handle.DrawingHandleWorld).Returns(world.Object);
        var eye = new Mock<IEye>();
        eye.SetupGet(e => e.Position).Returns(new MapCoordinates(xform.WorldPosition, xform.MapID));
        var viewport = new Mock<IClydeViewport>();
        viewport.SetupGet(v => v.Eye).Returns(eye.Object);
        var bounds = new Box2(-10, -10, 10, 10);
        var args = CreateDrawArgs(overlay.Space, null, viewport.Object, render.Object,
            new UIBox2i(0, 0, 640, 640), xform.MapUid!.Value, xform.MapID, bounds, new Box2Rotated(bounds, Angle.Zero));
        DrawOverlay(overlay, in args);
        return world.Invocations.Count(call => call.Method.Name is "DrawTextureRect" or "DrawTextureRectRegion");
    }

    // Exercise the actual overlay pass, not only its component or shader bookkeeping.
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern OverlayDrawArgs CreateDrawArgs(OverlaySpace space, IViewportControl? viewportControl,
        IClydeViewport viewport, IRenderHandle renderHandle, in UIBox2i viewportBounds, in EntityUid mapUid,
        in MapId mapId, in Box2 worldAabb, in Box2Rotated worldBounds);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Draw")]
    private static extern void DrawOverlay(NightVisionOverlay overlay, in OverlayDrawArgs args);
}
