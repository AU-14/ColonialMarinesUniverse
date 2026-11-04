using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.Doors.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkPresentationTest
{
    [Test]
    public async Task DirectionalButtonsKeepTheirMountingOffsets()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var transform = entities.System<SharedTransformSystem>();
            var checkedButtons = 0;
            foreach (var prototype in prototypes.EnumeratePrototypes<EntityPrototype>()
                         .Where(p => p.Parents?.Contains("CMUMohawkControl") == true))
            {
                var uid = entities.Spawn(prototype.ID);
                try
                {
                    var group = entities.GetComponent<MohawkControlComponent>(uid).Group;
                    if (group == MohawkControlGroup.Ramp)
                        continue;

                    var facing = Angle.FromDegrees(group == MohawkControlGroup.Port ? -90 : 90);
                    var mountingOffset = group == MohawkControlGroup.Hatch
                        ? new Vector2(0.5f, 5f / 32)
                        : Vector2.UnitY;
                    var sprite = entities.GetComponent<SpriteComponent>(uid);
                    Assert.That(sprite.NoRotation, Is.False);
                    Assert.That(sprite.DrawDepth, Is.GreaterThan((int) Content.Shared.DrawDepth.DrawDepth.Walls));
                    foreach (var shipDegrees in new[] { 0, 90, 180 })
                    {
                        var shipRotation = Angle.FromDegrees(shipDegrees);
                        transform.SetWorldRotation(uid, shipRotation + facing);
                        var actual = transform.GetWorldRotation(uid).RotateVec(sprite.Offset);
                        Assert.That(Vector2.Distance(actual, shipRotation.RotateVec(mountingOffset)), Is.LessThan(0.0001f),
                            $"{prototype.ID}: facing a button must not rotate it away from its wall mount.");
                    }
                    checkedButtons++;
                }
                finally
                {
                    entities.DeleteEntity(uid);
                }
            }
            Assert.That(checkedButtons, Is.EqualTo(12));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllMohawkDoorsAnimateOnClient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var doors = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(p => p.Parents?.Contains("CMUMohawkDoor") == true).ToArray();
            Assert.That(doors, Is.Not.Empty);
            var appearance = entities.System<AppearanceSystem>();
            var animations = entities.System<AnimationPlayerSystem>();

            foreach (var prototype in doors)
            {
                var uid = entities.Spawn(prototype.ID);
                try
                {
                    foreach (var (state, key) in new[]
                             {
                                 (DoorState.Opening, DoorComponent.OpenKey),
                                 (DoorState.Closing, DoorComponent.CloseKey),
                                 (DoorState.Denying, DoorComponent.DenyKey),
                             })
                    {
                        appearance.SetData(uid, DoorVisuals.State, state);
                        // Map-only server tests never invoke the client animation
                        // tracks that require all the airlock's sprite layers.
                        appearance.FrameUpdate(0f);
                        Assert.That(animations.HasRunningAnimation(uid, key), Is.True,
                            $"{prototype.ID} must play its {state} animation.");
                        animations.Stop(uid, null, key);
                    }
                }
                finally
                {
                    entities.DeleteEntity(uid);
                }
            }
        });
        await pair.CleanReturnAsync();
    }
}
