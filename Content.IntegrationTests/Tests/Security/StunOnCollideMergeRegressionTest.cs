using Content.IntegrationTests.Fixtures;
using Content.Server.Stunnable.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Security;

[TestFixture]
[TestOf(typeof(StunOnCollideComponent))]
public sealed class StunOnCollideMergeRegressionTest : GameTest
{
    private static readonly EntProtoId TaserProjectile = "StunMergeTaserProjectile";
    private static readonly EntProtoId UntaggedProjectile = "StunMergeUntaggedProjectile";
    private static readonly EntProtoId BlacklistedTarget = "StunMergeBlacklistedTarget";

    [TestPrototypes]
    private const string Prototypes = @"
- type: Tag
  id: StunMergeBlacklist

- type: entity
  id: StunMergeTaserProjectile
  components:
  - type: StunOnCollide
    stunAmount: 10
    knockdownAmount: 10
    slowdownAmount: 10
    walkSpeedModifier: 0.5
    sprintSpeedModifier: 0.5
    refresh: false
    blacklist:
      tags:
      - StunMergeBlacklist
  - type: ThrownItem
  - type: Tag
    tags:
    - Taser

- type: entity
  id: StunMergeUntaggedProjectile
  components:
  - type: StunOnCollide
    stunAmount: 10
    knockdownAmount: 10
    slowdownAmount: 10
    walkSpeedModifier: 0.5
    sprintSpeedModifier: 0.5
    refresh: false
    blacklist:
      tags:
      - StunMergeBlacklist
  - type: ThrownItem

- type: entity
  id: StunMergeBlacklistedTarget
  parent: CMMobHuman
  components:
  - type: Tag
    tags:
    - StunMergeBlacklist
";

    [Test]
    public async Task TaserOnlySkipsYautjaAfterBlacklistAndBeforeAllEffects()
    {
        var map = await Pair.CreateTestMap();
        EntityUid taser = default;
        EntityUid untagged = default;
        EntityUid yautja = default;
        EntityUid normal = default;
        EntityUid yautjaUntagged = default;
        EntityUid blacklisted = default;

        await Server.WaitPost(() =>
        {
            taser = SEntMan.SpawnEntity(TaserProjectile, map.GridCoords);
            untagged = SEntMan.SpawnEntity(UntaggedProjectile, map.GridCoords);
            yautja = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            normal = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            yautjaUntagged = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            blacklisted = SEntMan.SpawnEntity(BlacklistedTarget, map.GridCoords);

            SEntMan.EnsureComponent<YautjaComponent>(yautja);
            SEntMan.EnsureComponent<YautjaComponent>(yautjaUntagged);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            RaiseThrownHit(taser, yautja);
            RaiseThrownHit(taser, normal);
            RaiseThrownHit(untagged, yautjaUntagged);
            RaiseThrownHit(taser, blacklisted);

            AssertStunState(yautja, false,
                "a Taser-tagged projectile must not apply any stun effect to a Yautja");
            AssertStunState(normal, true,
                "the same Taser-tagged projectile must retain the upstream stun flow for a normal target");
            AssertStunState(yautjaUntagged, true,
                "the Yautja immunity must be limited to projectiles carrying the Taser tag");
            AssertStunState(blacklisted, false,
                "the upstream blacklist must still skip targets before the fork immunity check and all effects");
        });
    }

    private void RaiseThrownHit(EntityUid projectile, EntityUid target)
    {
        var thrown = SEntMan.GetComponent<ThrownItemComponent>(projectile);
        var ev = new ThrowDoHitEvent(projectile, target, thrown);
        SEntMan.EventBus.RaiseLocalEvent(projectile, ref ev);
    }

    private void AssertStunState(EntityUid target, bool expected, string message)
    {
        var statuses = SEntMan.System<StatusEffectsSystem>();

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.EqualTo(expected), message);
            Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.EqualTo(expected), message);
            Assert.That(statuses.HasStatusEffect(target, MovementModStatusSystem.TaserSlowdown),
                Is.EqualTo(expected), message);
        });
    }
}
