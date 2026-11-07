using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Slow;
using Content.Shared.Actions.Events;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Trauma;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.CombatMode;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

// CMU14 Test: Yautja frontline resilience contracts.
[TestFixture]
public sealed class YautjaFrontlineBalanceTest
{

    [Test]
    public async Task RegularHunterCanBeDisarmedAndDropsItemsWhenKnockedDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hands = entities.System<SharedHandsSystem>();
            var hunter = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            var attacker = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var weapon = entities.SpawnEntity("CMUYautjaClanSword", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(hunter, weapon), Is.True);

            var attempt = new DisarmAttemptEvent(hunter, attacker, weapon);
            entities.EventBus.RaiseLocalEvent(hunter, ref attempt);
            var disarmed = new DisarmedEvent(hunter, attacker, 1f);
            entities.EventBus.RaiseLocalEvent(hunter, ref disarmed);
            var knockedDown = entities.System<SharedStunSystem>()
                .TryKnockdown(hunter, TimeSpan.FromSeconds(2));

            // CM-SS13 (human_attackhand.dm disarm intent) has no Yautja exception, and only
            // knock_down_reduction 1.5 makes them harder to floor - no immunity, no drop protection.
            Assert.Multiple(() =>
            {
                Assert.That(attempt.Cancelled, Is.False);
                Assert.That(knockedDown, Is.True);
                Assert.That(hands.IsHolding(hunter, weapon), Is.False,
                    "the disarm now throws the held weapon instead of being ignored");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RegularHunterCanStillFractureAndBleed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hunter = entities.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetBodyPart(hunter,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                out var arm), Is.True);

            Assert.That(entities.System<SharedBoneSystem>().SeedFracture(arm, FractureSeverity.Simple), Is.True);
            Assert.That(entities.System<SharedBodyPartHealthSystem>().TryApplyPartDamage(
                hunter,
                arm,
                new DamageSpecifier { DamageDict = { ["Slash"] = 20 } },
                impact: DamageImpact.MeleeSlash,
                mechanism: CMUTraumaMechanism.Slash), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<FractureComponent>(arm).Severity,
                    Is.AtLeast(FractureSeverity.Simple));
                Assert.That(entities.TryGetComponent(arm, out BodyPartWoundComponent? wounds), Is.True);
                Assert.That(entities.System<CMUWoundLedgerSystem>()
                    .GetEntries(wounds!)
                    .Any(entry => entry.Wound.Bloodloss > 0f), Is.True);
                Assert.That(entities.GetComponent<BloodstreamComponent>(hunter).MaxBleedAmount, Is.GreaterThan(0f));
            });

            entities.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }
}
