#pragma warning disable RA0002
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Atmos;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Atmos;
using Content.Shared.Roles;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaFireAndMedalParityTest : GameTest
{
    // cmss13 only gives service medals to EQUIPMENT_PRESET_MARINE, so no yautja role should ever roll one
    [Test]
    public async Task YautjaJobsNeverAwardPlaytimeMedals()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ResolveDependency<IPrototypeManager>();
            var checkedJobs = 0;
            foreach (var job in protos.EnumeratePrototypes<JobPrototype>())
            {
                if (!job.ID.StartsWith("CMUYautja"))
                    continue;

                checkedJobs++;
                Assert.That(job.Medals is null || job.Medals.Count == 0, Is.True,
                    $"{job.ID} still inherits playtime medals");
            }

            Assert.That(checkedJobs, Is.GreaterThanOrEqualTo(6));

            // and the marines still get theirs, so the override didn't leak
            Assert.That(protos.Index<JobPrototype>("CMRifleman").Medals, Is.Not.Empty);
        });
    }

    // cmss13 resist_fire: yautja take -25 stacks and a WEAKEN 1 roll, and are never forced to roll
    [Test]
    public async Task YautjaRollFireQuicklyAndNeverPanicRoll()
    {
        var map = await Pair.CreateTestMap();
        EntityUid hunter = default;
        await Server.WaitAssertion(() =>
        {
            var flammableSys = SEntMan.System<FlammableSystem>();
            hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            Assert.That(SEntMan.HasComponent<CMUHunterFireRollComponent>(hunter), Is.True);

            var flammable = SEntMan.GetComponent<FlammableComponent>(hunter);
            Assert.That(flammable.ResistTime, Is.EqualTo(TimeSpan.FromSeconds(1)));

            // stacks cap at 10, so start low enough that the extra 15 off is visible
            flammableSys.SetFireStacks(hunter, 8, flammable, ignite: true);
            Assert.That(flammable.OnFire, Is.True);

            if (SEntMan.TryGetComponent(hunter, out CMUBurnPanicComponent? panic))
                Assert.That(panic.ForceRollAt, Is.Null, "preds must not get queued for the forced panic roll");

            var before = flammable.FireStacks;
            SEntMan.EventBus.RaiseLocalEvent(hunter, new ResistFireAlertEvent { User = hunter, AlertId = flammable.FireAlert });

            Assert.That(flammable.FireStacks, Is.LessThanOrEqualTo(0f),
                "an expert roll takes 25 stacks off, 8 stacks are gone in one roll");
            Assert.That(before, Is.EqualTo(8f).Within(0.01f));
            var roll = SEntMan.GetComponent<CMUHunterFireRollComponent>(hunter);
            Assert.That(roll.ResistStacks, Is.EqualTo(-25f));
            Assert.That(roll.LastRollEnd, Is.Not.Null, "the expert roll top-up ran for this roll");

            var status = SEntMan.System<StatusEffectsSystem>();
            Assert.That(status.TryGetTime(hunter, SharedStunSystem.ParalyzeId, out var stun), Is.True);
            var timing = Server.ResolveDependency<IGameTiming>();
            Assert.That(stun.EndEffectTime, Is.Not.Null);
            Assert.That(stun.EndEffectTime!.Value - timing.CurTime, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(1.01)),
                "the roll pins a pred for about a second, not the human 8");
        });

        // well past the 10s panic delay with the fire still going
        await Server.WaitPost(() => SEntMan.System<FlammableSystem>().SetFireStacks(hunter, 200, null, ignite: true));
        await Pair.RunSeconds(12);
        await Server.WaitAssertion(() =>
        {
            if (SEntMan.TryGetComponent(hunter, out CMUBurnPanicComponent? panic))
            {
                Assert.That(panic.Forcing, Is.False);
                Assert.That(panic.ForceRollAt, Is.Null);
            }
        });
    }
}
