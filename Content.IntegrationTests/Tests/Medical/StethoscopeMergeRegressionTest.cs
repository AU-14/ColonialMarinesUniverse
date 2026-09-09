#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Medical.Stethoscope;
using Content.Shared.Medical.Stethoscope.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using ClientPopupSystem = Content.Client.Popups.PopupSystem;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(StethoscopeSystem))]
public sealed class StethoscopeMergeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: StethoscopeMergeOrganic
  components:
  - type: MobState
  - type: Damageable
  - type: Injurable
    damageContainer: Biological

- type: entity
  id: StethoscopeMergeSynth
  parent: StethoscopeMergeOrganic
  components:
  - type: Synth

- type: entity
  id: StethoscopeMergeNoMobState
  components:
  - type: Damageable
  - type: Injurable
    damageContainer: Biological

- type: entity
  id: StethoscopeMergeMobOnly
  components:
  - type: MobState
";

    [Test]
    public async Task OrganicDeltaSynthResetAndNothingBranchesPreserveState()
    {
        var map = await Pair.CreateTestMap();
        var originalAttached = ServerSession!.AttachedEntity;
        EntityUid user = default;
        EntityUid stethoscope = default;
        EntityUid organic = default;
        EntityUid synth = default;
        EntityUid dead = default;
        EntityUid noMobState = default;
        EntityUid noDamageable = default;
        EntityUid noAsphyxiation = default;
        string raggedy = default!;
        string hyperWorsening = default!;
        string raggedyImproving = default!;
        string raggedySteady = default!;
        string nothing = default!;

        try
        {
            await Server.WaitPost(() =>
            {
                user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                stethoscope = SEntMan.SpawnEntity("ClothingNeckStethoscope", map.GridCoords);
                organic = SEntMan.SpawnEntity("StethoscopeMergeOrganic", map.GridCoords);
                synth = SEntMan.SpawnEntity("StethoscopeMergeSynth", map.GridCoords);
                dead = SEntMan.SpawnEntity("StethoscopeMergeOrganic", map.GridCoords);
                noMobState = SEntMan.SpawnEntity("StethoscopeMergeNoMobState", map.GridCoords);
                noDamageable = SEntMan.SpawnEntity("StethoscopeMergeMobOnly", map.GridCoords);
                noAsphyxiation = SEntMan.SpawnEntity("StethoscopeMergeOrganic", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(ServerSession, user);

                var damageable = Server.System<DamageableSystem>();
                damageable.TryChangeDamage(organic, Damage(20));
                damageable.TryChangeDamage(synth, Damage(40));
                damageable.TryChangeDamage(dead, Damage(20));
                Server.System<MobStateSystem>().ChangeMobState(dead, MobState.Dead);

                var localization = Server.ResolveDependency<ILocalizationManager>();
                raggedy = localization.GetString("stethoscope-raggedy");
                hyperWorsening = localization.GetString(
                    "stethoscope-combined-status",
                    ("absolute", localization.GetString("stethoscope-hyper")),
                    ("delta", localization.GetString("stethoscope-delta-worsening")));
                raggedyImproving = localization.GetString(
                    "stethoscope-combined-status",
                    ("absolute", raggedy),
                    ("delta", localization.GetString("stethoscope-delta-improving")));
                raggedySteady = localization.GetString(
                    "stethoscope-combined-status",
                    ("absolute", raggedy),
                    ("delta", localization.GetString("stethoscope-delta-steady")));
                nothing = localization.GetString("stethoscope-nothing");
            });
            await Pair.RunUntilSynced();

            await Measure(organic, raggedy);
            Assert.That(Stethoscope().LastMeasuredDamage, Is.EqualTo(FixedPoint2.New(20)));

            await Server.WaitPost(() => Server.System<DamageableSystem>()
                .TryChangeDamage(organic, Damage(15)));
            await Measure(organic, hyperWorsening);
            Assert.That(Stethoscope().LastMeasuredDamage, Is.EqualTo(FixedPoint2.New(35)));

            await Server.WaitPost(() => Server.System<DamageableSystem>()
                .TryChangeDamage(organic, Damage(-10)));
            await Measure(organic, raggedyImproving);
            Assert.That(Stethoscope().LastMeasuredDamage, Is.EqualTo(FixedPoint2.New(25)));

            await Measure(organic, raggedySteady);

            await Measure(synth, nothing, exactNewMatches: 1);
            Assert.That(Stethoscope().LastMeasuredDamage, Is.Null,
                "Synth must reset the biological baseline even with MobState, Damageable and Asphyxiation");

            await Measure(organic, raggedy);
            Assert.That(Stethoscope().LastMeasuredDamage, Is.EqualTo(FixedPoint2.New(25)),
                "the biological measurement after a Synth is absolute-only and seeds a fresh baseline");

            foreach (var target in new[] { dead, noMobState, noDamageable, noAsphyxiation })
            {
                await Server.WaitPost(() => Stethoscope().LastMeasuredDamage = FixedPoint2.New(99));
                await Measure(target, nothing);
                Assert.That(Stethoscope().LastMeasuredDamage, Is.Null,
                    $"the nothing branch must clear stale damage for {SEntMan.ToPrettyString(target)}");
            }
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, originalAttached));
        }

        StethoscopeComponent Stethoscope()
        {
            return SEntMan.GetComponent<StethoscopeComponent>(stethoscope);
        }

        async Task Measure(EntityUid target, string expected, int? exactNewMatches = null)
        {
            var before = 0;
            await Client.WaitAssertion(() => before = PopupCount(expected, null));
            await Server.WaitPost(() => InvokeExamine(stethoscope, user, target));
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                var clientTarget = CEntMan.GetEntity(SEntMan.GetNetEntity(target));
                Assert.That(PopupCount(expected, clientTarget),
                    Is.GreaterThanOrEqualTo(1),
                    "the popup must be displayed on the measured target for the attached user");
                if (exactNewMatches != null)
                    Assert.That(PopupCount(expected, null) - before, Is.EqualTo(exactNewMatches.Value));
            });
        }
    }

    [Test]
    public async Task InvalidDoAfterCompletionsResetWithoutPopupAndSuccessRepeats()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid stethoscope = default;
        EntityUid target = default;

        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            stethoscope = SEntMan.SpawnEntity("ClothingNeckStethoscope", map.GridCoords);
            target = SEntMan.SpawnEntity("StethoscopeMergeOrganic", map.GridCoords);
            Server.System<DamageableSystem>().TryChangeDamage(target, Damage(20));
        });

        await Server.WaitAssertion(() =>
        {
            var component = SEntMan.GetComponent<StethoscopeComponent>(stethoscope);
            var handled = Event(user, stethoscope, target);
            handled.Handled = true;
            component.LastMeasuredDamage = FixedPoint2.New(10);
            InvokeDoAfter(stethoscope, ref handled);
            Assert.Multiple(() =>
            {
                Assert.That(component.LastMeasuredDamage, Is.Null);
                Assert.That(handled.Repeat, Is.False);
            });

            var cancelled = Event(user, stethoscope, target);
            cancelled.DoAfter.CancelledTime = TimeSpan.Zero;
            component.LastMeasuredDamage = FixedPoint2.New(10);
            InvokeDoAfter(stethoscope, ref cancelled);
            Assert.Multiple(() =>
            {
                Assert.That(component.LastMeasuredDamage, Is.Null);
                Assert.That(cancelled.Repeat, Is.False);
            });

            var missingTarget = Event(user, stethoscope, null);
            component.LastMeasuredDamage = FixedPoint2.New(10);
            InvokeDoAfter(stethoscope, ref missingTarget);
            Assert.Multiple(() =>
            {
                Assert.That(component.LastMeasuredDamage, Is.Null);
                Assert.That(missingTarget.Repeat, Is.False);
            });

            var success = Event(user, stethoscope, target);
            InvokeDoAfter(stethoscope, ref success);
            Assert.Multiple(() =>
            {
                Assert.That(success.Handled, Is.False);
                Assert.That(success.Repeat, Is.True);
                Assert.That(component.LastMeasuredDamage, Is.EqualTo(FixedPoint2.New(20)));
            });
        });
    }

    private void InvokeExamine(EntityUid stethoscope, EntityUid user, EntityUid target)
    {
        var component = SEntMan.GetComponent<StethoscopeComponent>(stethoscope);
        typeof(StethoscopeSystem)
            .GetMethod("ExamineWithStethoscope", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Server.System<StethoscopeSystem>(),
                new object[] { new Entity<StethoscopeComponent>(stethoscope, component), user, target });
    }

    private void InvokeDoAfter(EntityUid stethoscope, ref StethoscopeDoAfterEvent args)
    {
        var component = SEntMan.GetComponent<StethoscopeComponent>(stethoscope);
        object?[] invocation = { new Entity<StethoscopeComponent>(stethoscope, component), args };
        typeof(StethoscopeSystem)
            .GetMethod("OnDoAfter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Server.System<StethoscopeSystem>(), invocation);
        args = (StethoscopeDoAfterEvent) invocation[1]!;
    }

    private StethoscopeDoAfterEvent Event(
        EntityUid user,
        EntityUid stethoscope,
        EntityUid? target)
    {
        var ev = new StethoscopeDoAfterEvent();
        var args = new DoAfterArgs(SEntMan, user, TimeSpan.Zero, ev, stethoscope, target, stethoscope);
        ev.DoAfter = new Content.Shared.DoAfter.DoAfter(0, args, TimeSpan.Zero);
        return ev;
    }

    private int PopupCount(string message, EntityUid? entity)
    {
        var popup = Client.System<ClientPopupSystem>();
        var dictionary = (IDictionary) typeof(ClientPopupSystem)
            .GetField("_aliveWorldLabels", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(popup)!;
        var count = 0;

        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key;
            var type = key.GetType();
            if ((string) type.GetProperty("Message")!.GetValue(key)! != message)
                continue;
            if (entity != null && (EntityUid?) type.GetProperty("Entity")!.GetValue(key) != entity)
                continue;
            count++;
        }

        return count;
    }

    private static DamageSpecifier Damage(float amount)
    {
        return new DamageSpecifier
        {
            DamageDict =
            {
                ["Asphyxiation"] = FixedPoint2.New(amount),
            },
        };
    }
}

#pragma warning restore RA0002
