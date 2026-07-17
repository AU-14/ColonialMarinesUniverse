using Content.Server.Examine;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaSkeletonTrophyTest
{
    [Test]
    public async Task SkeletonLimbExamineShowsCmss13PolishStateToYautjaTechUsers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<ExamineSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var techUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var ordinaryUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var armBone = entMan.SpawnEntity("CMUYautjaHumanLeftArmBoneTrophy", MapCoordinates.Nullspace);
            var xenoPelt = entMan.SpawnEntity("CMUYautjaRunnerPeltTrophy", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(techUser);

                var dirtyHunterText = examine.GetExamineText(armBone, hunter).ToMarkup();
                var dirtyTechText = examine.GetExamineText(armBone, techUser).ToMarkup();
                var dirtyOrdinaryText = examine.GetExamineText(armBone, ordinaryUser).ToMarkup();
                var peltText = examine.GetExamineText(xenoPelt, hunter).ToMarkup();

                var trophy = entMan.GetComponent<YautjaTrophyComponent>(armBone);
                var dirtyLine = Loc.GetString("cmu-yautja-skeleton-trophy-dirty-examine", ("trophy", armBone));
                var peltDirtyLine = Loc.GetString("cmu-yautja-skeleton-trophy-dirty-examine", ("trophy", xenoPelt));
                var polishedLine = Loc.GetString("cmu-yautja-skeleton-trophy-polished-examine");
                trophy.Polished = true;

                var polishedText = examine.GetExamineText(armBone, techUser).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(dirtyHunterText, Does.Contain(dirtyLine),
                        "CMSS13 /obj/item/clothing/accessory/limb/skeleton/get_examine_text() exposes dirty state to TRAIT_YAUTJA_TECH users.");
                    Assert.That(dirtyTechText, Does.Contain(dirtyLine),
                        "CMSS13 checks TRAIT_YAUTJA_TECH, so non-Yautja tech-authorized users should see the same skeleton-limb source line.");
                    Assert.That(dirtyOrdinaryText, Does.Not.Contain(dirtyLine),
                        "CMSS13 hides the skeleton-limb polish state from users without TRAIT_YAUTJA_TECH.");
                    Assert.That(peltText, Does.Not.Contain(peltDirtyLine),
                        "CMSS13 skeleton-limb polish examine text should not leak to non-skeleton trophies.");
                    Assert.That(polishedText, Does.Contain(polishedLine),
                        "CMSS13 shows a distinct line once the skeleton limb has been polished.");
                    Assert.That(polishedText, Does.Not.Contain(dirtyLine));
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, techUser, ordinaryUser, armBone, xenoPelt })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SkeletonLimbAttachmentRequiresYautjaTechLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var containers = entMan.System<SharedContainerSystem>();

            var ordinaryUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var techUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var ordinaryUniform = entMan.SpawnEntity("CMJumpsuitSPP", MapCoordinates.Nullspace);
            var techUniform = entMan.SpawnEntity("CMJumpsuitSPP", MapCoordinates.Nullspace);
            var ordinaryBone = entMan.SpawnEntity("CMUYautjaHumanLeftArmBoneTrophy", MapCoordinates.Nullspace);
            var techBone = entMan.SpawnEntity("CMUYautjaHumanRightArmBoneTrophy", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(techUser);

                var ordinaryInteract = new InteractUsingEvent(
                    ordinaryUser,
                    ordinaryBone,
                    ordinaryUniform,
                    entMan.GetComponent<TransformComponent>(ordinaryUniform).Coordinates);
                entMan.EventBus.RaiseLocalEvent(ordinaryUniform, ordinaryInteract);

                var techInteract = new InteractUsingEvent(
                    techUser,
                    techBone,
                    techUniform,
                    entMan.GetComponent<TransformComponent>(techUniform).Coordinates);
                entMan.EventBus.RaiseLocalEvent(techUniform, techInteract);

                Assert.That(containers.TryGetContainer(
                    techUniform,
                    entMan.GetComponent<UniformAccessoryHolderComponent>(techUniform).ContainerId,
                    out var techContainer), Is.True);
                Assert.That(containers.TryGetContainer(
                    ordinaryUniform,
                    entMan.GetComponent<UniformAccessoryHolderComponent>(ordinaryUniform).ContainerId,
                    out var ordinaryContainer), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(ordinaryInteract.Handled, Is.True,
                        "CMSS13 /obj/item/clothing/accessory/limb/skeleton/can_attach_to() handles non-tech attachment attempts with a denial notice.");
                    Assert.That(ordinaryContainer!.ContainedEntities, Does.Not.Contain(ordinaryBone),
                        "Non-tech users must not attach skeleton-limb trophies to clothing.");
                    Assert.That(techInteract.Handled, Is.True,
                        "CMSS13 permits TRAIT_YAUTJA_TECH users to attach skeleton-limb trophies to clothing.");
                    Assert.That(techContainer!.ContainedEntities, Does.Contain(techBone),
                        "A Yautja-tech user should attach the skeleton limb through the regular uniform accessory container.");
                });
            }
            finally
            {
                foreach (var uid in new[] { ordinaryUser, techUser, ordinaryUniform, techUniform, ordinaryBone, techBone })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
