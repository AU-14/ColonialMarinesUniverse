#pragma warning disable RA0002
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

            // The claw swing marks itself; the +50% itself lands per target in CMArmorSystem through the
            // same helper the xeno abilities use, so a swing catching several mobs only boosts the
            // xeno-sized ones.
            var clawSwing = new MeleeHitEvent(new List<EntityUid> { xeno }, xeno, xeno,
                new DamageSpecifier(slash, FixedPoint2.New(100)), null);
            entMan.EventBus.RaiseLocalEvent(xeno, clawSwing);

            Assert.That(clawSwing.Impact.Context.HasFlag(DamageImpactContext.XenoClaw), Is.True,
                "A xeno claw swing must be marked for the per-target XVX slash multiplier.");
        });

        await pair.CleanReturnAsync();
    }
}
