using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions;
using Content.Shared.CMU14.Marines.Orders;
using Content.Shared.Roles;

namespace Content.IntegrationTests.CMU14.Round;

[TestFixture]
public sealed class SilenceOrderRoleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase("AU14JobGOVFORPlatCo")]
    [TestCase("AU14JobOPFORPlatCo")]
    [TestCase("AU14JobGOVFORPlatCoCMBCIU")]
    [TestCase("AU14JobGOVFORPlatCoRMC")]
    [TestCase("AU14JobGOVFORPlatCoUPP")]
    [TestCase("AU14JobGOVFORPlatCoWYPMC")]
    public async Task PlatoonCommanderGetsSilenceAction(string jobId)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var commander = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                SEntMan.AddComponents(commander, SProtoMan.Index<JobPrototype>(jobId).RoundComponents);

                var ability = SEntMan.GetComponent<AU14SilenceOrderAbilityComponent>(commander);
                Assert.That(ability.SilenceActionEntity, Is.Not.Null, $"{jobId} was not granted the Silence action.");

                var actions = SEntMan.System<SharedActionsSystem>();
                Assert.That(actions.GetActions(commander).Any(a => a.Owner == ability.SilenceActionEntity), Is.True,
                    $"{jobId} has the Silence action entity but it is not in their action list.");
            }
            finally
            {
                SEntMan.DeleteEntity(commander);
            }
        });
    }
}
