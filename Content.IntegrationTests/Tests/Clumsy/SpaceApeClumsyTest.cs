#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Events;
using Content.Shared.Clumsy;
using Content.Shared.Clumsy.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Clumsy;

[TestFixture]
[TestOf(typeof(ClumsyStatusEffectSystem))]
public sealed class SpaceApeClumsyTest : GameTest
{
    private static readonly EntProtoId ApePrototype = "CMUMobApe";
    private static readonly EntProtoId ClumsyStatus = "StatusEffectClumsyMonkey";

    [Test]
    public async Task ApeLoadsWithPermanentClumsyStatusAndRelaysBehavior()
    {
        var prototype = SProtoMan.Index<EntityPrototype>(ApePrototype);
        Assert.Multiple(() =>
        {
            Assert.That(prototype.Components.ContainsKey("Clumsy"), Is.False,
                "SpaceApe must not reference the removed legacy Clumsy component");
            Assert.That(prototype.TryComp<PermanentStatusEffectsComponent>(
                out var permanentPrototype,
                SEntMan.ComponentFactory), Is.True);
            Assert.That(permanentPrototype!.StatusEffects, Is.EquivalentTo(new[] { ClumsyStatus }));
        });

        var map = await Pair.CreateTestMap();
        EntityUid ape = default;
        EntityUid injector = default;
        EntityUid intendedTarget = default;

        await Server.WaitPost(() =>
        {
            ape = SEntMan.SpawnEntity(ApePrototype, map.GridCoords);
            injector = SEntMan.SpawnEntity(null, map.GridCoords);
            intendedTarget = SEntMan.SpawnEntity(null, map.GridCoords);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var permanent = SEntMan.GetComponent<PermanentStatusEffectsComponent>(ape);
            var statuses = SEntMan.System<StatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(permanent.StatusEffects, Is.EquivalentTo(new[] { ClumsyStatus }));
                Assert.That(statuses.HasStatusEffect(ape, ClumsyStatus), Is.True,
                    "MapInit must apply the configured clumsy effect to the ape");
                Assert.That(statuses.TryGetStatusEffect(ape, ClumsyStatus, out var effect), Is.True);
                Assert.That(effect, Is.Not.Null);

                var status = SEntMan.GetComponent<StatusEffectComponent>(effect!.Value);
                Assert.That(status.AppliedTo, Is.EqualTo(ape));
                Assert.That(status.EndEffectTime, Is.Null, "the ape's clumsy effect must not expire");
                Assert.That(SEntMan.HasComponent<ClumsyCatchStatusEffectComponent>(effect.Value), Is.True);
                Assert.That(SEntMan.HasComponent<ClumsyDefibStatusEffectComponent>(effect.Value), Is.True);
                Assert.That(SEntMan.HasComponent<ClumsyGunStatusEffectComponent>(effect.Value), Is.True);
                Assert.That(SEntMan.HasComponent<ClumsyInjectorStatusEffectComponent>(effect.Value), Is.True);
                Assert.That(SEntMan.HasComponent<ClumsyVaultStatusEffectComponent>(effect.Value), Is.True);
            });
        });

        await Server.WaitPost(() =>
        {
            var statuses = SEntMan.System<StatusEffectsSystem>();
            Assert.That(statuses.TryGetStatusEffect(ape, ClumsyStatus, out var effect), Is.True);
            var injectorClumsy = SEntMan.GetComponent<ClumsyInjectorStatusEffectComponent>(effect!.Value);
            injectorClumsy.ClumsyChance = 1;
            SEntMan.Dirty(effect.Value, injectorClumsy);

            var attempt = new SelfBeforeInjectEvent(ape, injector, intendedTarget);
            SEntMan.EventBus.RaiseLocalEvent(ape, attempt);
            Assert.That(attempt.TargetGettingInjected, Is.EqualTo(ape),
                "the permanent status must relay clumsy injector behavior through the ape");
        });
    }
}

#pragma warning restore RA0002
