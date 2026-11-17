using Content.Shared.Damage.Systems;
using System.Linq;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Injuries.Pain;
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

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaMedicompProgressTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<DamageGroupPrototype> BrutePrototype = "Brute";

    // direct tool use (no surgery window) on a standing hunter has to find the medicomp
    // procedure the same way the window does
    [Test]
    public async Task MedicompStartsFromDirectToolUse()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var hands = entMan.System<SharedHandsSystem>();
            var dispatch = entMan.System<CMUSurgeryDispatchSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();
            var brute = prototypes.Index<DamageGroupPrototype>(BrutePrototype);

            var patient = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var tool = entMan.SpawnEntity("CMUYautjaStabilizerGel", MapCoordinates.Nullspace);

            try
            {
                entMan.System<SkillsSystem>().SetSkill(surgeon, "RMCSkillSurgery", 3);
                entMan.System<DamageableSystem>().TryChangeDamage(patient, new DamageSpecifier(brute, 20));
                targeting.SelectZone((surgeon, null), TargetBodyZone.LeftArm);
                Assert.That(hands.TryPickupAnyHand(surgeon, tool), Is.True);

                Assert.That(dispatch.TryDispatchUiLess(surgeon, patient, tool), Is.True);
                Assert.That(entMan.TryGetComponent(patient, out CMUSurgeryArmedStepComponent? armed), Is.True,
                    "Direct medicomp use must arm the stabilize step.");
                Assert.That(armed!.SurgeryId, Is.EqualTo("CMUSurgeryMcompWounds"));
                Assert.That(entMan.GetComponent<DoAfterComponent>(surgeon).DoAfters, Is.Not.Empty);
            }
            finally
            {
                foreach (var uid in new[] { tool, surgeon, patient })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

}
