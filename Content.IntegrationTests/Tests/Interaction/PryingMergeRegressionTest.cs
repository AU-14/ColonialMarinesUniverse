using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Power.EntitySystems;
using Content.Server._RMC14.Power;
using Content.Shared._RMC14.Doors;
using Content.Shared._RMC14.Prying;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Prying.Components;
using Content.Shared.Prying.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using DoAfterData = Content.Shared.DoAfter.DoAfter;

namespace Content.IntegrationTests.Tests.Interaction;

[TestFixture]
[TestOf(typeof(PryingSystem))]
public sealed class PryingMergeRegressionTest : GameTest
{
    private const string ToolSound = "/Audio/Effects/falling.ogg";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: RMCPodDoor
          id: PryingMergePodDoor
          components:
          - type: Door
            pryTime: 2
          - type: PryUnpowered
          - type: PryingMergeProbe

        - type: entity
          parent: RMCPodDoorAlmayerOpen
          id: PryingMergeOpenPodDoor
          components:
          - type: PryingMergeProbe

        - type: entity
          parent: Crowbar
          id: PryingMergeTool
          components:
          - type: Prying
            speedModifier: 4
            useSound:
              path: /Audio/Effects/falling.ogg

        - type: entity
          id: PryingMergeAlertTool
          components:
          - type: Alerts
          - type: Prying
        """;

    [Test]
    public async Task XenoPodlockStartDuplicateAndMovementCancelOwnTheirSound()
    {
        var map = await Pair.CreateTestMap();
        EntityUid xeno = default;
        EntityUid door = default;
        EntityUid openDoor = default;
        DoAfterId? movementId = null;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var prying = Server.System<PryingSystem>();
                var verbs = Server.System<SharedVerbSystem>();
                xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
                door = SEntMan.SpawnEntity("PryingMergePodDoor", map.GridCoords);
                openDoor = SEntMan.SpawnEntity("PryingMergeOpenPodDoor", map.GridCoords);
                var xenoPrying = SEntMan.GetComponent<PryingComponent>(xeno);
                var pod = SEntMan.GetComponent<RMCPodDoorComponent>(door);
                var doorComp = SEntMan.GetComponent<DoorComponent>(door);
                var probe = SEntMan.GetComponent<PryingMergeProbeComponent>(door);

                var closedVerbs = verbs.GetLocalVerbs(door, xeno, typeof(AlternativeVerb), force: true);
                var openVerbs = verbs.GetLocalVerbs(openDoor, xeno, typeof(AlternativeVerb), force: true);
                Assert.Multiple(() =>
                {
                    Assert.That(closedVerbs.Any(verb => verb.Text == Loc.GetString("door-pry")), Is.True);
                    Assert.That(openVerbs.Any(verb => verb.Text == Loc.GetString("door-pry")), Is.False,
                        "the alternative verb must apply the same CanPry gate as execution");
                    Assert.That(prying.TryPry(openDoor, xeno, out var openId, xeno), Is.True);
                    Assert.That(openId, Is.Null,
                        "an open RMC poddoor is handled but must not enter a pry do-after");
                    Assert.That(SEntMan.GetComponent<DoorComponent>(openDoor).SoundEntity, Is.Null);
                });

                Assert.That(prying.TryPry(door, xeno, out var firstId, xeno), Is.True);
                Assert.That(firstId, Is.Not.Null);
                var active = Active(firstId!.Value);
                Assert.Multiple(() =>
                {
                    Assert.That(active.Args.Delay,
                        Is.EqualTo(doorComp.PryTime * pod.XenoPodlockPryMultiplier / xenoPrying.SpeedModifier));
                    Assert.That(active.Args.BreakOnDamage, Is.False);
                    Assert.That(active.Args.BreakOnMove, Is.True);
                    Assert.That(active.Args.ForceVisible, Is.False);
                    Assert.That(active.Args.NeedHand, Is.False,
                        "the xeno/user-as-tool path must not require a selected hand");
                    Assert.That(probe.Started, Is.EqualTo(1));
                    Assert.That(doorComp.SoundEntity, Is.Not.Null);
                });

                Assert.That(prying.TryPry(door, xeno, out var duplicateId, xeno), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(duplicateId, Is.Null,
                        "the default same-event duplicate blocks the second pry");
                    Assert.That(probe.Started, Is.EqualTo(1),
                        "RMCDoorPryEvent must only be emitted after a successful TryStartDoAfter");
                    Assert.That(probe.Cancelled, Is.EqualTo(1));
                    Assert.That(doorComp.SoundEntity, Is.Null,
                        "cancel-existing/block-new must stop and clear the first pry stream");
                });

                Assert.That(prying.TryPry(door, xeno, out movementId, xeno), Is.True);
                Assert.That(movementId, Is.Not.Null);
                Assert.That(probe.Started, Is.EqualTo(2));
                Assert.That(doorComp.SoundEntity, Is.Not.Null);
                Server.System<SharedTransformSystem>().SetCoordinates(
                    xeno,
                    map.GridCoords.Offset(new Vector2(1, 0)));
            });

            await Server.WaitRunTicks(2);
            await Server.WaitAssertion(() =>
            {
                var doorComp = SEntMan.GetComponent<DoorComponent>(door);
                var probe = SEntMan.GetComponent<PryingMergeProbeComponent>(door);
                Assert.Multiple(() =>
                {
                    Assert.That(Server.System<SharedDoAfterSystem>().GetStatus(movementId), Is.EqualTo(DoAfterStatus.Cancelled));
                    Assert.That(probe.Cancelled, Is.EqualTo(2));
                    Assert.That(doorComp.SoundEntity, Is.Null,
                        "BreakOnMove cancellation must stop the exact SoundEntity rather than leaving it audible");
                });
            });
        }
        finally
        {
            await Delete(xeno, door, openDoor);
        }
    }

    [Test]
    public async Task ToolAndBareHandGatesRecheckAtCompletionAndPreserveAlerts()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;
        EntityUid noPrying = default;
        EntityUid tool = default;
        EntityUid alertTool = default;
        EntityUid door = default;
        EntityUid bareDoor = default;
        DoAfterId? toolId = null;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var prying = Server.System<PryingSystem>();
                var alerts = Server.System<AlertsSystem>();
                var hands = Server.System<SharedHandsSystem>();
                human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                noPrying = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                tool = SEntMan.SpawnEntity("PryingMergeTool", map.GridCoords);
                alertTool = SEntMan.SpawnEntity("PryingMergeAlertTool", map.GridCoords);
                door = SEntMan.SpawnEntity("PryingMergePodDoor", map.GridCoords);
                bareDoor = SEntMan.SpawnEntity("PryingMergePodDoor", map.GridCoords);

                Assert.Multiple(() =>
                {
                    Assert.That(alerts.IsShowingAlert(alertTool, "Prying"), Is.True,
                        "Prying startup must show its configured upstream alert");
                    Assert.That(SEntMan.HasComponent<RMCUserPryingRequiresToolComponent>(human), Is.True);
                    Assert.That(prying.TryPry(door, human, out var requiredId), Is.True);
                    Assert.That(requiredId, Is.Null,
                        "RMC base species must not start a bare-hand pry");
                    Assert.That(prying.TryPry(door, noPrying, out var missingId), Is.True);
                    Assert.That(missingId, Is.Null,
                        "an entity without PryingComponent cannot enter the bare-hand do-after");
                });

                SEntMan.RemoveComponent<PryingComponent>(alertTool);
                Assert.That(alerts.IsShowingAlert(alertTool, "Prying"), Is.False,
                    "Prying shutdown must clear the configured alert");

                Assert.That(hands.TryPickupAnyHand(human, tool, checkActionBlocker: false), Is.True);
                Assert.That(prying.TryPry(door, human, out toolId, tool), Is.True);
                Assert.That(toolId, Is.Not.Null);
                var active = Active(toolId!.Value);
                Assert.Multiple(() =>
                {
                    Assert.That(active.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(0.5)),
                        "ordinary tool timing is Door.PryTime divided by Prying.SpeedModifier");
                    Assert.That(active.Args.BreakOnDamage, Is.False);
                    Assert.That(active.Args.BreakOnMove, Is.True);
                    Assert.That(active.Args.ForceVisible, Is.False);
                    Assert.That(active.Args.NeedHand, Is.True);
                });

                var toolComp = SEntMan.GetComponent<PryingComponent>(tool);
                toolComp.Enabled = false;
                var audioBefore = SoundCount(ToolSound);
                InvokeCompletion(door, human, tool);
                var doorProbe = SEntMan.GetComponent<PryingMergeProbeComponent>(door);
                Assert.Multiple(() =>
                {
                    Assert.That(doorProbe.Pried, Is.Zero,
                        "a MultipleTool mode flip disabling the original distinct tool must fail terminal validation");
                    Assert.That(doorProbe.Cancelled, Is.EqualTo(1));
                    Assert.That(SoundCount(ToolSound), Is.EqualTo(audioBefore),
                        "terminal validation must precede the tool use sound");
                });

                toolComp.Enabled = true;
                InvokeCompletion(door, human, tool);
                Assert.Multiple(() =>
                {
                    Assert.That(doorProbe.Pried, Is.EqualTo(1));
                    Assert.That(SoundCount(ToolSound), Is.EqualTo(audioBefore + 1),
                        "a valid completion retains the upstream Prying.UseSound path");
                });

                var doAfters = SEntMan.GetComponent<DoAfterComponent>(human);
                Server.System<SharedDoAfterSystem>().Cancel(toolId, doAfters);
                Assert.That(Active(toolId.Value).Cancelled, Is.True,
                    "the manually replayed completion must not leave its real tool do-after active");
            });

            await Server.WaitRunTicks(20);
            await Server.WaitAssertion(() =>
            {
                var doAfters = SEntMan.GetComponent<DoAfterComponent>(human);
                var doAfterSystem = Server.System<SharedDoAfterSystem>();
                Assert.That(doAfterSystem.GetStatus(toolId, doAfters), Is.EqualTo(DoAfterStatus.Invalid),
                    "the cancelled tool do-after must leave the duplicate set before starting the bare pry");

                var hands = Server.System<SharedHandsSystem>();
                SEntMan.RemoveComponent<RMCUserPryingRequiresToolComponent>(human);
                SEntMan.EnsureComponent<PryingComponent>(human).SpeedModifier = 2;
                Assert.That(hands.TryDrop(human, tool, checkActionBlocker: false), Is.True);
                Assert.That(hands.IsHolding(human, tool), Is.False,
                    "the bare-hand branch must not retain the crowbar in another hand");

                Server.System<PowerReceiverSystem>().SetPowerDisabled(bareDoor, true);
            });

            await Server.WaitRunTicks(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<RMCPowerSystem>().IsPowered(bareDoor), Is.False,
                    "the upstream bare-hand path only applies to an unpowered door");

                var prying = Server.System<PryingSystem>();
                Assert.That(prying.TryPry(bareDoor, human, out var bareId), Is.True);
                Assert.That(bareId, Is.Not.Null);
                var bare = Active(bareId!.Value);
                Assert.Multiple(() =>
                {
                    Assert.That(bare.Args.ForceVisible, Is.True);
                    Assert.That(bare.Args.BreakOnDamage, Is.False);
                    Assert.That(bare.Args.BreakOnMove, Is.True);
                    Assert.That(bare.Args.Used, Is.Null);
                });
                InvokeCompletion(bareDoor, human, null);
                Assert.That(SEntMan.GetComponent<PryingMergeProbeComponent>(bareDoor).Pried, Is.EqualTo(1),
                    "the null/bare-hand path remains a valid terminal pry route");

                InvokeCompletion(bareDoor, human, null, nullTarget: true);
                Assert.That(SEntMan.GetComponent<PryingMergeProbeComponent>(bareDoor).Cancelled, Is.EqualTo(1),
                    "a null target terminal path must emit the cancelled door-pry event");
            });
        }
        finally
        {
            await Delete(human, noPrying, tool, alertTool, door, bareDoor);
        }
    }

    private DoAfterData Active(DoAfterId id)
    {
        return SEntMan.GetComponent<DoAfterComponent>(id.Uid).DoAfters[id.Index];
    }

    private void InvokeCompletion(EntityUid door, EntityUid user, EntityUid? used, bool nullTarget = false)
    {
        EntityUid? target = nullTarget ? null : door;
        var ev = new DoorPryDoAfterEvent();
        var args = new DoAfterArgs(SEntMan, user, TimeSpan.Zero, ev, door, target, used);
        ev.DoAfter = new Content.Shared.DoAfter.DoAfter(0, args, TimeSpan.Zero);
        typeof(PryingSystem)
            .GetMethod("OnDoAfter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Server.System<PryingSystem>(), new object[]
            {
                door,
                SEntMan.GetComponent<DoorComponent>(door),
                ev
            });
    }

    private int SoundCount(string path)
    {
        return SEntMan.EntityQuery<AudioComponent>().Count(component => component.FileName == path);
    }

    private async Task Delete(params EntityUid[] entities)
    {
        foreach (var uid in entities)
        {
            if (SEntMan.EntityExists(uid))
                await Pair.DeleteEntityTreeLeafFirst(uid);
        }
    }
}

[RegisterComponent]
public sealed partial class PryingMergeProbeComponent : Component
{
    public int Started;
    public int Cancelled;
    public int Pried;
}

public sealed class PryingMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PryingMergeProbeComponent, RMCDoorPryEvent>(OnDoorPry);
        SubscribeLocalEvent<PryingMergeProbeComponent, PriedEvent>(OnPried);
    }

    private static void OnDoorPry(Entity<PryingMergeProbeComponent> ent, ref RMCDoorPryEvent args)
    {
        if (args.Cancelled)
            ent.Comp.Cancelled++;
        else
            ent.Comp.Started++;
    }

    private static void OnPried(Entity<PryingMergeProbeComponent> ent, ref PriedEvent args)
    {
        ent.Comp.Pried++;
    }
}
