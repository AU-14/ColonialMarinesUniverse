#pragma warning disable RA0002 // Set controlled temperatures and deadlines for treatment scenarios.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Medical.Treatment.Effects;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Temperature.Components;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
public sealed class OpporozidoneRevivalTimerTest : GameTest
{
    private static readonly ProtoId<ReagentPrototype> ReagentId = "Opporozidone";

    [TestCase(1f, 5f)]
    [TestCase(0.25f, 1.25f)]
    [TestCase(2.5f, 12.5f)]
    public async Task ExtendsActiveDeadlineForActualDose(float dose, float seconds)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var target = SpawnDeadPatient(map.GridCoords);
            var revivable = SEntMan.GetComponent<RMCRevivableComponent>(target);
            var deadline = revivable.UnrevivableAt;

            Assert.That(ApplyDose(target, dose), Is.True);
            Assert.That(revivable.UnrevivableAt, Is.EqualTo(deadline + TimeSpan.FromSeconds(seconds)),
                "dose comes from the amount metabolized, including fractional doses, rather than the generic effect scale");
        });
    }

    [TestCase(0f, false)]
    [TestCase(40f, false)]
    [TestCase(0f, true)]
    [TestCase(40f, true)]
    public async Task ExpiredCountdownCannotBeReversed(float secondsExpired, bool scanned)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var target = SpawnDeadPatient(map.GridCoords);
            var revivable = SEntMan.GetComponent<RMCRevivableComponent>(target);
            var unrevivable = Server.System<RMCUnrevivableSystem>();
            var deadline = SGameTiming.CurTime - TimeSpan.FromSeconds(secondsExpired);
            revivable.UnrevivableAt = deadline;
            if (scanned)
                unrevivable.Update(1.1f);

            Assert.That(unrevivable.IsUnrevivable(target), Is.EqualTo(scanned));
            ApplyDose(target, 100f);
            Assert.That(revivable.UnrevivableAt, Is.EqualTo(deadline),
                "even a large dose cannot extend a deadline at or before the current time");
            unrevivable.Update(1.1f);
            Assert.That(unrevivable.IsUnrevivable(target), Is.True);
        });
    }

    [Test]
    public async Task ForcedPermanentDeathCannotExtendActiveCountdown()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var target = SpawnDeadPatient(map.GridCoords);
            var revivable = SEntMan.GetComponent<RMCRevivableComponent>(target);
            var unrevivable = Server.System<RMCUnrevivableSystem>();
            var deadline = SGameTiming.CurTime + TimeSpan.FromSeconds(60);
            revivable.UnrevivableAt = deadline;

            // Execution, suicide and chestburst use this same forced-death entry point.
            unrevivable.MakeUnrevivable((target, revivable));
            Assert.That(SEntMan.GetComponent<UnrevivableComponent>(target).FromRevivalTimer, Is.False);
            ApplyDose(target, 1f);
            Assert.That(revivable.UnrevivableAt, Is.EqualTo(deadline));
            Assert.That(unrevivable.IsUnrevivable(target), Is.True);
        });
    }

    [TestCase(150f, MobState.Dead, true)]
    [TestCase(151f, MobState.Dead, false)]
    [TestCase(150f, MobState.Alive, false)]
    public async Task RequiresDeadPatientAtCryogenicTemperature(float temperature, MobState state, bool works)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var target = SpawnDeadPatient(map.GridCoords);
            Server.System<MobStateSystem>().ChangeMobState(target, state);
            SEntMan.GetComponent<TemperatureComponent>(target).Temperature = temperature;
            var revivable = SEntMan.GetComponent<RMCRevivableComponent>(target);
            var deadline = SGameTiming.CurTime + TimeSpan.FromSeconds(60);
            revivable.UnrevivableAt = deadline;

            Assert.That(ApplyDose(target, 1f), Is.EqualTo(works));
            Assert.That(revivable.UnrevivableAt,
                Is.EqualTo(deadline + (works ? TimeSpan.FromSeconds(5) : TimeSpan.Zero)));
        });
    }

    private EntityUid SpawnDeadPatient(EntityCoordinates coordinates)
    {
        var target = SEntMan.SpawnEntity(null, coordinates);
        SEntMan.AddComponent<RMCRevivableComponent>(target);
        SEntMan.AddComponent<MobStateComponent>(target);
        SEntMan.AddComponent<TemperatureComponent>(target).Temperature = 150f;
        Server.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);
        return target;
    }

    private bool ApplyDose(EntityUid target, float dose)
    {
        var reagent = SProtoMan.Index(ReagentId);
        var effect = reagent.Metabolisms!.Metabolisms["Bloodstream"].Effects
            .OfType<CMUReverseRevivalTimer>().Single();
        var context = new ReagentEffectContext(reagent, null, null, null,
            new ReagentQuantity(ReagentId, FixedPoint2.New(dose)), "Bloodstream", null,
            ReagentEffectOrigin.Metabolism);
        return Server.System<SharedEntityEffectsSystem>().TryApplyEffect(target, effect,
            scale: 0.5f, reagentContext: context);
    }
}
