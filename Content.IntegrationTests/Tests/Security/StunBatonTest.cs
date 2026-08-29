#nullable enable
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Stamina;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Components;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Security;

[TestFixture]
public sealed class StunBatonTests : InteractionTest
{
    private static readonly EntProtoId StunBatonProtoId = "Stunbaton";
    private static readonly EntProtoId HumanProtoId = "MobHuman";

    private static readonly (EntProtoId Id, double RmcDamage, bool HasToggle)[] RmcBatons =
    [
        ("RMCWeaponTaser", 15, false),
        ("CMStunbaton", 30, true),
    ];

    // If you are rebalancing stun batons you will have to change this number.
    private const int NumberOfHitsToStun = 3;

    [SidedDependency(Side.Server)] private readonly SharedBatterySystem _battery = default!;
    [SidedDependency(Side.Server)] private readonly DamageableSystem _damageable = default!;

    [Test]
    public async Task RmcBatonsUseGenericToggleAndChargeContract()
    {
        await Server.WaitAssertion(() =>
        {
            foreach (var (id, rmcDamage, hasToggle) in RmcBatons)
            {
                var prototype = ProtoMan.Index<EntityPrototype>(id);
                Assert.Multiple(() =>
                {
                    Assert.That(prototype.TryGetComponent<ItemToggleExaminableStatusComponent>(out _, Factory), Is.True, id.Id);
                    Assert.That(prototype.TryGetComponent<ItemToggleRequiresChargeComponent>(out var toggleCharge, Factory), Is.True, id.Id);
                    Assert.That(toggleCharge!.RequiredCharge, Is.EqualTo(50), id.Id);
                    Assert.That(prototype.TryGetComponent<MeleeBatteryHitsLeftComponent>(out _, Factory), Is.True, id.Id);
                    Assert.That(prototype.TryGetComponent<StaminaDamageOnHitRequiresToggleComponent>(out _, Factory), Is.True, id.Id);
                    Assert.That(prototype.TryGetComponent<StaminaDamageOnHitRequiresChargeComponent>(out var hitCharge, Factory), Is.True, id.Id);
                    Assert.That(hitCharge!.RequiredCharge, Is.EqualTo(50), id.Id);
                    Assert.That(prototype.TryGetComponent<RMCStaminaDamageOnHitComponent>(out var rmcStamina, Factory), Is.True, id.Id);
                    Assert.That(rmcStamina!.Damage, Is.EqualTo(rmcDamage), id.Id);
                    Assert.That(prototype.TryGetComponent<ItemToggleComponent>(out _, Factory), Is.EqualTo(hasToggle), id.Id);
                });
            }
        });
    }

    [Test]
    public async Task RmcBatonUsesGenericComplexInteractionToggleGate()
    {
        await Server.WaitAssertion(() =>
        {
            var baton = SEntMan.SpawnEntity("CMStunbaton", MapData.GridCoords);
            var user = SEntMan.SpawnEntity(null, MapData.GridCoords);
            var toggle = SEntMan.GetComponent<ItemToggleComponent>(baton);

            Assert.Multiple(() =>
            {
                Assert.That(toggle.RequireComplexInteract, Is.True);
                Assert.That(ItemToggleSys.TryActivate(baton, user), Is.False,
                    "users without ComplexInteraction must not turn the baton on");
                Assert.That(toggle.Activated, Is.False);
            });

            SEntMan.EnsureComponent<ComplexInteractionComponent>(user);
            Assert.Multiple(() =>
            {
                Assert.That(ItemToggleSys.TryActivate(baton, user), Is.True);
                Assert.That(toggle.Activated, Is.True);
            });

            SEntMan.RemoveComponent<ComplexInteractionComponent>(user);
            Assert.Multiple(() =>
            {
                Assert.That(ItemToggleSys.TryDeactivate(baton, user), Is.False,
                    "users without ComplexInteraction must not turn the baton off");
                Assert.That(toggle.Activated, Is.True);
            });
        });
    }

    [Test]
    public async Task RmcBatonChargeIsSpentOnceOnlyForEligibleMeleeHits()
    {
        await Server.WaitAssertion(() =>
        {
            var baton = SEntMan.SpawnEntity("CMStunbaton", MapData.GridCoords);
            var taser = SEntMan.SpawnEntity("RMCWeaponTaser", MapData.GridCoords);
            var first = RmcStaminaTarget();
            var second = RmcStaminaTarget();
            var single = RmcStaminaTarget();
            var immune = RmcStaminaTarget();
            var cancelled = RmcStaminaTarget();
            var ineligible = SEntMan.SpawnEntity(null, MapData.GridCoords);
            SEntMan.EnsureComponent<YautjaComponent>(immune);

            Assert.That(ItemToggleSys.TryActivate(baton, SPlayer), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<MeleeBatteryHitsLeftComponent>(baton).HitPowerCost, Is.EqualTo(50));
                Assert.That(SEntMan.GetComponent<MeleeBatteryHitsLeftComponent>(taser).HitPowerCost, Is.EqualTo(50));
                Assert.That(_battery.GetCharge(baton), Is.EqualTo(500));
                Assert.That(_battery.GetCharge(taser), Is.EqualTo(500));
            });

            RaiseRmcMeleeHit(baton, [first], isHit: false);
            RaiseRmcMeleeHit(baton, [ineligible]);
            RaiseRmcMeleeHit(taser, [immune]);
            Assert.Multiple(() =>
            {
                Assert.That(_battery.GetCharge(baton), Is.EqualTo(500),
                    "misses and non-RMC targets must not consume charge");
                Assert.That(_battery.GetCharge(taser), Is.EqualTo(500),
                    "Taser-tagged melee must not consume charge against an immune Yautja");
                Assert.That(SEntMan.GetComponent<RMCStaminaComponent>(first).Current, Is.EqualTo(100));
                Assert.That(SEntMan.GetComponent<RMCStaminaComponent>(immune).Current, Is.EqualTo(100));
            });

            RaiseRmcMeleeHit(baton, [first, second]);
            Assert.Multiple(() =>
            {
                Assert.That(_battery.GetCharge(baton), Is.EqualTo(450),
                    "one multi-target melee event consumes one 50-unit charge");
                Assert.That(SEntMan.GetComponent<RMCStaminaComponent>(first).Current, Is.EqualTo(85));
                Assert.That(SEntMan.GetComponent<RMCStaminaComponent>(second).Current, Is.EqualTo(85));
            });

            RaiseRmcMeleeHit(baton, [single]);
            Assert.Multiple(() =>
            {
                Assert.That(_battery.GetCharge(baton), Is.EqualTo(400),
                    "one eligible single-target event consumes exactly one charge");
                Assert.That(SEntMan.GetComponent<RMCStaminaComponent>(single).Current, Is.EqualTo(70));
            });

            _battery.SetCharge(taser, 49);
            RaiseRmcMeleeHit(taser, [cancelled]);
            Assert.Multiple(() =>
            {
                Assert.That(_battery.GetCharge(taser), Is.EqualTo(49),
                    "the generic insufficient-charge attempt cancellation must not spend charge");
                Assert.That(SEntMan.GetComponent<RMCStaminaComponent>(cancelled).Current, Is.EqualTo(100));
            });
        });
    }

    private EntityUid RmcStaminaTarget()
    {
        var target = SEntMan.SpawnEntity(null, MapData.GridCoords);
        SEntMan.EnsureComponent<RMCStaminaComponent>(target);
        return target;
    }

    private void RaiseRmcMeleeHit(EntityUid weapon, List<EntityUid> targets, bool isHit = true)
    {
        var ev = new MeleeHitEvent(targets, SPlayer, weapon, new DamageSpecifier(), null)
        {
            IsHit = isHit,
        };
        SEntMan.EventBus.RaiseLocalEvent(weapon, ev);
    }

    [Test]
    [Description("Checks that an activated stun baton stuns the target")]
    public async Task StunBatonTest()
    {
        // Prevent the test mob from suffocating.
        await AddAtmosphere();

        // Spawn a stun baton in the player's hands and turn it on.
        var baton = await PlaceInHands(StunBatonProtoId, enableToggleable: true);
        var sBaton = ToServer(baton);
        var batonStaminaDamage = Comp<StaminaDamageOnHitComponent>(baton).Damage;
        var chargeUsePerHit = Comp<MeleeBatteryHitsLeftComponent>(baton).HitPowerCost;
        var batonIntialCharges = _battery.GetRemainingUses(sBaton, chargeUsePerHit);
        var batonMaxCharges = _battery.GetMaxUses(sBaton, chargeUsePerHit);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batonMaxCharges, Is.GreaterThan(0), "Stun baton had no charges.");
            Assert.That(batonIntialCharges, Is.EqualTo(batonMaxCharges), "Stun baton was not fully charged when spawned.");
        }

        // Spawn a target mob.
        await SpawnTarget(HumanProtoId);
        SEntMan.EnsureComponent<StaminaComponent>(STarget!.Value);
        var standingStateComp = Comp<StandingStateComponent>();
        var staminaComp = Comp<StaminaComponent>();
        Entity<DamageableComponent> mob = (STarget.Value, Comp<DamageableComponent>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(HasComp<KnockedDownComponent>(), Is.False, "Target mob spawned knocked down.");
            Assert.That(HasComp<StunnedComponent>(), Is.False, "Target mob spawned stunned.");
            Assert.That(standingStateComp.Standing, Is.True, "Target mob was not standing when spawned.");
            Assert.That(_damageable.GetPositiveDamage(mob).GetTotal(), Is.EqualTo(FixedPoint2.Zero), "Target mob spawned with damage.");
            Assert.That(staminaComp.StaminaDamage, Is.Zero, "Target mob spawned with stamina damage.");
        }

        // Melee attack.
        await SetCombatMode(true);
        await RunSeconds(2); // Weapon cooldown.
        await AttemptLightAttack();

        // Not stunned yet after the first hit.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_damageable.GetPositiveDamage(mob).GetTotal(), Is.EqualTo(FixedPoint2.Zero), "Activated stun baton caused damage.");
            Assert.That(staminaComp.StaminaDamage, Is.EqualTo(batonStaminaDamage), "Target mob did not take the correct amount of stamina damage.");
            Assert.That(_battery.GetRemainingUses(sBaton, chargeUsePerHit), Is.EqualTo(batonMaxCharges - 1), "Stun baton did not loose a charge when used.");
        }

        // Continue attacking, checking that the mob gets stunned when it's supposed to.
        for (var i = 0; i < NumberOfHitsToStun - 1; i++)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(HasComp<KnockedDownComponent>(), Is.False, "Target mob was knocked down before the expected number of stun baton hits.");
                Assert.That(HasComp<StunnedComponent>(), Is.False, "Target mob was stunned before the expected number of stun baton hits.");
                Assert.That(standingStateComp.Standing, Is.True, "Target mob was not standing before the expected number of stun baton hits.");
            }

            await RunSeconds(2); // Weapon cooldown.
            await AttemptLightAttack();
        }

        // Check all components to see if we are stunned now.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(HasComp<KnockedDownComponent>(), Is.True, "Target mob was not knocked down from the expected number of stun baton hits.");
            Assert.That(HasComp<StunnedComponent>(), Is.True, "Target mob was not stunned from the expected number of stun baton hits.");
            Assert.That(standingStateComp.Standing, Is.False, "Target mob was not downed from the expected number of stun baton hits.");
            Assert.That(_damageable.GetPositiveDamage(mob).GetTotal(), Is.EqualTo(FixedPoint2.Zero), "Activated stun baton caused damage.");
            Assert.That(_battery.GetRemainingUses(sBaton, chargeUsePerHit), Is.EqualTo(batonMaxCharges - NumberOfHitsToStun), "Stun baton did not loose the correct charge when stunning.");
        }
    }

    [Test]
    [Description("Checks that a deactivated stun baton does not stun the target")]
    public async Task HarmBatonTest()
    {
        // Prevent the test mob from suffocating.
        await AddAtmosphere();

        // Spawn a stun baton in the player's hands without turning it on.
        var baton = await PlaceInHands(StunBatonProtoId, enableToggleable: false);
        var sBaton = ToServer(baton);
        var chargeUsePerHit = Comp<MeleeBatteryHitsLeftComponent>(baton).HitPowerCost;
        var batonIntialCharges = _battery.GetRemainingUses(sBaton, chargeUsePerHit);
        var batonMaxCharges = _battery.GetMaxUses(sBaton, chargeUsePerHit);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batonMaxCharges, Is.GreaterThan(0), "Stun baton had no charges.");
            Assert.That(batonIntialCharges, Is.EqualTo(batonMaxCharges), "Stun baton was not fully charged when spawned.");
        }

        // Spawn a target mob.
        await SpawnTarget(HumanProtoId);
        SEntMan.EnsureComponent<StaminaComponent>(STarget!.Value);
        var standingStateComp = Comp<StandingStateComponent>();
        var staminaComp = Comp<StaminaComponent>();
        Entity<DamageableComponent> mob = (STarget.Value, Comp<DamageableComponent>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(HasComp<KnockedDownComponent>(), Is.False, "Target mob spawned knocked down.");
            Assert.That(HasComp<StunnedComponent>(), Is.False, "Target mob spawned stunned.");
            Assert.That(standingStateComp.Standing, Is.True, "Target mob was not standing when spawned.");
            Assert.That(_damageable.GetPositiveDamage(mob).GetTotal(), Is.EqualTo(FixedPoint2.Zero), "Target mob spawned with damage.");
            Assert.That(staminaComp.StaminaDamage, Is.Zero, "Target mob spawned with stamina damage.");
        }

        // Attack until the target would be stunned if the baton was activated.
        await SetCombatMode(true);
        for (var i = 0; i < NumberOfHitsToStun; i++)
        {
            await RunSeconds(2); // Weapon cooldown.
            await AttemptLightAttack();
        }

        // Check all components to see if we are stunned now.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(HasComp<KnockedDownComponent>(), Is.False, "Target mob was knocked down from harmbaton attacks.");
            Assert.That(HasComp<StunnedComponent>(), Is.False, "Target mob was stunned from harmbaton attacks.");
            Assert.That(standingStateComp.Standing, Is.True, "Target mob was downed from harmbaton attacks.");
            Assert.That(_damageable.GetPositiveDamage(mob).GetTotal(), Is.GreaterThan(FixedPoint2.Zero), "Deactivated stun baton did not cause damage.");
            Assert.That(_battery.GetRemainingUses(sBaton, chargeUsePerHit), Is.EqualTo(batonMaxCharges), "Stun baton lost charge while deactivated.");
        }
    }

    [Test]
    [Description("Checks that missing attack with a stun baton does not cost any charge")]
    public async Task StunBatonMissTest()
    {
        // Spawn a stun baton in the player's hands and turn it on.
        var baton = await PlaceInHands(StunBatonProtoId, enableToggleable: true);
        var sBaton = ToServer(baton);
        var batteryComp = Comp<BatteryComponent>(baton);
        var batonIntialCharge = _battery.GetCharge(sBaton);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batonIntialCharge, Is.GreaterThan(0), "Stun baton had no charge.");
            Assert.That(batonIntialCharge, Is.EqualTo(batteryComp.MaxCharge), "Stun baton was not fully charged when spawned.");
        }

        // Missing melee attack.
        await SetCombatMode(true);
        await RunSeconds(2); // Weapon cooldown.
        await AttemptLightAttackMiss();

        var batonNewCharge = _battery.GetCharge(sBaton);
        Assert.That(batonNewCharge, Is.EqualTo(batonIntialCharge), "Stun baton lost charge when missing an attack.");
    }
}
