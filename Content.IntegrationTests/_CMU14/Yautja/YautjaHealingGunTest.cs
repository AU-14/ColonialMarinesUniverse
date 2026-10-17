using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

// The port routes healing through the Medicomp surgery session. Keep master's
// self-repair regression coverage at that boundary, including capsule consumption.
[TestFixture]
[TestOf(typeof(YautjaHealingGunComponent))]
public sealed class YautjaHealingGunTest : GameTest
{
    [Test]
    public async Task SelfUseCannotBypassMedicompTreatment()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId, runMapInit: true);
            var coordinates = new MapCoordinates(Vector2.Zero, mapId);
            var hunter = SEntMan.SpawnEntity("CMUMobYautja", coordinates);
            var gun = SEntMan.SpawnEntity("CMUYautjaHealingGun", coordinates);
            try
            {
                SEntMan.GetComponent<YautjaRecallableComponent>(gun).YautjaOwner = hunter;
                var damage = SEntMan.System<DamageableSystem>();
                var blunt = new DamageSpecifier();
                blunt.DamageDict.Add("Blunt", FixedPoint2.New(10));
                Assert.That(damage.TryChangeDamage(hunter, blunt, true), Is.Not.Null);
                var before = damage.GetAllDamage(hunter).GetTotal();

                for (var i = 0; i < 2; i++)
                {
                    var use = new UseInHandEvent(hunter);
                    SEntMan.EventBus.RaiseLocalEvent(gun, use);
                    Assert.That(damage.GetAllDamage(hunter).GetTotal(), Is.EqualTo(before));
                    Assert.That(SEntMan.GetComponent<YautjaHealingGunComponent>(gun).Loaded, Is.True);
                }
            }
            finally
            {
                SEntMan.DeleteEntity(gun);
                SEntMan.DeleteEntity(hunter);
                SEntMan.DeleteEntity(map);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DeepDamageCanBeSelfRepairedThroughMedicomp(bool surgicalTrait)
    {
        EntityUid hunter = default;
        EntityUid gun = default;
        EntityUid torso = default;
        EntityUid map = default;
        var delay = 0f;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId, runMapInit: true);
            maps.SetPaused(map, false);
            var coordinates = new MapCoordinates(Vector2.Zero, mapId);
            hunter = SEntMan.SpawnEntity("CMUMobYautja", coordinates);
            gun = SEntMan.SpawnEntity("CMUYautjaHealingGun", coordinates);
            SEntMan.GetComponent<YautjaRecallableComponent>(gun).YautjaOwner = hunter;
            Assert.That(SEntMan.System<SharedHandsSystem>()
                .TryPickupAnyHand(hunter, gun, checkActionBlocker: false), Is.True);

            var index = SEntMan.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetBodyPart(hunter,
                new CMUMedicalBodyPartKey(BodyPartType.Torso, BodyPartSymmetry.None),
                out torso), Is.True);
            if (surgicalTrait)
                SEntMan.System<SharedCMUSurgicalTraitSystem>().EnsureTrait(torso, CMUSurgicalTrait.ContaminatedWound);
            else
                SEntMan.System<SharedCMUWoundsSystem>().SeedInternalBleed(torso, "yautja-test", 0.5f);

            // Isolate the gun stage, with its preceding stabilization completed.
            SEntMan.EnsureComponent<CMUYautjaMedicompStabilizedComponent>(torso);
            var flow = SEntMan.System<CMUSurgeryFlowSystem>();
            Assert.That(flow.TryArmExactStep(hunter, hunter, torso, "CMUSurgeryMcompWounds",
                1, BodyPartType.Torso, BodyPartSymmetry.None), Is.Not.Null);
            Assert.That(SEntMan.System<CMUSurgeryDispatchSystem>().TryDispatch(hunter, hunter, gun), Is.True);
            var active = SEntMan.GetComponent<DoAfterComponent>(hunter).DoAfters.Values.Single();
            delay = (float) active.Args.Delay.TotalSeconds;
            Assert.That(SEntMan.GetComponent<YautjaHealingGunComponent>(gun).Loaded, Is.True);
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(delay + 0.5f));
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(torso), Is.False);
                Assert.That(SEntMan.System<SharedCMUSurgicalTraitSystem>().CountTraits(torso), Is.Zero);
                Assert.That(SEntMan.GetComponent<YautjaHealingGunComponent>(gun).Loaded, Is.False);
            });

            // The UI must still offer the closing step after the gun removes
            // the last injury; otherwise the patient remains in surgery.
            SEntMan.DeleteEntity(gun);
            var clamp = SEntMan.SpawnEntity("CMUYautjaWoundClamp", SEntMan.GetComponent<TransformComponent>(hunter).Coordinates);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(hunter, clamp), Is.True);
            Assert.That(SEntMan.System<CMUSurgeryFlowSystem>().TryArmExactStep(hunter, hunter, torso,
                "CMUSurgeryMcompWounds", 2, BodyPartType.Torso, BodyPartSymmetry.None), Is.Not.Null);
            Assert.That(SEntMan.System<CMUSurgeryDispatchSystem>().TryDispatch(hunter, hunter, clamp), Is.True);
            var active = SEntMan.GetComponent<DoAfterComponent>(hunter).DoAfters.Values.Single(value => !value.Completed && !value.Cancelled);
            delay = (float) active.Args.Delay.TotalSeconds;
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(delay + 0.5f));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMUYautjaMedicompTreatedComponent>(torso), Is.False);
            SEntMan.DeleteEntity(hunter);
            SEntMan.DeleteEntity(map);
        });
    }
}
