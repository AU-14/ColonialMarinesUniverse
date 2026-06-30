using Content.Shared.Clothing;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.Roles;

[TestFixture]
public sealed class WeaponSpecialistLoadoutTest
{
    private static readonly ProtoId<JobPrototype> GovforWeaponsSpecialist = "AU14JobGOVFORWeaponsSpecialist";
    private const string GovforWeaponsSpecialistLoadout = "JobAU14JobGOVFORWeaponsSpecialist";

    [Test]
    public async Task GovforWeaponsSpecialistResolvesRoleLoadout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var (key, proto) = LoadoutSystem.GetJobLoadoutInfo(GovforWeaponsSpecialist, prototypes);

            Assert.Multiple(() =>
            {
                Assert.That(key, Is.EqualTo(GovforWeaponsSpecialistLoadout));
                Assert.That(proto, Is.Not.Null);
                Assert.That(proto?.ID, Is.EqualTo(GovforWeaponsSpecialistLoadout));
                Assert.That(proto?.Points, Is.EqualTo(7));
            });
        });

        await pair.CleanReturnAsync();
    }
}
