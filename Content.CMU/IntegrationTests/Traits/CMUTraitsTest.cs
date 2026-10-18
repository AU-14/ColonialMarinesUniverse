using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Hearing;
using Content.Shared.CMU14.Traits.IronTeeth;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Traits;

[TestFixture]
public sealed class CMUTraitsTest : GameTest
{
    [Test]
    public async Task BadLegIsKeptOffMilitaryAndLawEnforcement()
    {
        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();

            var roles = SEntMan.System<SharedRoleSystem>();

            bool Bars(string job) => roles.GetRoleRequirements(prototypes.Index<JobPrototype>(job))?
                .OfType<TraitsRequirement>()
                .Any(requirement => requirement.Inverted && requirement.Traits.Contains("CMUBadLeg")) == true;

            Assert.Multiple(() =>
            {
                Assert.That(Bars("AU14JobGOVFORSquadRifleman"), Is.True);
                Assert.That(Bars("AU14JobOPFORSquadRifleman"), Is.True);
                Assert.That(Bars("AU14JobCivilianCMBMarshal"), Is.True);
                Assert.That(Bars("CMUJobINDFORProvostEnforcer"), Is.True);
                Assert.That(Bars("AU14JobCivilianJournalist"), Is.False);
            });
        });
    }

    [Test]
    public async Task MouthItemsFallOutUnlessIronTeeth()
    {
        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();

            var plain = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var iron = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            SEntMan.AddComponent<CMUIronTeethComponent>(iron);

            foreach (var mob in new[] { plain, iron })
            {
                var knife = SEntMan.SpawnEntity("RMCCombatUtilityKnifeA", MapCoordinates.Nullspace);
                Assert.That(inventory.TryEquip(mob, knife, "mask", silent: true, force: true), Is.True);
                SEntMan.AddComponent<CMUMuffledHearingComponent>(mob);
            }

            Assert.Multiple(() =>
            {
                Assert.That(inventory.TryGetSlotEntity(plain, "mask", out _), Is.False,
                    "a knife in the teeth falls out when they go down");
                Assert.That(inventory.TryGetSlotEntity(iron, "mask", out _), Is.True,
                    "iron teeth keep hold of it");
            });

            SEntMan.DeleteEntity(plain);
            SEntMan.DeleteEntity(iron);
        });
    }
}
