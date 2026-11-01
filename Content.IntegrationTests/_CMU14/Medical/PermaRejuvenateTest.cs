using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Administration.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class PermaRejuvenateTest : InteractionTest
{
    protected override string PlayerPrototype => "CMMobHuman";

    // playtest: aheal a perma'd marine, kill them again, and they were perma on the spot
    [Test]
    public async Task RejuvenateClearsPermaSoTheNextDeathGetsAFreshWindow()
    {
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Dead);
            SEntMan.System<RMCUnrevivableSystem>().MakeUnrevivable(SPlayer);
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(SPlayer), Is.True);

            SEntMan.System<RejuvenateSystem>().PerformRejuvenate(SPlayer);
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(SPlayer), Is.False);
            Assert.That(SEntMan.System<MobStateSystem>().IsDead(SPlayer), Is.False);

            SEntMan.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Dead);
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(SPlayer), Is.False);
            Assert.That(SEntMan.GetComponent<RMCRevivableComponent>(SPlayer).UnrevivableAt, Is.Not.Null);
        });
    }

    // the DNR trait is a character choice, not a death state
    [Test]
    public async Task RejuvenateKeepsTheNonResuscitableTrait()
    {
        await Server.WaitAssertion(() =>
        {
            var trait = SEntMan.EnsureComponent<UnrevivableComponent>(SPlayer);
            trait.Cloneable = true;

            SEntMan.System<RejuvenateSystem>().PerformRejuvenate(SPlayer);
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(SPlayer), Is.True);
        });
    }
}
