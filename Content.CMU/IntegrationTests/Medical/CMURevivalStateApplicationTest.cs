using Content.Client._RMC14.Medical.HUD;
using Content.Client._RMC14.Weapons.Melee;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Administration.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusIcon;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class CMURevivalStateApplicationTest : GameTest
{
    private static readonly EntProtoId PainNumbness = "StatusEffectPainNumbness";
    private static readonly ProtoId<HealthIconPrototype> HealthyIcon = "CMHealthIconHealthy";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task RemovingStatusDuringRevivalSnapshotDoesNotRestoreDeath()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid effect = default;
        NetEntity patientNet = default;
        NetEntity xenoNet = default;
        IComponentState mobState = null;
        IComponentState thresholdsState = null;
        IComponentState containersState = null;
        IComponentState damageState = null;
        await Server.WaitAssertion(() =>
        {
            patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var xeno = SEntMan.SpawnEntity("RMCXenoRavagerBerserker", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, xeno);
            patientNet = SEntMan.GetNetEntity(patient);
            xenoNet = SEntMan.GetNetEntity(xeno);
            Assert.That(Server.System<MobThresholdSystem>().TryGetDeadThreshold(patient, out var dead), Is.True);
            Server.System<DamageableSystem>().TryChangeDamage(patient,
                new DamageSpecifier { DamageDict = { ["Poison"] = dead.Value } }, true);
            Assert.That(Server.System<StatusEffectsSystem>().TrySetStatusEffectDuration(patient, PainNumbness,
                out var status), Is.True);
            effect = status.Value;
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => Assert.That(
            Client.System<MobStateSystem>().IsDead(CEntMan.GetEntity(patientNet)), Is.True));

        await Server.WaitAssertion(() =>
        {
            Server.System<RejuvenateSystem>().PerformRejuvenate(patient);
            SEntMan.DeleteEntity(effect);
            Assert.That(Server.System<MobStateSystem>().IsAlive(patient), Is.True);
            mobState = Capture<MobStateComponent>();
            thresholdsState = Capture<MobThresholdsComponent>();
            containersState = Capture<ContainerManagerComponent>();
            damageState = Capture<DamageableComponent>();
        });

        await Client.WaitAssertion(() =>
        {
            var clientPatient = CEntMan.GetEntity(patientNet);
            // Component states are not an atomic gameplay transition. Removing the status
            // container's entry can happen after MobState but before healed damage arrives.
            using (CGameTiming.StartStateApplicationArea())
            {
                Apply<MobStateComponent>(mobState);
                Apply<MobThresholdsComponent>(thresholdsState);
                Apply<ContainerManagerComponent>(containersState);
                Apply<DamageableComponent>(damageState);
            }

            Assert.That(Client.System<DamageableSystem>().GetTotalDamage(clientPatient).Float(), Is.Zero,
                "the full revival snapshot has applied its healed damage");
            Assert.That(Client.System<MobStateSystem>().IsAlive(clientPatient), Is.True,
                "a revived marine must not be latched dead by a callback during state application");
            Assert.That(CEntMan.GetComponent<MobThresholdsComponent>(clientPatient).CurrentThresholdState,
                Is.EqualTo(MobState.Alive));
            Assert.That(Client.System<CMHealthIconsSystem>().TryGetIcon(
                (clientPatient, CEntMan.GetComponent<DamageableComponent>(clientPatient)), out var icon), Is.True);
            Assert.That(icon, Is.SameAs(CProtoMan.Index(HealthyIcon)));
            var coordinates = Client.System<SharedTransformSystem>().GetMapCoordinates(clientPatient);
            Assert.That(Client.System<RMCMeleeWeaponSystem>().TryGetAlternativeXenoAttackTarget(
                CEntMan.GetEntity(xenoNet), coordinates, [clientPatient], out var selected), Is.True);
            Assert.That(selected, Is.EqualTo(clientPatient));

            // Actual predicted damage must still cross death thresholds outside state application.
            Assert.That(CGameTiming.InPrediction, Is.True);
            Assert.That(Client.System<MobThresholdSystem>().TryGetDeadThreshold(clientPatient, out var dead), Is.True);
            Client.System<DamageableSystem>().TryChangeDamage(clientPatient,
                new DamageSpecifier { DamageDict = { ["Poison"] = dead.Value } }, true);
            Assert.That(Client.System<MobStateSystem>().IsDead(clientPatient), Is.True);

            void Apply<T>(IComponentState state) where T : Component
            {
                var handle = new ComponentHandleState(state, null);
                CEntMan.EventBus.RaiseComponentEvent(clientPatient, CEntMan.GetComponent<T>(clientPatient), ref handle);
            }
        });

        IComponentState Capture<T>() where T : Component
            => SEntMan.GetComponentState(SEntMan.EventBus, SEntMan.GetComponent<T>(patient), null, GameTick.Zero)!;
    }
}
