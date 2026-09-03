#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.CriticalGrace;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Events;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Mobs;

[TestFixture]
[TestOf(typeof(MobThresholdSystem))]
public sealed class MobThresholdMergeRegressionTest : GameTest
{
    [Test]
    public async Task XenoHealthUsesIncapMaximumAndCriticalGraceAlertState()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid xeno = default;
        EntityUid human = default;
        NetEntity xenoNet = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var thresholds = Server.System<MobThresholdSystem>();
                Server.System<MobThresholdMergeProbeSystem>().CriticalTransitions = 0;
                xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
                human = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                xenoNet = SEntMan.GetNetEntity(xeno);
                SEntMan.AddComponent<MobThresholdMergeProbeComponent>(xeno);
                Server.PlayerMan.SetAttachedEntity(session, xeno);
                var component = SEntMan.GetComponent<MobThresholdsComponent>(xeno);
                component.Thresholds = new SortedDictionary<FixedPoint2, MobState>
                {
                    [0] = MobState.Alive,
                    [200] = MobState.Critical,
                    [300] = MobState.Dead,
                };
                component.StateAlertDict = new Dictionary<MobState, ProtoId<AlertPrototype>>
                {
                    [MobState.Alive] = "HumanHealth",
                    [MobState.Critical] = "HumanCrit",
                    [MobState.Dead] = "HumanDead",
                };
                component.ShowOverlays = false;
                component.AllowRevives = true;
                component.DisplayDamageInAlert = false;
                SEntMan.Dirty(xeno, component);
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientXeno = CEntMan.GetEntity(xenoNet);
                var threshold = CEntMan.GetComponent<MobThresholdsComponent>(clientXeno);
                Assert.Multiple(() =>
                {
                    Assert.That(threshold.StateAlertDict[MobState.Alive].ToString(), Is.EqualTo("HumanHealth"));
                    Assert.That(threshold.StateAlertDict[MobState.Critical].ToString(), Is.EqualTo("HumanCrit"));
                    Assert.That(threshold.ShowOverlays, Is.False);
                    Assert.That(threshold.AllowRevives, Is.True);
                    Assert.That(threshold.DisplayDamageInAlert, Is.False,
                        "all custom MobThreshold state fields must overwrite the opposite client prototype values");
                });
            });

            await Server.WaitAssertion(() =>
            {
                var component = SEntMan.GetComponent<MobThresholdsComponent>(xeno);
                component.StateAlertDict[MobState.Alive] = "XenoHealth";
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientXeno = CEntMan.GetEntity(xenoNet);
                var threshold = CEntMan.GetComponent<MobThresholdsComponent>(clientXeno);
                Assert.That(threshold.StateAlertDict[MobState.Alive].ToString(), Is.EqualTo("HumanHealth"),
                    "the handled client state owns its dictionary rather than aliasing the authoritative component");
            });

            await Server.WaitAssertion(() =>
            {
                var thresholds = Server.System<MobThresholdSystem>();
                var component = SEntMan.GetComponent<MobThresholdsComponent>(xeno);
                component.StateAlertDict = new Dictionary<MobState, ProtoId<AlertPrototype>>
                {
                    [MobState.Alive] = "XenoHealth",
                    [MobState.Critical] = "XenoCrit",
                    [MobState.Dead] = "XenoDead",
                };
                component.ShowOverlays = true;
                component.AllowRevives = false;
                component.DisplayDamageInAlert = true;
                SEntMan.Dirty(xeno, component);
                thresholds.VerifyThresholds(xeno, component);
                thresholds.VerifyThresholds(human);

                var alerts = Server.EntMan.System<AlertsSystem>();
                var healthKey = AlertKey.ForCategory("Health");
                Assert.That(alerts.TryGetAlertState(xeno, healthKey, out var xenoHealth), Is.True);
                Assert.That(alerts.TryGetAlertState(human, healthKey, out var humanHealth), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(xenoHealth.Type.ToString(), Is.EqualTo("XenoHealth"));
                    Assert.That(xenoHealth.DynamicMessage, Is.EqualTo("200 / 200"),
                        "numeric Xeno health uses the first incapacitation threshold, not the 300 death threshold");
                    Assert.That(humanHealth.DynamicMessage, Is.Null,
                        "ordinary DisplayDamageInAlert=false mobs retain the upstream tooltip-only alert");
                });
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientXeno = CEntMan.GetEntity(xenoNet);
                var threshold = CEntMan.GetComponent<MobThresholdsComponent>(clientXeno);
                var alerts = Client.EntMan.System<AlertsSystem>();
                Assert.That(alerts.TryGetAlertState(clientXeno, AlertKey.ForCategory("Health"), out var health), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(threshold.StateAlertDict[MobState.Alive].ToString(), Is.EqualTo("XenoHealth"));
                    Assert.That(threshold.StateAlertDict[MobState.Critical].ToString(), Is.EqualTo("XenoCrit"));
                    Assert.That(threshold.ShowOverlays, Is.True);
                    Assert.That(threshold.AllowRevives, Is.False);
                    Assert.That(threshold.DisplayDamageInAlert, Is.True);
                    Assert.That(health.DynamicMessage, Is.EqualTo("200 / 200"));
                });
            });

            await Server.WaitAssertion(() =>
            {
                var damageable = Server.System<DamageableSystem>();
                Assert.That(damageable.TryChangeDamage(xeno, Damage(50), ignoreResistances: true), Is.Not.Null);
                AssertAlert(xeno, MobState.Alive, "XenoHealth", "150 / 200");

                Assert.That(damageable.TryChangeDamage(xeno, Damage(150), ignoreResistances: true), Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.GetComponent<MobStateComponent>(xeno).CurrentState, Is.EqualTo(MobState.Alive),
                        "damage exactly equal to the incap threshold must enter critical grace and remain Alive");
                    Assert.That(SEntMan.HasComponent<InCriticalGraceComponent>(xeno), Is.True);
                });
                AssertAlert(xeno, MobState.Alive, "XenoCrit", "0 / 200");
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientXeno = CEntMan.GetEntity(xenoNet);
                var alerts = Client.EntMan.System<AlertsSystem>();
                Assert.That(alerts.TryGetAlertState(clientXeno, AlertKey.ForCategory("Health"), out var state), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(state.Type.ToString(), Is.EqualTo("XenoCrit"));
                    Assert.That(state.DynamicMessage, Is.EqualTo("0 / 200"),
                        "threshold equality uses the Critical alert while grace keeps the owner Alive");
                });
            });

            await Server.WaitAssertion(() =>
            {
                var probe = Server.System<MobThresholdMergeProbeSystem>();
                var grace = SEntMan.GetComponent<InCriticalGraceComponent>(xeno);
                Assert.That(grace.Over, Is.False);
                SEntMan.RemoveComponent<InCriticalGraceComponent>(xeno);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.GetComponent<MobStateComponent>(xeno).CurrentState, Is.EqualTo(MobState.Critical),
                        "shutdown must mark the still-resolvable grace component Over before threshold verification");
                    Assert.That(SEntMan.HasComponent<InCriticalGraceComponent>(xeno), Is.False,
                        "explicit grace removal must not grant a replacement grace component");
                    Assert.That(probe.CriticalTransitions, Is.EqualTo(1),
                        "the unchanged equality damage must produce exactly one Alive-to-Critical transition");
                });
                AssertAlert(xeno, MobState.Critical, "XenoCrit", "0 / 200");

                var damageable = Server.System<DamageableSystem>();
                Assert.That(damageable.TryChangeDamage(xeno, Damage(-10), ignoreResistances: true), Is.Not.Null);
                AssertAlert(xeno, MobState.Alive, "XenoHealth", "10 / 200");
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
            });
        }
    }

    private void AssertAlert(EntityUid entity, MobState expectedMobState, string type, string message)
    {
        var alerts = Server.EntMan.System<AlertsSystem>();
        var mobState = SEntMan.GetComponent<MobStateComponent>(entity);
        Assert.That(alerts.TryGetAlertState(entity, AlertKey.ForCategory("Health"), out var alert), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(mobState.CurrentState, Is.EqualTo(expectedMobState));
            Assert.That(alert.Type.ToString(), Is.EqualTo(type));
            Assert.That(alert.DynamicMessage, Is.EqualTo(message));
        });
    }

    private DamageSpecifier Damage(float amount)
    {
        var type = Server.ProtoMan.Index<DamageTypePrototype>("Blunt");
        return new DamageSpecifier(type, FixedPoint2.New(amount));
    }
}

[RegisterComponent]
public sealed partial class MobThresholdMergeProbeComponent : Component;

public sealed partial class MobThresholdMergeProbeSystem : EntitySystem
{
    public int CriticalTransitions;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobThresholdMergeProbeComponent, MobStateChangedEvent>(OnStateChanged);
    }

    private void OnStateChanged(Entity<MobThresholdMergeProbeComponent> entity, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical)
            CriticalTransitions++;
    }
}

#pragma warning restore RA0002
