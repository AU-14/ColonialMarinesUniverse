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
