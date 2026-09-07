using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;

namespace Content.IntegrationTests.Tests.Damageable;

[TestFixture]
[TestOf(typeof(SharedStaminaSystem))]
public sealed class StaminaTaserImmunityMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: StaminaMergeTarget
  components:
  - type: Stamina
    decay: 0
    baseCritThreshold: 100

- type: entity
  id: StaminaMergeTaserHit
  components:
  - type: StaminaDamageOnHit
    damage: 10
  - type: Tag
    tags: [ Taser ]

- type: entity
  id: StaminaMergePlainHit
  components:
  - type: StaminaDamageOnHit
    damage: 10

- type: entity
  id: StaminaMergeTaserCollide
  components:
  - type: StaminaDamageOnCollide
    damage: 10
  - type: ThrownItem
  - type: Tag
    tags: [ Taser ]

- type: entity
  id: StaminaMergePlainCollide
  components:
  - type: StaminaDamageOnCollide
    damage: 10
  - type: ThrownItem
";

    [Test]
    public async Task TaserImmunityIsLimitedToYautjaAndBothVanillaHitPaths()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var taserHit = SEntMan.Spawn("StaminaMergeTaserHit");
            var plainHit = SEntMan.Spawn("StaminaMergePlainHit");
            var taserCollide = SEntMan.Spawn("StaminaMergeTaserCollide");
            var plainCollide = SEntMan.Spawn("StaminaMergePlainCollide");
            var hitImmune = SEntMan.Spawn("StaminaMergeTarget");
            var hitNormal = SEntMan.Spawn("StaminaMergeTarget");
            var hitPlain = SEntMan.Spawn("StaminaMergeTarget");
            var collideImmune = SEntMan.Spawn("StaminaMergeTarget");
            var collidePlain = SEntMan.Spawn("StaminaMergeTarget");
            try
            {
                SEntMan.EnsureComponent<YautjaComponent>(hitImmune);
                SEntMan.EnsureComponent<YautjaComponent>(hitPlain);
                SEntMan.EnsureComponent<YautjaComponent>(collideImmune);
                SEntMan.EnsureComponent<YautjaComponent>(collidePlain);

                RaiseMeleeHit(taserHit, hitImmune);
                RaiseMeleeHit(taserHit, hitNormal);
                RaiseMeleeHit(plainHit, hitPlain);
                RaiseThrownHit(taserCollide, collideImmune);
                RaiseThrownHit(plainCollide, collidePlain);

                AssertStamina(hitImmune, 0, false,
                    "Taser-tagged melee stamina damage is skipped for Yautja");
                AssertStamina(hitNormal, 10, true,
                    "the same Taser-tagged melee source retains the vanilla path for ordinary targets");
                AssertStamina(hitPlain, 10, true,
                    "untagged melee stamina damage still affects Yautja");
                AssertStamina(collideImmune, 0, false,
                    "Taser-tagged collide stamina damage is skipped for Yautja");
                AssertStamina(collidePlain, 10, true,
                    "untagged collide stamina damage still affects Yautja");
            }
            finally
            {
                foreach (var ent in new[]
                         {
                             taserHit, plainHit, taserCollide, plainCollide, hitImmune, hitNormal, hitPlain,
                             collideImmune, collidePlain,
                         })
                {
                    SEntMan.DeleteEntity(ent);
                }
            }
        });
    }

    private void RaiseMeleeHit(EntityUid weapon, EntityUid target)
    {
        var ev = new MeleeHitEvent([target], weapon, weapon, new DamageSpecifier(), null);
        SEntMan.EventBus.RaiseLocalEvent(weapon, ev);
    }

    private void RaiseThrownHit(EntityUid projectile, EntityUid target)
    {
        var thrown = SEntMan.GetComponent<ThrownItemComponent>(projectile);
        var ev = new ThrowDoHitEvent(projectile, target, thrown);
        SEntMan.EventBus.RaiseLocalEvent(projectile, ref ev);
    }

    private void AssertStamina(EntityUid target, float expectedDamage, bool active, string message)
    {
        var stamina = SEntMan.GetComponent<StaminaComponent>(target);
        var status = SEntMan.System<StatusEffectsSystem>();
        Assert.Multiple(() =>
        {
            Assert.That(stamina.StaminaDamage, Is.EqualTo(expectedDamage), message);
            Assert.That(SEntMan.HasComponent<ActiveStaminaComponent>(target), Is.EqualTo(active), message);
            Assert.That(status.HasStatusEffect(target, SharedStaminaSystem.StaminaLow), Is.EqualTo(active),
                "the upstream stamina status lifecycle must remain reachable on allowed hits");
        });
    }
}
