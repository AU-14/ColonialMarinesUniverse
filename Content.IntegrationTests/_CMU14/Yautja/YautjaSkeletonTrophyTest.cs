using Content.Server.Examine;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaSkeletonTrophyTest
{

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
