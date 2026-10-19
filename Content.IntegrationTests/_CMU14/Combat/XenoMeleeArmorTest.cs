using System.Collections.Generic;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee.Events;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Combat;

[TestFixture]
public sealed class XenoMeleeArmorTest
{
    [Test]
    public async Task MeleeAgainstXenosMatchesCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var damageable = entMan.System<DamageableSystem>();

            var slash = prototypes.Index<DamageTypePrototype>("Slash");

            var nonXeno = entMan.SpawnEntity("CMUYautjaPlasmaCarbine", MapCoordinates.Nullspace);
            var xeno = entMan.SpawnEntity("RMCXenoRavagerHedgehog", MapCoordinates.Nullspace);
            var bayonet = entMan.SpawnEntity("RMCM5Bayonet", MapCoordinates.Nullspace);

            // One fresh xeno per case, all at the same spot, so the directional armour contribution is
            // identical everywhere and only the rule under test varies.
            double Hit(FixedPoint2 amount, EntityUid? origin = null, EntityUid? tool = null,
                DamageImpact impact = default, int ap = 0)
            {
                var victim = entMan.SpawnEntity("RMCXenoRavagerHedgehog", MapCoordinates.Nullspace);
                damageable.TryChangeDamage(victim, new DamageSpecifier(slash, amount), out var result,
                    origin: origin ?? nonXeno, tool: tool, impact: impact, armorPiercing: ap);
                return (double) result.GetTotal().Float();
            }

            // Damage multipliers with the armour nullified by a huge explicit penetration, so only the
            // multipliers show: baseline 100 -> xeno-melee config x1.5 -> per-target XVX claw x1.5 = x2.25.
            var raw = Hit(100, impact: default, ap: 100);
            var rawBody = Hit(100, origin: xeno, tool: xeno, impact: DamageImpact.MeleeSlash, ap: 100);
            var rawClaw = Hit(100, origin: xeno, tool: xeno,
                impact: DamageImpact.MeleeSlash with { Context = DamageImpactContext.XenoClaw }, ap: 100);

            Assert.That(rawBody / raw, Is.EqualTo(1.5).Within(0.02),
                "Any melee hit on a xeno gets the xeno/melee config x1.5, whoever dealt it.");
            Assert.That(rawClaw / raw, Is.EqualTo(2.25).Within(0.03),
                "Xeno melee on a xeno must total x2.25: XVX x1.5 on the swing times the config x1.5.");

            // CM-SS13 runs every xeno ability through apply_armoured_damage(..., ARMOR_MELEE) too, so an
            // ability that carries no Melee-delivery impact still gets the config x1.5. A xeno projectile
            // takes the ranged config instead and must not.
            var xenoAbility = Hit(100, origin: xeno, tool: xeno, impact: default, ap: 100);
            var xenoProjectile = Hit(100, origin: xeno, tool: bayonet, impact: default, ap: 100);

            Assert.That(xenoAbility / raw, Is.EqualTo(1.5).Within(0.02),
                "A xeno ability on a xeno must get the xeno/melee config x1.5, exactly like the claw swing.");
            Assert.That(xenoProjectile / raw, Is.EqualTo(1.0).Within(0.02),
                "A xeno projectile on a xeno is ranged-type and must not get the melee config.");

            // Heals ride the same event, and both Fury (XenoFurySystem) and Soak (XenoSoakSystem) heal with
            // origin: xeno, tool: xeno. Negative damage must never be scaled.
            double Heal(EntityUid origin)
            {
                var wounded = entMan.SpawnEntity("RMCXenoRavagerHedgehog", MapCoordinates.Nullspace);
                damageable.TryChangeDamage(wounded, new DamageSpecifier(slash, FixedPoint2.New(200)), out _);
                damageable.TryChangeDamage(wounded, new DamageSpecifier(slash, FixedPoint2.New(-100)),
                    out var healed, origin: origin, tool: origin, armorPiercing: 100);
                return (double) healed.GetTotal().Float();
            }

            Assert.That(Heal(xeno), Is.EqualTo(Heal(nonXeno)).Within(0.5),
                "A xeno heal must come back unscaled: Fury and Soak heal with origin: xeno, tool: xeno.");

            // Armour handling with the real armour in play.
            var baseline = Hit(100, impact: default);
            var marineBody = Hit(100, origin: nonXeno, tool: nonXeno, impact: DamageImpact.MeleeSlash);
            var xenoBody = Hit(100, origin: xeno, tool: xeno, impact: DamageImpact.MeleeSlash);
            var itemHit = Hit(100, tool: bayonet, impact: DamageImpact.MeleeSlash);
            var marineBodyPierce = Hit(100, origin: nonXeno, tool: nonXeno, impact: DamageImpact.MeleeSlash, ap: 20);

            Assert.That(baseline, Is.GreaterThan(0), "Sanity: the baseline hit has to land some damage.");
            Assert.That(marineBody / baseline, Is.EqualTo(1.5).Within(0.02),
                "A non-xeno body hit on a xeno gets the config x1.5 and nothing else.");

            // The flat 20 penetration is the item-attack path only.
            Assert.That(itemHit, Is.EqualTo(marineBodyPierce).Within(0.5),
                "A melee hit with a weapon must subtract the same 20 armour as an explicit 20 penetration.");

            // XVX_ARMOR_EFFECTIVEMULT: a xeno's own body attack ignores three quarters of the armour.
            Assert.That(xenoBody, Is.GreaterThan(marineBody),
                "A xeno body attack must ignore three quarters of the victim's armour, so it lands more.");

            // The claw swing marks itself; the +50% itself lands per target in CMArmorSystem, gated on
            // XenoComponent, so a swing catching several mobs only boosts the xeno victims.
            var clawSwing = new MeleeHitEvent(new List<EntityUid> { xeno }, xeno, xeno,
                new DamageSpecifier(slash, FixedPoint2.New(100)), null);
            entMan.EventBus.RaiseLocalEvent(xeno, clawSwing);

            Assert.That(clawSwing.Impact.Context.HasFlag(DamageImpactContext.XenoClaw), Is.True,
                "A xeno claw swing must be marked for the per-target XVX slash multiplier.");
        });

        await pair.CleanReturnAsync();
    }
}
