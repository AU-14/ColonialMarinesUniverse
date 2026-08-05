using Content.IntegrationTests.Fixtures;
using Content.Shared._CMU14.Compatibility;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.ZLevels;

[TestFixture]
public sealed class LegacyBushCompatibilityRegistrationTest : GameTest
{
    private static readonly (Type Type, string Name)[] OrganSnapshotComponents =
    [
        (typeof(OrganHealthComponent), "LegacyBushOrganHealth"),
        (typeof(HeartComponent), "LegacyBushHeart"),
        (typeof(KidneysComponent), "LegacyBushKidneys"),
        (typeof(LiverComponent), "LegacyBushLiver"),
        (typeof(LungsComponent), "LegacyBushLungs"),
        (typeof(CMUStomachComponent), "LegacyBushStomach"),
    ];

    [Test]
    public async Task OrganSnapshotComponentsUsePrivateRegistrationNames()
    {
        await Server.WaitAssertion(() => AssertRegistrationNames(
            Server.ResolveDependency<IComponentFactory>()));
        await Client.WaitAssertion(() => AssertRegistrationNames(
            Client.ResolveDependency<IComponentFactory>()));
    }

    private static void AssertRegistrationNames(IComponentFactory factory)
    {
        Assert.Multiple(() =>
        {
            foreach (var (type, expectedName) in OrganSnapshotComponents)
            {
                Assert.That(factory.GetRegistration(type).Name, Is.EqualTo(expectedName),
                    $"{type.Name} can collide with a gameplay component registration.");
            }
        });
    }
}
