using Content.IntegrationTests.Fixtures;
using Content.Server.Mind;
using Content.Shared._RMC14.Medical.Examine;
using Content.Shared.Examine;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Minds;

[TestFixture]
[TestOf(typeof(MindExamineSystem))]
public sealed class MindExamineMergeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
    };

    [Test]
    public async Task RmcSuppressesOnlyConnectedDeadGenericLine()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        var dummy = await Server.AddDummySession($"MindExamineDisconnected{Guid.NewGuid():N}");
        var dummyRemoved = false;
        EntityUid hostMind = default;
        EntityUid? originalOwned = null;
        EntityUid examiner = default;
        EntityUid ordinary = default;
        EntityUid rmc = default;
        EntityUid disconnectedRmc = default;
        EntityUid mindlessRmc = default;
        EntityUid disconnectedMind = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var mindSystem = Server.System<MindSystem>();
                var mobState = Server.System<MobStateSystem>();
                var connectedMind = mindSystem.GetMind(session.UserId);
                Assert.That(connectedMind, Is.Not.Null, "the connected integration session must own a mind");
                hostMind = connectedMind!.Value;
                originalOwned = SEntMan.GetComponent<MindComponent>(hostMind).OwnedEntity;

                examiner = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                ordinary = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                rmc = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                disconnectedRmc = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                mindlessRmc = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

                mindSystem.TransferTo(hostMind, ordinary);
                mobState.ChangeMobState(ordinary, MobState.Dead);
                Assert.That(State(ordinary), Is.EqualTo(MindState.Dead));
                var ordinaryDead = Examine(ordinary, examiner);
                Assert.That(ordinaryDead,
                    Does.Contain(Loc.GetString("comp-mind-examined-dead", ("ent", ordinary))),
                    "ordinary connected dead species retain the upstream generic red line");

                mindSystem.TransferTo(hostMind, rmc);
                mobState.ChangeMobState(rmc, MobState.Dead);
                Assert.That(State(rmc), Is.EqualTo(MindState.Dead));
                var rmcDead = Examine(rmc, examiner);
                Assert.Multiple(() =>
                {
                    Assert.That(rmcDead,
                        Does.Contain(Loc.GetString("rmc-medical-examine-dead", ("victim", rmc))),
                        "RMC connected dead bodies retain their own medical death text");
                    Assert.That(rmcDead,
                        Does.Not.Contain(Loc.GetString("comp-mind-examined-dead", ("ent", rmc))),
                        "RMCMedicalExamine suppresses only the duplicate generic connected-dead line");
                });

                disconnectedMind = mindSystem.CreateMind(dummy.UserId, "Disconnected mind");
                mindSystem.TransferTo(disconnectedMind, disconnectedRmc);
                Assert.That(State(disconnectedRmc), Is.EqualTo(MindState.None),
                    "the dummy begins as a genuinely connected mind before disconnection");

                Assert.That(State(mindlessRmc), Is.EqualTo(MindState.Catatonic));
                var catatonic = Examine(mindlessRmc, examiner);
                Assert.That(catatonic,
                    Does.Contain(Loc.GetString("comp-mind-examined-catatonic", ("ent", mindlessRmc))),
                    "RMCMedicalExamine must not suppress the upstream catatonic status");
            });

            await Server.RemoveDummySession(dummy);
            dummyRemoved = true;

            await Server.WaitAssertion(() =>
            {
                var mobState = Server.System<MobStateSystem>();
                Assert.That(State(disconnectedRmc), Is.EqualTo(MindState.SSD));
                var ssd = Examine(disconnectedRmc, examiner);
                Assert.That(ssd,
                    Does.Contain(Loc.GetString("comp-mind-examined-ssd", ("ent", disconnectedRmc))),
                    "RMCMedicalExamine must not suppress the upstream living SSD status");

                mobState.ChangeMobState(disconnectedRmc, MobState.Dead);
                Assert.That(State(disconnectedRmc), Is.EqualTo(MindState.DeadSSD));
                var deadSsd = Examine(disconnectedRmc, examiner);
                Assert.Multiple(() =>
                {
                    Assert.That(deadSsd,
                        Does.Contain(Loc.GetString("comp-mind-examined-dead-and-ssd", ("ent", disconnectedRmc))));
                    Assert.That(deadSsd,
                        Does.Contain(Loc.GetString("rmc-medical-examine-dead", ("victim", disconnectedRmc))));
                });

                mobState.ChangeMobState(mindlessRmc, MobState.Dead);
                Assert.That(State(mindlessRmc), Is.EqualTo(MindState.Irrecoverable));
                var irrecoverable = Examine(mindlessRmc, examiner);
                Assert.Multiple(() =>
                {
                    Assert.That(irrecoverable,
                        Does.Contain(Loc.GetString("comp-mind-examined-dead-and-irrecoverable", ("ent", mindlessRmc))));
                    Assert.That(irrecoverable,
                        Does.Contain(Loc.GetString("rmc-medical-examine-dead", ("victim", mindlessRmc))));
                });
            });
        }
        finally
        {
            if (!dummyRemoved)
                await Server.RemoveDummySession(dummy);

            await Server.WaitPost(() =>
            {
                var mindSystem = Server.System<MindSystem>();
                if (hostMind.IsValid())
                {
                    if (originalOwned is { } owned && SEntMan.EntityExists(owned))
                        mindSystem.TransferTo(hostMind, owned);
                    else
                        mindSystem.TransferTo(hostMind, null, createGhost: false);
                }

                Server.PlayerMan.SetAttachedEntity(session, originalAttached);

                if (disconnectedMind.IsValid())
                {
                    mindSystem.WipeMind(disconnectedMind);
                    SEntMan.DeleteEntity(disconnectedMind);
                }

                Delete(mindlessRmc);
                Delete(disconnectedRmc);
                Delete(rmc);
                Delete(ordinary);
                Delete(examiner);
            });
        }
    }

    private MindState State(EntityUid uid)
    {
        return SEntMan.GetComponent<MindExaminableComponent>(uid).State;
    }

    private string Examine(EntityUid target, EntityUid examiner)
    {
        var examined = new ExaminedEvent(new FormattedMessage(), target, examiner, true, false);
        SEntMan.EventBus.RaiseLocalEvent(target, examined);
        return examined.GetTotalMessage().ToMarkup();
    }

    private void Delete(EntityUid uid)
    {
        if (uid.IsValid())
            SEntMan.DeleteEntity(uid);
    }
}
