#pragma warning disable RA0002 // Configure the resource's deadline and inspect replicated committed state.
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class RevivablePauseLifecycleTest : InteractionTest
{
    protected override string PlayerPrototype => "CMMobHuman";

    [TestCase(false)]
    [TestCase(true)]
    public async Task PausingALivingOrRejuvenatedPatientCannotCreateADeathDeadline(bool rejuvenated)
    {
        await Server.WaitAssertion(() =>
        {
            if (rejuvenated)
            {
                SEntMan.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Dead);
                Assert.That(SEntMan.GetComponent<RMCRevivableComponent>(SPlayer).UnrevivableAt, Is.Not.Null);
                SEntMan.System<RejuvenateSystem>().PerformRejuvenate(SPlayer);
            }
            Assert.That(SEntMan.GetComponent<RMCRevivableComponent>(SPlayer).UnrevivableAt, Is.Null);
            SEntMan.System<MetaDataSystem>().SetEntityPaused(SPlayer, true);
        });
        await RunSeconds(20);
        await Server.WaitPost(() => SEntMan.System<MetaDataSystem>().SetEntityPaused(SPlayer, false));
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<RMCRevivableComponent>(SPlayer).UnrevivableAt, Is.Null);
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(SPlayer), Is.False);
            Assert.That(SEntMan.System<RMCUnrevivableSystem>().GetUnrevivableStage(SPlayer, 4), Is.Zero);
        });
        await Client.WaitAssertion(() =>
            Assert.That(CEntMan.GetComponent<RMCRevivableComponent>(CPlayer).UnrevivableAt, Is.Null));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RevivalWindowUsesFrozenTimeWhenDeathPrecedesOrOccursDuringPause(bool diesDuringPause)
    {
        var stageBefore = 0;
        await Server.WaitAssertion(() =>
        {
            SEntMan.GetComponent<RMCRevivableComponent>(SPlayer).UnrevivableDelay = TimeSpan.FromSeconds(10);
            if (!diesDuringPause)
                SEntMan.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Dead);
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            stageBefore = SEntMan.System<RMCUnrevivableSystem>().GetUnrevivableStage(SPlayer, 4);
            SEntMan.System<MetaDataSystem>().SetEntityPaused(SPlayer, true);
        });
        await RunSeconds(12);
        await Server.WaitAssertion(() =>
        {
            if (diesDuringPause)
                SEntMan.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Dead);
            Assert.That(SEntMan.System<RMCUnrevivableSystem>().GetUnrevivableStage(SPlayer, 4),
                Is.EqualTo(stageBefore), "Frozen time cannot age an existing or newly created death window.");
        });
        await RunSeconds(8);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(SPlayer), Is.False);
            Assert.That(SEntMan.System<RMCUnrevivableSystem>().GetUnrevivableStage(SPlayer, 4), Is.EqualTo(stageBefore));
            SEntMan.System<MetaDataSystem>().SetEntityPaused(SPlayer, false);
        });
        await RunSeconds(5);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<UnrevivableComponent>(SPlayer), Is.False));
        await RunSeconds(6);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<UnrevivableComponent>(SPlayer), Is.True));
    }

    [Test]
    public async Task PausingADeadPatientPreservesTheActualRemainingRevivalWindow()
    {
        TimeSpan original = default;
        await Server.WaitAssertion(() =>
        {
            var revivable = SEntMan.GetComponent<RMCRevivableComponent>(SPlayer);
            revivable.UnrevivableDelay = TimeSpan.FromSeconds(10);
            SEntMan.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Dead);
            original = revivable.UnrevivableAt!.Value;
        });
        await RunSeconds(3);
        await Server.WaitPost(() => SEntMan.System<MetaDataSystem>().SetEntityPaused(SPlayer, true));
        await RunSeconds(20);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(SPlayer), Is.False);
            SEntMan.System<MetaDataSystem>().SetEntityPaused(SPlayer, false);
            Assert.That(SEntMan.GetComponent<RMCRevivableComponent>(SPlayer).UnrevivableAt,
                Is.GreaterThan(original + TimeSpan.FromSeconds(19)));
        });
        await RunSeconds(5);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<UnrevivableComponent>(SPlayer), Is.False));
        await RunSeconds(4);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<UnrevivableComponent>(SPlayer), Is.True));
    }
}
