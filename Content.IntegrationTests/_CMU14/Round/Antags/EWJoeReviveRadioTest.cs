using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.CMU14.Medical.Defibrillator;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Damage;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radio.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// D3 as revised: the Early Warning Joe comes back only from a defibrillator zap, on a longer charge; chemical
/// revival stays refused. Its bare hands hit harder than a plain Working Joe's, and it holds the machine
/// channel from selection, before any thrall exists.
/// </summary>
[TestFixture]
public sealed class EWJoeReviveRadioTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string SubversionKey = "CMUCLFSynthSubversionKey";
    private const string ResetKey = "RMCSynthResetKeySMART";

    [Test]
    public async Task JoeIsDefibOnlyHitsHarderAndHoldsTheMachineChannel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var rule = entities.System<EWJoeRuleSystem>();
            var defib = entities.System<RMCDefibrillatorSystem>();

            var plain = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var victim = entities.SpawnEntity(Human, map.GridCoords);

            // longer charge on the Joe only
            var baseDelay = TimeSpan.FromSeconds(4);
            var joeDelay = new CMUDefibZapDelayEvent(victim, victim, baseDelay);
            entities.EventBus.RaiseLocalEvent(joe, ref joeDelay);
            var plainDelay = new CMUDefibZapDelayEvent(victim, victim, baseDelay);
            entities.EventBus.RaiseLocalEvent(plain, ref plainDelay);

            // on the dead bodies the zap's revival attempt is open; chemical pacing is refused
            var mobState = entities.System<MobStateSystem>();
            mobState.ChangeMobState(plain, MobState.Dead);
            mobState.ChangeMobState(joe, MobState.Dead);
            var zapAttempt = defib.PrepareRevival(joe).Cancelled;
            var plainZapAttempt = defib.PrepareRevival(plain).Cancelled;
            var chemAttempt = defib.PrepareRevival(joe, allowBeatingHeart: true).Cancelled;

            // a reset key reaches the Joe; a subversion key is refused before it zaps
            var resetKey = entities.SpawnEntity(ResetKey, map.GridCoords);
            var resetZap = new TargetBeforeDefibrillatorZapsEvent(victim, resetKey, joe);
            entities.EventBus.RaiseLocalEvent(joe, resetZap);
            var subverter = entities.SpawnEntity(SubversionKey, map.GridCoords);
            var subvertZap = new TargetBeforeDefibrillatorZapsEvent(victim, subverter, joe);
            entities.EventBus.RaiseLocalEvent(joe, subvertZap);
            mobState.ChangeMobState(joe, MobState.Alive);

            // bare-handed hit: the Joe is its own weapon
            var baseDamage = new DamageSpecifier();
            baseDamage.DamageDict["Blunt"] = 60;
            var hit = new MeleeHitEvent(new List<EntityUid> { victim }, joe, joe, baseDamage, null);
            entities.EventBus.RaiseLocalEvent(joe, hit);

            rule.ApplyLevel((joe, comp), 0);

            Assert.Multiple(() =>
            {
                Assert.That(joeDelay.Delay, Is.EqualTo(baseDelay + comp.DefibDoAfterBonus), "the Joe's charge is longer");
                Assert.That(comp.DefibDoAfterBonus, Is.GreaterThan(TimeSpan.Zero));
                Assert.That(plainDelay.Delay, Is.EqualTo(baseDelay), "a plain Working Joe's is not");
                Assert.That(plainZapAttempt, Is.False, "precondition: a dead Working Joe's zap attempt is open");
                Assert.That(zapAttempt, Is.False, "a defibrillator zap is not refused for being the Joe");
                Assert.That(resetZap.Cancelled, Is.False, "a reset key may zap the Joe");
                Assert.That(subvertZap.Cancelled, Is.True, "a subversion key may not");
                Assert.That(chemAttempt, Is.True, "chemical revival is refused");
                Assert.That(hit.BonusDamage.DamageDict.GetValueOrDefault("Blunt").Float(), Is.EqualTo(comp.UnarmedBonusDamage).Within(0.01f),
                    "bare hands deal the Joe's extra blunt");
                Assert.That(comp.UnarmedBonusDamage, Is.GreaterThan(0f));
                Assert.That(comp.SlamDamage, Is.LessThan(40), "slam lowered from 40");
                Assert.That(comp.GrantedMachineRadio, "the machine channel is the Joe's from level 0");
                Assert.That(entities.GetComponent<ActiveRadioComponent>(joe).Channels, Does.Contain(comp.ThrallRadioChannel!.Value));
                Assert.That(entities.GetComponent<IntrinsicRadioTransmitterComponent>(joe).Channels, Does.Contain(comp.ThrallRadioChannel!.Value));
            });
        });

        await pair.CleanReturnAsync();
    }
}
