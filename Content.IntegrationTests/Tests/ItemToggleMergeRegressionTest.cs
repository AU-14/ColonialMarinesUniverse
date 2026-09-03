#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Lobby;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(ItemToggleSystem))]
public sealed class ItemToggleMergeRegressionTest : GameTest
{
    private const string ActivateSound = "/Audio/Weapons/ebladeon.ogg";
    private const string DeactivateSound = "/Audio/Weapons/ebladeoff.ogg";
    private const string ActiveSound = "/Audio/Weapons/ebladehum.ogg";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ItemToggleMergeAttachable
  components:
  - type: ItemToggle
    requireComplexInteract: false
  - type: AttachableToggleable
    attachedOnly: true
    attached: false
  - type: ItemToggleMergeProbe

- type: entity
  id: ItemToggleMergeAudio
  components:
  - type: ItemToggle
    requireComplexInteract: false
    soundActivate:
      path: /Audio/Weapons/ebladeon.ogg
    soundDeactivate:
      path: /Audio/Weapons/ebladeoff.ogg
  - type: ItemToggleActiveSound
    activeSound:
      path: /Audio/Weapons/ebladehum.ogg
  - type: ItemToggleMergeProbe

- type: entity
  parent: ItemToggleMergeAudio
  id: ItemToggleMergePreviewAudio
  components:
  - type: LobbyPreviewEntity
";

    [Test]
    public async Task DetachedAndAttachedInteractionPathsPreserveHandledContract()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<ItemToggleMergeProbeSystem>();
            var item = SEntMan.SpawnEntity("ItemToggleMergeAttachable", map.GridCoords);
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);

            try
            {
                var toggle = SEntMan.GetComponent<ItemToggleComponent>(item);
                var attachable = SEntMan.GetComponent<AttachableToggleableComponent>(item);
                var probe = SEntMan.GetComponent<ItemToggleMergeProbeComponent>(item);

                var detachedHand = new UseInHandEvent(user);
            SEntMan.EventBus.RaiseLocalEvent(item, detachedHand);
                Assert.Multiple(() =>
                {
                    Assert.That(detachedHand.Handled, Is.False,
                        "detached AttachedOnly use-in-hand returns without consuming the interaction");
                    Assert.That(toggle.Activated, Is.False);
                    Assert.That(probe.Toggles, Is.Empty);
                });

                var detachedWorld = new ActivateInWorldEvent(user, item, true);
            SEntMan.EventBus.RaiseLocalEvent(item, detachedWorld);
                Assert.Multiple(() =>
                {
                    Assert.That(detachedWorld.Handled, Is.True,
                        "detached AttachedOnly world activation is consumed without toggling");
                    Assert.That(toggle.Activated, Is.False);
                    Assert.That(probe.Toggles, Is.Empty);
                });

                attachable.Attached = true;
                var attachedHand = new UseInHandEvent(user);
            SEntMan.EventBus.RaiseLocalEvent(item, attachedHand);
                Assert.Multiple(() =>
                {
                    Assert.That(attachedHand.Handled, Is.True);
                    Assert.That(toggle.Activated, Is.True);
                    Assert.That(probe.Toggles, Is.EqualTo(new[] { true }));
                });

                var attachedWorld = new ActivateInWorldEvent(user, item, true);
            SEntMan.EventBus.RaiseLocalEvent(item, attachedWorld);
                Assert.Multiple(() =>
                {
                    Assert.That(attachedWorld.Handled, Is.True);
                    Assert.That(toggle.Activated, Is.False);
                    Assert.That(probe.Toggles, Is.EqualTo(new[] { true, false }));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(item);
                SEntMan.DeleteEntity(user);
            }
        });
    }

    [Test]
    public async Task LobbyPreviewItemOrUserSuppressesSoundsButNotStateOrEvents()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<ItemToggleMergeProbeSystem>();
            var system = Server.System<ItemToggleSystem>();
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var previewUser = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            SEntMan.EnsureComponent<LobbyPreviewEntityComponent>(previewUser);
            var normal = SEntMan.SpawnEntity("ItemToggleMergeAudio", map.GridCoords);
            var previewItem = SEntMan.SpawnEntity("ItemToggleMergePreviewAudio", map.GridCoords);
            var previewUserItem = SEntMan.SpawnEntity("ItemToggleMergeAudio", map.GridCoords);

            try
            {
                var normalActivateBefore = AudioCount(ActivateSound);
                var normalActiveBefore = AudioCount(ActiveSound);
                Assert.That(system.TryActivate(normal, user, predicted: false, showPopup: false), Is.True);
                var normalActive = SEntMan.GetComponent<ItemToggleActiveSoundComponent>(normal);
                Assert.Multiple(() =>
                {
                    Assert.That(AudioCount(ActivateSound), Is.EqualTo(normalActivateBefore + 1),
                        "the normal control retains its upstream activation one-shot");
                    Assert.That(AudioCount(ActiveSound), Is.EqualTo(normalActiveBefore + 1));
                    Assert.That(normalActive.PlayingStream, Is.Not.Null);
                    Assert.That(SEntMan.GetComponent<ItemToggleMergeProbeComponent>(normal).Toggles,
                        Is.EqualTo(new[] { true }));
                });

                var normalDeactivateBefore = AudioCount(DeactivateSound);
                Assert.That(system.TryDeactivate(normal, user, predicted: false, showPopup: false), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(AudioCount(DeactivateSound), Is.EqualTo(normalDeactivateBefore + 1),
                        "the normal control retains its upstream deactivation one-shot");
                    Assert.That(normalActive.PlayingStream, Is.Null);
                    Assert.That(SEntMan.GetComponent<ItemToggleMergeProbeComponent>(normal).Toggles,
                        Is.EqualTo(new[] { true, false }));
                });

                AssertPreviewToggle(system, previewItem, user,
                    "a preview-marked item suppresses both one-shots and its active loop");
                AssertPreviewToggle(system, previewUserItem, previewUser,
                    "a preview-marked user suppresses sounds for an ordinary item");
            }
            finally
            {
                SEntMan.DeleteEntity(previewUserItem);
                SEntMan.DeleteEntity(previewItem);
                SEntMan.DeleteEntity(normal);
                SEntMan.DeleteEntity(previewUser);
                SEntMan.DeleteEntity(user);
            }
        });
    }

    [Test]
    public async Task PreviewReplayClearsAnExistingLoopOutsideFirstPrediction()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid item = default;
        EntityUid user = default;
        NetEntity itemNet = default;
        NetEntity userNet = default;
        EntityUid clientStream = default;

        try
        {
            await Server.WaitPost(() =>
            {
                item = SEntMan.SpawnEntity("ItemToggleMergeAudio", map.GridCoords);
                user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                itemNet = SEntMan.GetNetEntity(item);
                userNet = SEntMan.GetNetEntity(user);
                Server.PlayerMan.SetAttachedEntity(session, user);
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientItem = CEntMan.GetEntity(itemNet);
                var clientUser = CEntMan.GetEntity(userNet);
                var system = Client.System<ItemToggleSystem>();
                Assert.That(system.TryActivate(clientItem, clientUser, predicted: true, showPopup: false), Is.True);
                var active = CEntMan.GetComponent<ItemToggleActiveSoundComponent>(clientItem);
                Assert.That(active.PlayingStream, Is.Not.Null,
                    "the ordinary first-prediction control must establish a looping stream");
                clientStream = active.PlayingStream!.Value;
                Assert.That(CEntMan.EntityExists(clientStream), Is.True);

                CEntMan.EnsureComponent<LobbyPreviewEntityComponent>(clientItem);
                CGameTiming.StartPastPrediction();
                try
                {
                    var replay = new ItemToggledEvent(Predicted: true, Activated: true, User: clientUser);
                    CEntMan.EventBus.RaiseLocalEvent(clientItem, ref replay);
                }
                finally
                {
                    CGameTiming.EndPastPrediction();
                }

                Assert.That(active.PlayingStream, Is.Null,
                    "the preview branch must clear a stale loop before the first-prediction replay guard");
                Assert.That(CEntMan.IsQueuedForDeletion(clientStream) || CEntMan.Deleted(clientStream), Is.True,
                    "past-prediction preview cleanup must terminate the actual client-side loop entity");
            });
            await Pair.RunTicksSync(1);

            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.Deleted(clientStream), Is.True,
                    "the terminated client loop must be deleted on the cleanup tick");
            });
        }
        finally
        {
            await Client.WaitPost(() =>
            {
                if (clientStream.Valid && CEntMan.EntityExists(clientStream))
                    CEntMan.DeleteEntity(clientStream);
            });
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
            });
            // GameTest owns server entity cleanup. Deleting the attached mob and its children in one replicated tick
            // can mutate the client's live transform-child collection while the parent deletion is being processed.
        }
    }

    private void AssertPreviewToggle(
        ItemToggleSystem system,
        EntityUid item,
        EntityUid user,
        string message)
    {
        var activateBefore = AudioCount(ActivateSound);
        var deactivateBefore = AudioCount(DeactivateSound);
        var activeBefore = AudioCount(ActiveSound);

        Assert.That(system.TryActivate(item, user, predicted: false, showPopup: false), Is.True);
        var component = SEntMan.GetComponent<ItemToggleComponent>(item);
        var active = SEntMan.GetComponent<ItemToggleActiveSoundComponent>(item);
        Assert.Multiple(() =>
        {
            Assert.That(component.Activated, Is.True);
            Assert.That(active.PlayingStream, Is.Null, message);
            Assert.That(AudioCount(ActivateSound), Is.EqualTo(activateBefore), message);
            Assert.That(AudioCount(ActiveSound), Is.EqualTo(activeBefore), message);
        });

        Assert.That(system.TryDeactivate(item, user, predicted: false, showPopup: false), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(component.Activated, Is.False);
            Assert.That(active.PlayingStream, Is.Null);
            Assert.That(AudioCount(DeactivateSound), Is.EqualTo(deactivateBefore), message);
            Assert.That(SEntMan.GetComponent<ItemToggleMergeProbeComponent>(item).Toggles,
                Is.EqualTo(new[] { true, false }),
                "preview suppression must not suppress state changes or ItemToggledEvent");
        });
    }

    private int AudioCount(string path)
    {
        return SEntMan.EntityQuery<AudioComponent>().Count(component => component.FileName == path);
    }
}

[RegisterComponent]
public sealed partial class ItemToggleMergeProbeComponent : Component
{
    public readonly List<bool> Toggles = new();
}

public sealed class ItemToggleMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ItemToggleMergeProbeComponent, ItemToggledEvent>(OnToggled);
    }

    private static void OnToggled(Entity<ItemToggleMergeProbeComponent> entity, ref ItemToggledEvent args)
    {
        entity.Comp.Toggles.Add(args.Activated);
    }
}

#pragma warning restore RA0002
