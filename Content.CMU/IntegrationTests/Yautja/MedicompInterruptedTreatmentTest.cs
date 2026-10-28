using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Slow;
using Content.Shared.Atmos;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class MedicompInterruptedTreatmentTest : GameTest
{
    [Test]
    public async Task DroppedHealingGunCanResumeAndClampRestoresSpeed()
    {
        EntityUid hunter = default;
        EntityUid torso = default;
        EntityUid map = default;
        EntityUid tool = default;
        var delay = 0f;
        var normalSpeed = 0f;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId, runMapInit: true);
            maps.SetPaused(map, false);
            // Surgery takes time: keep space cooling from adding an unrelated movement penalty.
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            SEntMan.System<AtmosphereSystem>().SetMapAtmosphere(map, false, new GasMixture(moles, Atmospherics.T20C));
            hunter = SEntMan.SpawnEntity("CMUMobYautja", new MapCoordinates(Vector2.Zero, mapId));
        });
        // Let initial body updates settle before measuring the healthy baseline.
        await Pair.RunTicksSync(Pair.SecondsToTicks(1));
        await Server.WaitAssertion(() =>
        {
            normalSpeed = SEntMan.GetComponent<MovementSpeedModifierComponent>(hunter).CurrentSprintSpeed;
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", 10);
            SEntMan.System<DamageableSystem>().TryChangeDamage(hunter, damage, true);
            Assert.That(SEntMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(hunter,
                new CMUMedicalBodyPartKey(BodyPartType.Torso, BodyPartSymmetry.None), out torso), Is.True);
            StartStage("CMUYautjaStabilizerGel", 0);
        });
        await Pair.RunTicksSync(Pair.SecondsToTicks(delay + 0.5f));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<MovementSpeedModifierComponent>(hunter).CurrentSprintSpeed, Is.LessThan(normalSpeed));
            SEntMan.DeleteEntity(tool);
            StartStage("CMUYautjaHealingGun", 1);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryDrop(hunter, tool), Is.True);
        });
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<YautjaHealingGunComponent>(tool).Loaded, Is.True);
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(hunter).DoAfters.Values.Any(d => !d.Cancelled && !d.Completed), Is.False);
            Assert.That(SEntMan.HasComponent<CMUYautjaMedicompStabilizedComponent>(torso), Is.True);
            SEntMan.DeleteEntity(tool);
            StartStage("CMUYautjaHealingGun", 1);
        });
        await Pair.RunTicksSync(Pair.SecondsToTicks(delay + 0.5f));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<YautjaHealingGunComponent>(tool).Loaded, Is.False);
            SEntMan.DeleteEntity(tool);
            StartStage("CMUYautjaWoundClamp", 2);
        });
        await Pair.RunTicksSync(Pair.SecondsToTicks(delay + 0.5f));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RMCSlowdownComponent>(hunter), Is.False);
            Assert.That(SEntMan.HasComponent<RMCSuperSlowdownComponent>(hunter), Is.False);
            Assert.That(SEntMan.GetComponent<MovementSpeedModifierComponent>(hunter).CurrentSprintSpeed, Is.EqualTo(normalSpeed).Within(0.001f));
            SEntMan.DeleteEntity(hunter);
            SEntMan.DeleteEntity(map);
        });

        void StartStage(string prototype, int step)
        {
            tool = SEntMan.SpawnEntity(prototype, SEntMan.GetComponent<TransformComponent>(hunter).Coordinates);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(hunter, tool), Is.True);
            Assert.That(SEntMan.System<CMUSurgeryFlowSystem>().TryArmExactStep(hunter, hunter, torso,
                "CMUSurgeryMcompWounds", step, BodyPartType.Torso, BodyPartSymmetry.None), Is.Not.Null);
            Assert.That(SEntMan.System<CMUSurgeryDispatchSystem>().TryDispatch(hunter, hunter, tool), Is.True);
            delay = (float) SEntMan.GetComponent<DoAfterComponent>(hunter).DoAfters.Values.Single(d => !d.Cancelled && !d.Completed).Args.Delay.TotalSeconds;
        }
    }
}
