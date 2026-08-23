using System.Linq;
using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaMedicompProgressTest
{
    private static readonly (string Tool, int Step, string Label)[] Stages =
    [
        ("CMUYautjaStabilizerGel", 0, "stabilize wounds"),
        ("CMUYautjaHealingGun", 1, "tend wounds"),
        ("CMUYautjaWoundClamp", 2, "clamp wounds"),
    ];

    [Test]
    public async Task MedicompStagesUseOneContinuousDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var damageable = entMan.System<DamageableSystem>();
            var skills = entMan.System<SkillsSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var brute = prototypes.Index<DamageGroupPrototype>("Brute");

            foreach (var (toolPrototype, step, label) in Stages)
            {
                var patient = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
                var surgeon = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
                var tool = entMan.SpawnEntity(toolPrototype, MapCoordinates.Nullspace);

                try
                {
                    skills.SetSkill(surgeon, "RMCSkillSurgery", 3);
                    damageable.TryChangeDamage(patient, new DamageSpecifier(brute, 20));
                    var targetPart = FindPart(entMan, patient, BodyPartType.Arm, BodyPartSymmetry.Left);

                    Assert.That(
                        flow.TryArmExactStep(
                            surgeon,
                            patient,
                            targetPart,
                            "CMUSurgeryMcompWounds",
                            step,
                            BodyPartType.Arm,
                            BodyPartSymmetry.Left),
                        Is.Not.Null,
                        $"Medicomp {label} must arm.");
                    Assert.That(hands.TryPickupAnyHand(surgeon, tool), Is.True, $"Medicomp {label} tool pickup.");
                    Assert.That(dispatch.TryDispatch(surgeon, patient, tool), Is.True, $"Medicomp {label} dispatch.");

                    var doAfter = entMan.GetComponent<DoAfterComponent>(surgeon).DoAfters.Values.Single();
                    Assert.That(doAfter.Args.AttemptFrequency, Is.EqualTo(AttemptFrequency.StartAndEnd),
                        $"Medicomp {label} must not re-run its attempt event every tick, or the progress bar flashes.");
                }
                finally
                {
                    foreach (var uid in new[] { tool, surgeon, patient })
                    {
                        if (!entMan.Deleted(uid))
                            entMan.DeleteEntity(uid);
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid FindPart(
        IEntityManager entMan,
        EntityUid patient,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        foreach (var (partUid, part) in entMan.System<SharedBodySystem>().GetBodyChildren(patient))
        {
            if (part.PartType == type && part.Symmetry == symmetry)
                return partUid;
        }

        Assert.Fail($"Could not find {symmetry} {type} on test patient.");
        return default;
    }
}
