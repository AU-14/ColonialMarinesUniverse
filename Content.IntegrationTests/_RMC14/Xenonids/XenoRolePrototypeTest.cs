using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Xenonids;

[TestFixture]
public sealed class XenoRolePrototypeTest : GameTest
{
    [Test]
    public async Task XenoJobPrototypesExist()
    {
        var prototypeManager = Pair.Server.ResolveDependency<IPrototypeManager>();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (prototype, xeno) in Pair.GetPrototypesWithComponent<XenoComponent>())
                {
                    Assert.That(prototypeManager.HasIndex<JobPrototype>(xeno.Role),
                        $"Xeno entity {prototype.ID} references missing job prototype {xeno.Role}.");
                }
            });
        });
    }
}
