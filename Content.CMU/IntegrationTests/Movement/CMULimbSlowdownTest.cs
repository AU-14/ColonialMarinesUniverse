using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Slow;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Movement;

[TestFixture]
public sealed class CMULimbSlowdownTest : GameTest
{
    [Test]
    public async Task ArmoredSlowdownRemainsFiniteAcrossLimbLossAndTableReentry()
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid table = default;
        EntityUid torso = default;
        NetEntity patientNet = default;
        var parts = new List<(EntityUid Part, string Slot)>();
        var original = Pair.Player!.AttachedEntity;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var coords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
                table = SEntMan.SpawnEntity("CMOperatingTable", coords);
                patient = SEntMan.SpawnEntity("CMMobHuman", coords);
                patientNet = SEntMan.GetNetEntity(patient);
                Server.PlayerMan.SetAttachedEntity(Pair.Player, patient);
                var armor = SEntMan.SpawnEntity("CMArmorM3Medium", coords);
                Assert.That(Server.System<InventorySystem>().TryEquip(patient, armor, "outerClothing", force: true), Is.True);
                Assert.That(Server.System<RMCSlowSystem>().TrySuperSlowdown(patient, TimeSpan.FromSeconds(30)), Is.True);

                var index = Server.System<CMUMedicalBodyIndexSystem>();
                torso = Server.System<SharedBodySystem>().GetRootPartOrNull(patient)!.Value.Entity;
                foreach (var (type, side, slot) in new[]
                         {
                             (BodyPartType.Arm, BodyPartSymmetry.Left, "left_arm"),
                             (BodyPartType.Arm, BodyPartSymmetry.Right, "right_arm"),
                             (BodyPartType.Leg, BodyPartSymmetry.Left, "left_leg"),
                             (BodyPartType.Leg, BodyPartSymmetry.Right, "right_leg"),
                         })
                {
                    Assert.That(index.TryGetBodyPart(patient, new CMUMedicalBodyPartKey(type, side), out var part), Is.True);
                    parts.Add((part, slot));
                }
                Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(patient, patient, table), Is.True);
            });
            await RunTicksSync(30);

            await Server.WaitAssertion(() =>
            {
                foreach (var (part, _) in parts)
                    Assert.That(Server.System<DetachableOrganSystem>().Detach(part), Is.Not.Null);
            });
            await RunTicksSync(30);
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.GetComponent<MovementSpeedModifierComponent>(patient).CurrentSprintSpeed,
                    Is.Zero, "a slowed, armored patient without legs must have zero speed rather than NaN"));

            await Server.WaitAssertion(() =>
            {
                foreach (var (part, slot) in parts)
                    Assert.That(Server.System<SharedBodySystem>().AttachPart(torso, slot, part), Is.True);
            });
            await RunTicksSync(30);
            await Server.WaitAssertion(() =>
                Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(patient, patient, table), Is.True));
            await RunTicksSync(30);

            await Server.WaitAssertion(() =>
            {
                var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(patient);
                var xform = SEntMan.GetComponent<TransformComponent>(patient);
                Assert.Multiple(() =>
                {
                    Assert.That(float.IsFinite(speed.CurrentSprintSpeed), Is.True);
                    Assert.That(speed.CurrentSprintSpeed, Is.GreaterThan(0));
                    Assert.That(xform.ParentUid, Is.EqualTo(table));
                    Assert.That(xform.MapID, Is.EqualTo(SEntMan.GetComponent<TransformComponent>(table).MapID));
                });
            });
            await Client.WaitAssertion(() =>
            {
                var clientPatient = CEntMan.GetEntity(patientNet);
                var speed = CEntMan.GetComponent<MovementSpeedModifierComponent>(clientPatient);
                var coords = Client.System<SharedTransformSystem>().GetMapCoordinates(clientPatient);
                Assert.Multiple(() =>
                {
                    Assert.That(float.IsFinite(speed.CurrentSprintSpeed), Is.True);
                    Assert.That(speed.CurrentSprintSpeed, Is.GreaterThan(0));
                    Assert.That(float.IsFinite(coords.X) && float.IsFinite(coords.Y), Is.True);
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(Pair.Player, original));
        }
    }
}
