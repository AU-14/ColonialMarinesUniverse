#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections;
using System.Reflection;
using Content.Client.Popups;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Stun;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(SharedFlashSystem))]
public sealed class FlashMergeRegressionTest : GameTest
{
    private static readonly EntProtoId Flashed = "StatusEffectFlashed";
    private static readonly EntProtoId FlashSlowdown = "FlashSlowdownStatusEffect";
    private static readonly EntProtoId ParalyzeId = SharedStunSystem.ParalyzeId;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMMobHuman
  id: FlashMergeTarget
  components:
  - type: FlashMergeProbe

- type: entity
  parent: FlashMergeTarget
  id: FlashMergeSynth
  components:
  - type: Synth
    initialized: true
  - type: FlashImmunity
    showInExamine: true

- type: entity
  parent: FlashMergeTarget
  id: FlashMergeMask
  components:
  - type: FlashImmunity
    showInExamine: true
  - type: Mask

- type: entity
  id: FlashMergeIneligible
  components:
  - type: FlashMergeProbe

- type: entity
  parent: FlashMergeTarget
  id: FlashMergeMarine
  components:
  - type: Marine

- type: entity
  parent: FlashMergeMarine
  id: FlashMergeSynthMarine
  components:
  - type: Synth
    initialized: true
  - type: FlashImmunity

- type: entity
  id: FlashMergeTrigger
  components:
  - type: RMCStunOnTrigger
    range: 3
    stun: 0
    paralyze: 0
    flash: 0.5
    flashAdditionalStunTime: 2

- type: entity
  id: FlashMergeUser
  name: flash regression user
";

    [Test]
    public async Task SynthAndMaskImmunityCoverExamineAndBothFlashOverloads()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var flash = Server.System<SharedFlashSystem>();
            var statuses = Server.System<StatusEffectsSystem>();
            var synth = SEntMan.SpawnEntity("FlashMergeSynth", map.GridCoords);
            var mask = SEntMan.SpawnEntity("FlashMergeMask", map.GridCoords);
            var ineligible = SEntMan.SpawnEntity("FlashMergeIneligible", map.GridCoords);
            var user = SEntMan.SpawnEntity("FlashMergeUser", map.GridCoords);

            try
            {
                AssertExamineProtection(synth, visible: false,
                    "Synth immunity must remain hidden even when ShowInExamine is true");
                AssertExamineProtection(mask, visible: true,
                    "ordinary flash protection must honor ShowInExamine");

                var synthProbe = SEntMan.GetComponent<FlashMergeProbeComponent>(synth);
                var synthTimeSpanBaseline = BeginFlashAction(synth);
                flash.Flash(synth, user, null, TimeSpan.FromMilliseconds(250), 0.6f, displayPopup: false);
                var synthFloatBaseline = BeginFlashAction(synth);
                var synthFloat = flash.Flash(synth, user, null, 375f, displayPopup: false);

                Assert.Multiple(() =>
                {
                    Assert.That(synthFloat, Is.False,
                        "the retained millisecond wrapper must report Synth immunity");
                    Assert.That(synthProbe.Attempts - synthTimeSpanBaseline.Attempts, Is.EqualTo(2),
                        "both TimeSpan and retained float overloads must pass through FlashAttempt");
                    Assert.That(synthProbe.Attempts - synthFloatBaseline.Attempts, Is.EqualTo(1));
                    Assert.That(synthProbe.AfterFlashed - synthTimeSpanBaseline.AfterFlashed, Is.Zero);
                    Assert.That(StatusCount(synth, Flashed), Is.Zero);
                    Assert.That(StatusCount(synth, FlashSlowdown), Is.Zero);
                    Assert.That(synthProbe.MovementRefreshes - synthTimeSpanBaseline.MovementRefreshes, Is.Zero);
                    Assert.That(synthProbe.Stunned - synthTimeSpanBaseline.Stunned, Is.Zero);
                });

                var maskProbe = SEntMan.GetComponent<FlashMergeProbeComponent>(mask);
                var protectedMaskBaseline = BeginFlashAction(mask);
                var protectedMask = flash.Flash(mask, user, null, 400f, displayPopup: false);
                var maskComponent = SEntMan.GetComponent<MaskComponent>(mask);
                maskComponent.IsToggled = true;
                var loweredMaskBaseline = BeginFlashAction(mask);
                var loweredMask = flash.Flash(mask, user, null, 400f, slowTo: 0.55f, displayPopup: false);

                Assert.Multiple(() =>
                {
                    Assert.That(protectedMask, Is.False);
                    Assert.That(loweredMask, Is.True,
                        "pulling down an ordinary protective mask must bypass FlashImmunity");
                    Assert.That(maskProbe.Attempts - protectedMaskBaseline.Attempts, Is.EqualTo(2));
                    Assert.That(maskProbe.Attempts - loweredMaskBaseline.Attempts, Is.EqualTo(1));
                    Assert.That(maskProbe.AfterFlashed - protectedMaskBaseline.AfterFlashed, Is.EqualTo(1));
                    Assert.That(StatusCount(mask, Flashed), Is.EqualTo(1));
                    Assert.That(StatusCount(mask, FlashSlowdown), Is.EqualTo(1));
                });
                AssertStatusDuration(statuses, mask, Flashed, TimeSpan.FromMilliseconds(400));
                AssertStatusDuration(statuses, mask, FlashSlowdown, TimeSpan.FromMilliseconds(400));

                var failedProbe = SEntMan.GetComponent<FlashMergeProbeComponent>(ineligible);
                var failedBaseline = BeginFlashAction(ineligible);
                var failedStatus = flash.Flash(ineligible, user, null, 625f, displayPopup: false);
                Assert.Multiple(() =>
                {
                    Assert.That(failedStatus, Is.False,
                        "the bool wrapper must report a rejected StatusEffectFlashed application");
                    Assert.That(failedProbe.Attempts - failedBaseline.Attempts, Is.EqualTo(1));
                    Assert.That(failedProbe.Events, Is.EqualTo(new[] { "attempt" }));
                    Assert.That(failedProbe.AfterFlashed - failedBaseline.AfterFlashed, Is.Zero);
                    Assert.That(StatusCount(ineligible, Flashed), Is.Zero);
                    Assert.That(StatusCount(ineligible, FlashSlowdown), Is.Zero);
                });
            }
            finally
            {
                SEntMan.DeleteEntity(synth);
                SEntMan.DeleteEntity(mask);
                SEntMan.DeleteEntity(ineligible);
                SEntMan.DeleteEntity(user);
            }
        });
    }

    [Test]
    public async Task FloatMillisecondsApplyEffectsBeforeOneAfterEventAndPopup()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid target = default;
        EntityUid user = default;
        NetEntity targetNet = default;
        string popup = string.Empty;

        try
        {
            await Server.WaitPost(() =>
            {
                target = SEntMan.SpawnEntity("FlashMergeTarget", map.GridCoords);
                user = SEntMan.SpawnEntity("FlashMergeUser", map.GridCoords);
                targetNet = SEntMan.GetNetEntity(target);
                Server.PlayerMan.SetAttachedEntity(session, target);
            });
            await Pair.RunUntilSynced();

            await Server.WaitAssertion(() =>
            {
                const float milliseconds = 1234f;
                const float slowTo = 0.42f;
                var flash = Server.System<SharedFlashSystem>();
                var statuses = Server.System<StatusEffectsSystem>();
                popup = Loc.GetString("flash-component-user-blinds-you",
                    ("user", Content.Shared.IdentityManagement.Identity.Entity(user, SEntMan)));
                var probe = SEntMan.GetComponent<FlashMergeProbeComponent>(target);
                var baseline = BeginFlashAction(target);

                Assert.That(flash.Flash(target,
                    user,
                    null,
                    milliseconds,
                    slowTo,
                    displayPopup: true,
                    melee: true), Is.True);

                var slowdown = Status(statuses, target, FlashSlowdown);
                var movement = SEntMan.GetComponent<MovementModStatusEffectComponent>(slowdown);

                Assert.Multiple(() =>
                {
                    Assert.That(StatusCount(target, Flashed), Is.EqualTo(1));
                    Assert.That(StatusCount(target, FlashSlowdown), Is.EqualTo(1));
                    Assert.That(probe.Attempts - baseline.Attempts, Is.EqualTo(1));
                    Assert.That(probe.AfterFlashed - baseline.AfterFlashed, Is.EqualTo(1));
                    Assert.That(probe.LastTarget, Is.EqualTo(target));
                    Assert.That(probe.LastUser, Is.EqualTo(user));
                    Assert.That(probe.LastUsed, Is.Null);
                    Assert.That(probe.LastMelee, Is.True);
                    Assert.That(probe.Events.First(), Is.EqualTo("attempt"));
                    Assert.That(probe.Events.IndexOf("before:StatusEffectFlashed"),
                        Is.LessThan(probe.Events.IndexOf("before:FlashSlowdownStatusEffect")));
                    Assert.That(probe.Events.IndexOf("before:FlashSlowdownStatusEffect"),
                        Is.LessThan(probe.Events.IndexOf("after")),
                        "flash and movement statuses must exist before AfterFlashed is raised");
                    Assert.That(probe.MovementRefreshes - baseline.MovementRefreshes, Is.GreaterThanOrEqualTo(1));
                    Assert.That(movement.WalkSpeedModifier, Is.EqualTo(slowTo));
                    Assert.That(movement.SprintSpeedModifier, Is.EqualTo(slowTo));
                });
                AssertStatusDuration(statuses, target, Flashed, TimeSpan.FromMilliseconds(milliseconds));
                AssertStatusDuration(statuses, target, FlashSlowdown, TimeSpan.FromMilliseconds(milliseconds));
            });

            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                Assert.That(PopupCount(popup, CEntMan.GetEntity(targetNet)), Is.EqualTo(1),
                    "one successful flash must create exactly one target popup");
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
            });
        }
    }

    [Test]
    public async Task StunBranchAndRmcBonusRunOnlyAfterSuccessfulFlash()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var flash = Server.System<SharedFlashSystem>();
            var statuses = Server.System<StatusEffectsSystem>();
            var target = SEntMan.SpawnEntity("FlashMergeTarget", map.GridCoords);
            var user = SEntMan.SpawnEntity("FlashMergeUser", map.GridCoords);

            try
            {
                var probe = SEntMan.GetComponent<FlashMergeProbeComponent>(target);
                var baseline = BeginFlashAction(target);
                Assert.That(flash.Flash(target,
                    user,
                    null,
                    875f,
                    displayPopup: false,
                    stunDuration: TimeSpan.FromMilliseconds(325)), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(StatusCount(target, Flashed), Is.EqualTo(1));
                    Assert.That(StatusCount(target, ParalyzeId), Is.EqualTo(1));
                    Assert.That(StatusCount(target, FlashSlowdown), Is.Zero,
                        "the stun branch must not also apply the movement-slow branch");
                    Assert.That(probe.Attempts - baseline.Attempts, Is.EqualTo(1));
                    Assert.That(probe.Stunned - baseline.Stunned, Is.EqualTo(1));
                    Assert.That(probe.AfterFlashed - baseline.AfterFlashed, Is.EqualTo(1));
                    Assert.That(probe.Events.IndexOf("stunned"), Is.LessThan(probe.Events.IndexOf("after")));
                });
                AssertStatusDuration(statuses, target, Flashed, TimeSpan.FromMilliseconds(875));
                AssertStatusDuration(statuses, target, ParalyzeId, TimeSpan.FromMilliseconds(325));
            }
            finally
            {
                SEntMan.DeleteEntity(target);
                SEntMan.DeleteEntity(user);
            }
        });

        await Server.WaitAssertion(() =>
        {
            var trigger = SEntMan.SpawnEntity("FlashMergeTrigger", map.GridCoords);
            var ordinary = SEntMan.SpawnEntity("FlashMergeMarine", map.GridCoords);
            try
            {
                var probe = SEntMan.GetComponent<FlashMergeProbeComponent>(ordinary);
                var baseline = BeginFlashAction(ordinary);
                var ev = new RMCTriggerEvent(null, false);
                SEntMan.EventBus.RaiseLocalEvent(trigger, ref ev);
                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(probe.AfterFlashed - baseline.AfterFlashed, Is.EqualTo(1));
                    Assert.That(probe.Stunned - baseline.Stunned, Is.EqualTo(1),
                        "a true Flash result must add the configured RMC flash bonus stun exactly once");
                });
            }
            finally
            {
                SEntMan.DeleteEntity(ordinary);
                SEntMan.DeleteEntity(trigger);
            }
        });

        await Server.WaitAssertion(() =>
        {
            var trigger = SEntMan.SpawnEntity("FlashMergeTrigger", map.GridCoords);
            var synth = SEntMan.SpawnEntity("FlashMergeSynthMarine", map.GridCoords);
            try
            {
                var probe = SEntMan.GetComponent<FlashMergeProbeComponent>(synth);
                var baseline = BeginFlashAction(synth);
                var ev = new RMCTriggerEvent(null, false);
                SEntMan.EventBus.RaiseLocalEvent(trigger, ref ev);
                Assert.Multiple(() =>
                {
                    Assert.That(ev.Handled, Is.True);
                    Assert.That(probe.AfterFlashed - baseline.AfterFlashed, Is.Zero);
                    Assert.That(probe.Stunned - baseline.Stunned, Is.Zero,
                        "a false Flash result must not add RMC flash bonus time");
                    Assert.That(StatusCount(synth, Flashed), Is.Zero);
                });
            }
            finally
            {
                SEntMan.DeleteEntity(synth);
                SEntMan.DeleteEntity(trigger);
            }
        });
    }

    private void AssertExamineProtection(EntityUid target, bool visible, string message)
    {
        var examine = new ExaminedEvent(new FormattedMessage(), target, target, true, false);
        SEntMan.EventBus.RaiseLocalEvent(target, examine);
        var protection = Loc.GetString("flash-protection");
        Assert.That(examine.GetTotalMessage().ToMarkup().Contains(protection), Is.EqualTo(visible), message);
    }

    private FlashProbeBaseline BeginFlashAction(EntityUid target)
    {
        var probe = SEntMan.GetComponent<FlashMergeProbeComponent>(target);
        probe.Events.Clear();
        return new FlashProbeBaseline(
            probe.Attempts,
            probe.AfterFlashed,
            probe.MovementRefreshes,
            probe.Stunned);
    }

    private EntityUid Status(StatusEffectsSystem statuses, EntityUid target, EntProtoId id)
    {
        Assert.That(statuses.TryGetStatusEffect(target, id, out var effect), Is.True, id.Id);
        return effect!.Value;
    }

    private void AssertStatusDuration(
        StatusEffectsSystem statuses,
        EntityUid target,
        EntProtoId id,
        TimeSpan expected)
    {
        var effect = Status(statuses, target, id);
        Assert.That(SEntMan.GetComponent<StatusEffectComponent>(effect).Duration, Is.EqualTo(expected), id.Id);
    }

    private int StatusCount(EntityUid target, EntProtoId id)
    {
        if (!SEntMan.TryGetComponent<StatusEffectContainerComponent>(target, out var container))
            return 0;

        return container.ActiveStatusEffects?.ContainedEntities.Count(effect =>
            SEntMan.GetComponent<MetaDataComponent>(effect).EntityPrototype?.ID == id.Id) ?? 0;
    }

    private int PopupCount(string message, EntityUid entity)
    {
        var popup = Client.System<PopupSystem>();
        var dictionary = (IDictionary) typeof(PopupSystem)
            .GetField("_aliveWorldLabels", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(popup)!;
        var count = 0;

        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key;
            var type = key.GetType();
            if ((string) type.GetProperty("Message")!.GetValue(key)! != message)
                continue;
            if ((EntityUid?) type.GetProperty("Entity")!.GetValue(key) != entity)
                continue;
            count++;
        }

        return count;
    }

    private readonly record struct FlashProbeBaseline(
        int Attempts,
        int AfterFlashed,
        int MovementRefreshes,
        int Stunned);
}

[RegisterComponent]
public sealed partial class FlashMergeProbeComponent : Component
{
    public readonly List<string> Events = [];
    public int Attempts;
    public int AfterFlashed;
    public int MovementRefreshes;
    public int Stunned;
    public EntityUid LastTarget;
    public EntityUid? LastUser;
    public EntityUid? LastUsed;
    public bool LastMelee;
}

public sealed class FlashMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlashMergeProbeComponent, FlashAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<FlashMergeProbeComponent, BeforeStatusEffectAddedEvent>(OnBeforeStatus);
        SubscribeLocalEvent<FlashMergeProbeComponent, RefreshMovementSpeedModifiersEvent>(OnMovementRefresh);
        SubscribeLocalEvent<FlashMergeProbeComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<FlashMergeProbeComponent, AfterFlashedEvent>(OnAfterFlashed);
    }

    private static void OnAttempt(Entity<FlashMergeProbeComponent> ent, ref FlashAttemptEvent args)
    {
        ent.Comp.Attempts++;
        ent.Comp.Events.Add("attempt");
    }

    private static void OnBeforeStatus(
        Entity<FlashMergeProbeComponent> ent,
        ref BeforeStatusEffectAddedEvent args)
    {
        ent.Comp.Events.Add($"before:{args.Effect.Id}");
    }

    private static void OnMovementRefresh(
        Entity<FlashMergeProbeComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        ent.Comp.MovementRefreshes++;
        ent.Comp.Events.Add("movement");
    }

    private static void OnStunned(Entity<FlashMergeProbeComponent> ent, ref StunnedEvent args)
    {
        ent.Comp.Stunned++;
        ent.Comp.Events.Add("stunned");
    }

    private static void OnAfterFlashed(Entity<FlashMergeProbeComponent> ent, ref AfterFlashedEvent args)
    {
        ent.Comp.AfterFlashed++;
        ent.Comp.LastTarget = args.Target;
        ent.Comp.LastUser = args.User;
        ent.Comp.LastUsed = args.Used;
        ent.Comp.LastMelee = args.Melee;
        ent.Comp.Events.Add("after");
    }
}

#pragma warning restore RA0002
