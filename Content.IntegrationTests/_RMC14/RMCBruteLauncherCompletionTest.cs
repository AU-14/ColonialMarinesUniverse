#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Weapons.Ranged.Brute;
using Content.Shared.DoAfter;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
[TestOf(typeof(RMCBruteLauncherComponent))]
public sealed class RMCBruteLauncherCompletionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: RMCWeaponLauncherM6HBrute
  id: RMCBruteLauncherCompletionTestGun
  components:
  - type: RemoveComponents
    components:
    - type: Wieldable
    - type: GunRequiresWield
    - type: GunRequiresSkills
      skills: {}
    - type: WieldDelay
    - type: RMCBackblastOnShoot
  - type: RMCBruteLauncherCompletionProbe

- type: entity
  id: RMCBruteLauncherCompletionTestTarget
  components:
  - type: Tag
    tags:
    - Structure
  - type: RMCWallExplosionDeletable
";

    public override PoolSettings PoolSettings => new()
    {
        Connected = false,
    };

    [Test]
    public async Task CompletionUsesStoredCoordinatesWithoutTargetIdentityAndAlwaysClearsLock()
    {
        var map = await Pair.CreateTestMap();
        var entities = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                _ = Server.System<RMCBruteLauncherCompletionProbeSystem>();
                var user = Spawn("CMMobHuman", map.GridCoords, entities);
                var target = Spawn(
                    "RMCBruteLauncherCompletionTestTarget",
                    map.GridCoords.Offset(new Vector2(2, 0)),
                    entities);
                var storedCoordinates = map.GridCoords.Offset(new Vector2(4.25f, -1.5f));

                var success = Spawn("RMCBruteLauncherCompletionTestGun", map.GridCoords, entities);
                CompleteLock(success, user, target, storedCoordinates);
                AssertCompletion(success, storedCoordinates, expectedShots: 1);

                var failed = Spawn("RMCBruteLauncherCompletionTestGun", map.GridCoords, entities);
                var failedProbe = SEntMan.GetComponent<RMCBruteLauncherCompletionProbeComponent>(failed);
                failedProbe.CancelAttempts = true;
                CompleteLock(failed, user, target, storedCoordinates);
                AssertCompletion(failed, storedCoordinates, expectedShots: 0);
            });

            // Let the launcher's final 0.7 second wave row finish before deleting the map fixture.
            // Spawned ammunition is engine-owned and cleans itself up after priming.
            await Pair.RunTicksSync(24);
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var entity in entities.Distinct())
                {
                    if (SEntMan.EntityExists(entity))
                        SEntMan.DeleteEntity(entity);
                }
            });
        }
    }

    private EntityUid Spawn(string prototype, EntityCoordinates coordinates, ICollection<EntityUid> entities)
    {
        var entity = SEntMan.SpawnEntity(prototype, coordinates);
        entities.Add(entity);
        return entity;
    }

    private void CompleteLock(
        EntityUid launcher,
        EntityUid user,
        EntityUid target,
        EntityCoordinates storedCoordinates)
    {
        var component = SEntMan.GetComponent<RMCBruteLauncherComponent>(launcher);
        component.LockId++;
        component.LockTarget = target;

        var completion = new RMCBruteLockOnDoAfterEvent(
            component.LockId,
            SEntMan.GetNetEntity(target),
            SEntMan.GetNetCoordinates(storedCoordinates));
        var args = new DoAfterArgs(
            SEntMan,
            user,
            TimeSpan.Zero,
            completion,
            launcher,
            target,
            launcher);
        completion.DoAfter = new DoAfter(0, args, TimeSpan.Zero);

        SEntMan.EventBus.RaiseLocalEvent(launcher, completion);
        Assert.Multiple(() =>
        {
            Assert.That(completion.Handled, Is.True);
            Assert.That(component.LockTarget, Is.Null);
            Assert.That(component.LockComplete, Is.False,
                "the completion guard must be cleared after either firing result");
        });
    }

    private void AssertCompletion(EntityUid launcher, EntityCoordinates expectedCoordinates, int expectedShots)
    {
        var transform = Server.System<SharedTransformSystem>();
        var probe = SEntMan.GetComponent<RMCBruteLauncherCompletionProbeComponent>(launcher);
        Assert.Multiple(() =>
        {
            Assert.That(probe.AttemptCoordinates, Has.Count.EqualTo(1));
            Assert.That(probe.AttemptTargets, Is.EqualTo(new EntityUid?[] { null }),
                "the lock target validates completion but must not become shot identity");
            Assert.That(probe.LockCompleteDuringAttempts, Is.EqualTo(new[] { true }));
            Assert.That(probe.ShotCoordinates, Has.Count.EqualTo(expectedShots));
        });

        AssertCoordinates(transform, probe.AttemptCoordinates.Single(), expectedCoordinates);
        foreach (var coordinates in probe.ShotCoordinates)
            AssertCoordinates(transform, coordinates, expectedCoordinates);
    }

    private static void AssertCoordinates(
        SharedTransformSystem transform,
        EntityCoordinates actual,
        EntityCoordinates expected)
    {
        var actualMap = transform.ToMapCoordinates(actual);
        var expectedMap = transform.ToMapCoordinates(expected);
        Assert.Multiple(() =>
        {
            Assert.That(actualMap.MapId, Is.EqualTo(expectedMap.MapId));
            Assert.That(actualMap.Position.X, Is.EqualTo(expectedMap.Position.X).Within(0.0001f));
            Assert.That(actualMap.Position.Y, Is.EqualTo(expectedMap.Position.Y).Within(0.0001f));
        });
    }
}

[RegisterComponent]
public sealed partial class RMCBruteLauncherCompletionProbeComponent : Component
{
    public readonly List<EntityCoordinates> AttemptCoordinates = [];
    public readonly List<EntityUid?> AttemptTargets = [];
    public readonly List<bool> LockCompleteDuringAttempts = [];
    public readonly List<EntityCoordinates> ShotCoordinates = [];
    public bool CancelAttempts;
}

public sealed class RMCBruteLauncherCompletionProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCBruteLauncherCompletionProbeComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<RMCBruteLauncherCompletionProbeComponent, GunShotEvent>(OnGunShot);
    }

    private void OnAttemptShoot(
        Entity<RMCBruteLauncherCompletionProbeComponent> entity,
        ref AttemptShootEvent args)
    {
        Assert.That(args.ToCoordinates, Is.Not.Null);
        entity.Comp.AttemptCoordinates.Add(args.ToCoordinates!.Value);
        entity.Comp.AttemptTargets.Add(Comp<GunComponent>(entity).Target);
        entity.Comp.LockCompleteDuringAttempts.Add(Comp<RMCBruteLauncherComponent>(entity).LockComplete);
        args.Cancelled |= entity.Comp.CancelAttempts;
    }

    private static void OnGunShot(
        Entity<RMCBruteLauncherCompletionProbeComponent> entity,
        ref GunShotEvent args)
    {
        entity.Comp.ShotCoordinates.Add(args.ToCoordinates);
    }
}

#pragma warning restore RA0002
