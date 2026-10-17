#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Chat;
using Content.Shared.Chasm;
using Content.Shared.Mobs.Components;
using Content.Shared.Speech.Components;
using Robust.Shared.Audio.Components;

namespace Content.IntegrationTests.Tests.Chasm;

[TestFixture]
[TestOf(typeof(RequisitionsSystem))]
public sealed class RequisitionsChasmFallRegressionTest : GameTest
{
    internal const string FallingSound = "/Audio/Effects/falling.ogg";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: RequisitionsChasmFallElevator
          components:
          - type: Chasm
            fallingSound:
              path: /Audio/Effects/falling.ogg
            emote: Scream
          - type: RequisitionsElevator
            mode: Raised
            radius: 0.1
          - type: RequisitionsChasmFallProbe
        """;

    [Test]
    public async Task LoweredElevatorPreservesTripperEventsAndManualSoundWithoutNewEmote()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;

        try
        {
            await Server.WaitAssertion(() =>
            {
                _ = Server.System<RequisitionsChasmFallProbeSystem>();
                var requisitions = Server.System<RequisitionsSystem>();
                var elevator = SEntMan.SpawnEntity("RequisitionsChasmFallElevator", map.GridCoords);
                var tripper = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(session, tripper);

                Assert.That(SEntMan.HasComponent<MobStateComponent>(tripper), Is.True,
                    "the requisitions lookup only considers mobs");

                SEntMan.RemoveComponent<EmoteCooldownComponent>(tripper);
                SEntMan.EnsureComponent<EmoteCooldownComponent>(tripper);

                var chasmProbe = SEntMan.GetComponent<RequisitionsChasmFallProbeComponent>(elevator);
                var tripperProbe = SEntMan.EnsureComponent<RequisitionsChasmFallProbeComponent>(tripper);
                var soundBaseline = SoundCount();
                chasmProbe.SoundBaseline = soundBaseline;
                tripperProbe.SoundBaseline = soundBaseline;

                var elevatorComp = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
                elevatorComp.Mode = RequisitionsElevatorMode.Lowered;
                elevatorComp.NextChasmCheck = TimeSpan.Zero;
                requisitions.Update(0f);

                var falling = SEntMan.GetComponent<ChasmFallingComponent>(tripper);
                Assert.Multiple(() =>
                {
                    Assert.That(falling.FallingInto, Is.EqualTo(elevator));
                    Assert.That(chasmProbe.StartedAsChasm, Is.EqualTo(1));
                    Assert.That(chasmProbe.Faller, Is.EqualTo(tripper));
                    Assert.That(tripperProbe.StartedAsFaller, Is.EqualTo(1));
                    Assert.That(tripperProbe.FallingInto, Is.EqualTo(elevator));
                    Assert.That(tripperProbe.Emotes, Is.Zero,
                        "the upstream Chasm emote is new behavior and must stay disabled for the elevator");
                    Assert.That(chasmProbe.SoundCountAtStart, Is.EqualTo(soundBaseline + 1),
                        "the predicted chasm sound must precede the start event");
                    Assert.That(tripperProbe.SoundCountAtStart, Is.EqualTo(soundBaseline + 1),
                        "the manual sound must not run until after both start events");
                    Assert.That(SoundCount(), Is.EqualTo(soundBaseline + 2),
                        "the pre-merge manual recipient sound must still follow StartFalling");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
        }
    }

    private int SoundCount()
    {
        return SEntMan.EntityQuery<AudioComponent>()
            .Count(component => component.FileName == FallingSound);
    }
}

[RegisterComponent]
public sealed partial class RequisitionsChasmFallProbeComponent : Component
{
    public int SoundBaseline;
    public int SoundCountAtStart;
    public int StartedAsChasm;
    public int StartedAsFaller;
    public int Emotes;
    public EntityUid? Faller;
    public EntityUid? FallingInto;
}

public sealed class RequisitionsChasmFallProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RequisitionsChasmFallProbeComponent, EntityStartedFallingIntoChasmEvent>(OnChasmStarted);
        SubscribeLocalEvent<RequisitionsChasmFallProbeComponent, StartedFallingIntoChasmEvent>(OnFallerStarted);
        SubscribeLocalEvent<RequisitionsChasmFallProbeComponent, EmoteEvent>(OnEmote);
    }

    private void OnChasmStarted(
        Entity<RequisitionsChasmFallProbeComponent> ent,
        ref EntityStartedFallingIntoChasmEvent args)
    {
        ent.Comp.StartedAsChasm++;
        ent.Comp.Faller = args.Faller.Owner;
        ent.Comp.SoundCountAtStart = SoundCount();
    }

    private void OnFallerStarted(
        Entity<RequisitionsChasmFallProbeComponent> ent,
        ref StartedFallingIntoChasmEvent args)
    {
        ent.Comp.StartedAsFaller++;
        ent.Comp.FallingInto = args.FallingInto.Owner;
        ent.Comp.SoundCountAtStart = SoundCount();
    }

    private static void OnEmote(Entity<RequisitionsChasmFallProbeComponent> ent, ref EmoteEvent args)
    {
        ent.Comp.Emotes++;
    }

    private int SoundCount()
    {
        return EntityQuery<AudioComponent>()
            .Count(component => component.FileName == RequisitionsChasmFallRegressionTest.FallingSound);
    }
}

#pragma warning restore RA0002
