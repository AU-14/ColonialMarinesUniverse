using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Chasm;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Chasm;

[TestFixture]
[TestOf(typeof(ChasmSystem))]
public sealed class ChasmMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: ChasmMergeHole
          components:
          - type: Chasm
          - type: ChasmMergeProbe
        """;

    [Test]
    public async Task ChasmPrototypeBlocksWeedSpread()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var chasm = SEntMan.SpawnEntity("FloorChasmEntity", map.GridCoords);

            Assert.That(SEntMan.HasComponent<BlockWeedsComponent>(chasm), Is.True,
                "chasm hazards must prevent weeds from spreading onto their tile");
        });
    }

    [Test]
    public async Task QueenDeathPrecedesCompletionAndSourcelessFallSkipsCompletionEvents()
    {
        var map = await Pair.CreateTestMap();
        EntityUid queen = default;
        EntityUid hive = default;
        EntityUid sourceless = default;

        await Server.WaitPost(() =>
        {
            var entities = SEntMan;
            var chasms = Server.System<ChasmSystem>();
            var hives = Server.System<SharedXenoHiveSystem>();
            var timing = Server.ResolveDependency<IGameTiming>();
            var probe = Server.System<ChasmMergeProbeSystem>();

            var chasm = entities.SpawnEntity("ChasmMergeHole", map.GridCoords);
            hive = entities.SpawnEntity("CMXenoHive", map.GridCoords);
            queen = entities.SpawnEntity("CMXenoQueen", map.GridCoords);
            entities.AddComponent<ChasmMergeProbeComponent>(queen);
            entities.AddComponent<ChasmMergeProbeComponent>(hive);
            hives.SetHive(queen, hive);

            Assert.That(entities.GetComponent<HiveComponent>(hive).CurrentQueen, Is.EqualTo(queen));

            sourceless = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.AddComponent<ChasmMergeProbeComponent>(sourceless);
            var sourcelessFalling = entities.AddComponent<ChasmFallingComponent>(sourceless);
            sourcelessFalling.NextDeletionTime = timing.CurTime;

            probe.Queen = queen;
            probe.Hive = hive;
            probe.Sourceless = sourceless;

            var falling = chasms.StartFalling(chasm, queen, playSound: false, playEmote: false);
            Assert.That(falling, Is.Not.Null);
            falling!.Value.Comp.NextDeletionTime = timing.CurTime;
        });

        await Server.WaitRunTicks(2);
        await Server.WaitAssertion(() =>
        {
            var probe = Server.System<ChasmMergeProbeSystem>();
            var hiveComponent = SEntMan.GetComponent<HiveComponent>(hive);

            Assert.Multiple(() =>
            {
                Assert.That(probe.ChasmCompletionEvents, Is.EqualTo(1));
                Assert.That(probe.FallerCompletionEvents, Is.EqualTo(1));
                Assert.That(probe.QueenChangeEvents, Is.EqualTo(1),
                    "the authoritative death transition must clear the Queen exactly once");
                Assert.That(probe.QueenWasDeadAtChasmCompletion, Is.True);
                Assert.That(probe.QueenWasDeadAtFallerCompletion, Is.True);
                Assert.That(probe.HiveWasClearedAtChasmCompletion, Is.True);
                Assert.That(probe.HiveWasClearedAtFallerCompletion, Is.True);
                Assert.That(probe.LastQueenDeathWasSetAtChasmCompletion, Is.True);
                Assert.That(probe.LastQueenDeathWasSetAtFallerCompletion, Is.True);
                Assert.That(hiveComponent.CurrentQueen, Is.Null);
                Assert.That(hiveComponent.LastQueenDeath, Is.Not.Null);
                Assert.That(probe.TerminatedDead, Does.Contain(queen));
                Assert.That(probe.TerminatedDead, Does.Contain(sourceless),
                    "a source-less CMU fall must still complete its authoritative death transition");
                Assert.That(probe.CompletionFallers, Does.Not.Contain(sourceless),
                    "an invalid FallingInto UID must not be used as a completion-event target");
                Assert.That(SEntMan.Deleted(queen), Is.True);
                Assert.That(SEntMan.Deleted(sourceless), Is.True);
            });
        });
    }
}

[RegisterComponent]
public sealed partial class ChasmMergeProbeComponent : Component;

public sealed class ChasmMergeProbeSystem : EntitySystem
{
    public EntityUid Queen;
    public EntityUid Hive;
    public EntityUid Sourceless;
    public int ChasmCompletionEvents;
    public int FallerCompletionEvents;
    public int QueenChangeEvents;
    public bool QueenWasDeadAtChasmCompletion;
    public bool QueenWasDeadAtFallerCompletion;
    public bool HiveWasClearedAtChasmCompletion;
    public bool HiveWasClearedAtFallerCompletion;
    public bool LastQueenDeathWasSetAtChasmCompletion;
    public bool LastQueenDeathWasSetAtFallerCompletion;
    public readonly HashSet<EntityUid> CompletionFallers = [];
    public readonly HashSet<EntityUid> TerminatedDead = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChasmMergeProbeComponent, EntityCompletedFallingIntoChasmEvent>(OnChasmCompleted);
        SubscribeLocalEvent<ChasmMergeProbeComponent, CompletedFallingIntoChasmEvent>(OnFallerCompleted);
        SubscribeLocalEvent<ChasmMergeProbeComponent, XenoHiveQueenChangedEvent>(OnQueenChanged);
        SubscribeLocalEvent<ChasmMergeProbeComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnChasmCompleted(
        Entity<ChasmMergeProbeComponent> ent,
        ref EntityCompletedFallingIntoChasmEvent args)
    {
        ChasmCompletionEvents++;
        CompletionFallers.Add(args.Faller.Owner);
        QueenWasDeadAtChasmCompletion = IsDead(Queen);
        CaptureHiveState(out HiveWasClearedAtChasmCompletion, out LastQueenDeathWasSetAtChasmCompletion);
    }

    private void OnFallerCompleted(
        Entity<ChasmMergeProbeComponent> ent,
        ref CompletedFallingIntoChasmEvent args)
    {
        FallerCompletionEvents++;
        CompletionFallers.Add(ent.Owner);
        QueenWasDeadAtFallerCompletion = IsDead(Queen);
        CaptureHiveState(out HiveWasClearedAtFallerCompletion, out LastQueenDeathWasSetAtFallerCompletion);
    }

    private void OnQueenChanged(
        Entity<ChasmMergeProbeComponent> ent,
        ref XenoHiveQueenChangedEvent args)
    {
        if (ent.Owner == Hive && args.OldQueen == Queen && args.NewQueen == null)
            QueenChangeEvents++;
    }

    private void OnTerminating(Entity<ChasmMergeProbeComponent> ent, ref EntityTerminatingEvent args)
    {
        if ((ent.Owner == Queen || ent.Owner == Sourceless) && IsDead(ent.Owner))
            TerminatedDead.Add(ent.Owner);
    }

    private bool IsDead(EntityUid uid)
    {
        return TryComp(uid, out MobStateComponent? mobState) && mobState.CurrentState == MobState.Dead;
    }

    private void CaptureHiveState(out bool cleared, out bool deathSet)
    {
        var hive = Comp<HiveComponent>(Hive);
        cleared = hive.CurrentQueen == null;
        deathSet = hive.LastQueenDeath != null;
    }
}
