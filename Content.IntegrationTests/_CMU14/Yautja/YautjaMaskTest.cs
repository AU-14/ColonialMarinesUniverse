using System.Linq;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Armor.ThermalCloak;
using Content.Shared._RMC14.NightVision;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaMaskTest
{
    [Test]
    public async Task MaskEquipsAutomaticVisorAndCleansUpWithoutLegacyActions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid cloakPack = default;
        EntityUid mask = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var actions = entMan.System<SharedActionsSystem>();
                var inventory = entMan.System<InventorySystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
                cloakPack = entMan.SpawnEntity("CMUYautjaCloakPack", MapCoordinates.Nullspace);
                mask = entMan.SpawnEntity("CMUYautjaMask", MapCoordinates.Nullspace);

                entMan.EnsureComponent<YautjaComponent>(hunter);

                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, cloakPack, "back", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(hunter, mask, "mask", silent: true, force: true), Is.True);

                Assert.That(entMan.GetComponent<YautjaMaskComponent>(mask).VisorEnabled, Is.True);
                Assert.That(entMan.HasComponent<YautjaHudViewerComponent>(hunter), Is.True);

                var nightVisionItem = entMan.GetComponent<NightVisionItemComponent>(mask);
                Assert.That(nightVisionItem.Toggleable, Is.False);
                Assert.That(nightVisionItem.DefaultState, Is.EqualTo(NightVisionState.Full));
                Assert.That(nightVisionItem.Green, Is.False);

                var nightVision = entMan.GetComponent<NightVisionComponent>(hunter);
                Assert.That(nightVision.State, Is.EqualTo(NightVisionState.Full));
                Assert.That(nightVision.Green, Is.False);

                Assert.That(entMan.GetComponent<ThermalCloakComponent>(cloakPack).GrantAction, Is.False);

                var actionPrototypeIds = actions.GetActions(hunter)
                    .Select(action => entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID)
                    .Where(id => id != null)
                    .ToHashSet();
                Assert.That(actionPrototypeIds, Does.Contain("CMUActionYautjaToggleVisor"));
                Assert.That(actionPrototypeIds, Does.Contain("CMUActionYautjaToggleCloak"));
                Assert.That(actionPrototypeIds, Does.Not.Contain("ActionToggleMask"));
                Assert.That(actionPrototypeIds, Does.Not.Contain("RMCActionToggleCloak"));

                Assert.That(inventory.TryUnequip(hunter, "mask", silent: true, force: true), Is.True);
            });

            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.GetComponent<YautjaMaskComponent>(mask).VisorEnabled, Is.False);
                Assert.That(server.EntMan.HasComponent<YautjaHudViewerComponent>(hunter), Is.False);
                Assert.That(server.EntMan.HasComponent<NightVisionComponent>(hunter), Is.False);
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                if (hunter.IsValid() && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (bracer.IsValid() && !entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);

                if (cloakPack.IsValid() && !entMan.Deleted(cloakPack))
                    entMan.DeleteEntity(cloakPack);

                if (mask.IsValid() && !entMan.Deleted(mask))
                    entMan.DeleteEntity(mask);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ToggleVisorControlsNightVisionState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid hunter = default;
        EntityUid mask = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();

                hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                mask = entMan.SpawnEntity("CMUYautjaMask", MapCoordinates.Nullspace);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, mask, "mask", silent: true, force: true), Is.True);

                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.VisorEnabled, Is.True);
                Assert.That(entMan.GetComponent<NightVisionComponent>(hunter).State, Is.EqualTo(NightVisionState.Full));
                Assert.That(maskComp.ToggleVisorAction, Is.Not.Null);

                var action = maskComp.ToggleVisorAction!.Value;
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var toggle = new YautjaToggleVisorActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(mask, toggle);
                Assert.That(maskComp.VisorEnabled, Is.False);

                Assert.That(entMan.HasComponent<NightVisionComponent>(hunter), Is.True);
            });

            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.HasComponent<NightVisionComponent>(hunter), Is.False);
                Assert.That(entMan.HasComponent<YautjaHudViewerComponent>(hunter), Is.False);
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                if (hunter.IsValid() && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (mask.IsValid() && !entMan.Deleted(mask))
                    entMan.DeleteEntity(mask);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MaskZoomUsesBinocularStyleZoomOutAndOffset()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var mask = entMan.SpawnEntity("CMUYautjaMask", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, mask, "mask", silent: true, force: true), Is.True);

                var maskComp = entMan.GetComponent<YautjaMaskComponent>(mask);
                Assert.That(maskComp.ZoomLevel, Is.GreaterThan(1f));
                Assert.That(maskComp.ToggleZoomAction, Is.Not.Null);

                var action = maskComp.ToggleZoomAction!.Value;
                var actionComp = entMan.GetComponent<ActionComponent>(action);
                var toggle = new YautjaToggleMaskZoomActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(mask, toggle);

                Assert.That(maskComp.Zoomed, Is.True);
                Assert.That(entMan.GetComponent<ContentEyeComponent>(hunter).TargetZoom.X, Is.EqualTo(maskComp.ZoomLevel));
                Assert.That(entMan.TryGetComponent(hunter, out YautjaMaskZoomComponent zoom), Is.True);
                Assert.That(Math.Abs(zoom!.Offset.X) + Math.Abs(zoom.Offset.Y), Is.GreaterThan(0));

                toggle = new YautjaToggleMaskZoomActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(mask, toggle);

                Assert.That(maskComp.Zoomed, Is.False);
                Assert.That(entMan.GetComponent<ContentEyeComponent>(hunter).TargetZoom.X, Is.EqualTo(1f));
                Assert.That(entMan.HasComponent<YautjaMaskZoomComponent>(hunter), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(mask))
                    entMan.DeleteEntity(mask);
            }
        });

        await pair.CleanReturnAsync();
    }
}
