using Content.IntegrationTests.Fixtures;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Loadouts;

[TestFixture]
public sealed class CMUPridePinAvailabilityTest
{
    [TestCase("JobPassenger")]
    [TestCase("JobRMCJobSynthetic")]
    [TestCase("JobAU14JobCivilianColonist")]
    [TestCase("JobAU14JobGOVFORAuxSupportSynth")]
    [TestCase("JobAU14JobCLFGuerilla")]
    public async Task EveryRoleFamilyOffersPridePins(string roleLoadoutId)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ProtoMan;
            var pins = new ProtoId<LoadoutGroupPrototype>("RMCLoadoutGroupPins");
            var roleLoadout = prototypes.Index<RoleLoadoutPrototype>(roleLoadoutId);
            Assert.That(roleLoadout.Groups, Does.Contain(pins), roleLoadoutId);
        });
        await pair.CleanReturnAsync();
    }
}
