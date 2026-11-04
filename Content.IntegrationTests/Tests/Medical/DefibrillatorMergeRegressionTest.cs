#nullable enable
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Power.EntitySystems;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Audio.Components;
using Robust.Shared.Prototypes;
using DoAfterData = Content.Shared.DoAfter.DoAfter;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(DefibrillatorComponent))]
public sealed class DefibrillatorMergeRegressionTest : InteractionTest
{
    private static readonly EntProtoId DefibrillatorId = "Defibrillator";
    private static readonly EntProtoId LifepakId = "CMDefibrillator";
    private static readonly EntProtoId NoCriticalDefibrillatorId = "DefibrillatorMergeNoCritical";
    private static readonly EntProtoId TargetId = "DefibrillatorMergeTarget";
    private static readonly EntProtoId InanimateId = "DefibrillatorMergeInanimate";
    private static readonly EntProtoId BlockingOuterId = "DefibrillatorMergeBlockingOuter";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageGroupPrototype> Brute = "Brute";
    private const string LifepakChargeSound = "/Audio/_RMC14/Medical/defib_charge.ogg";

    protected override string PlayerPrototype => "MobHuman";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: DefibrillatorMergeTarget
  components:
  - type: Damageable
  - type: Injurable
    damageContainer: Biological
  - type: MobState
  - type: MobThresholds
    thresholds:
      0: Alive
      50: Critical
      100: Dead
  - type: MindContainer
  - type: DefibrillatorMergeProbe

- type: entity
  id: DefibrillatorMergeInanimate
  components:
  - type: Damageable
  - type: Injurable
    damageContainer: Biological
  - type: DefibrillatorMergeProbe

- type: entity
  id: DefibrillatorMergeBlockingOuter
  parent: ClothingOuterBase
  components:
  - type: Sprite
    sprite: Clothing/OuterClothing/Misc/black_hoodie.rsi
  - type: Clothing
    sprite: Clothing/OuterClothing/Misc/black_hoodie.rsi
  - type: RMCDefibrillatorBlocked
    showOnExamine: false

- type: entity
  id: DefibrillatorMergeNoCritical
  parent: Defibrillator
  components:
  - type: Defibrillator
    canDefibCrit: false
";

    [Test]
    public async Task LifepakStartsChargingAudioForReviver()
    {
        await SpawnTarget(TargetId);
        var defibNet = await PlaceInHands(LifepakId, enableToggleable: true);
        var serverDefib = ToServer(defibNet);
        var serverTarget = STarget!.Value;

        await Server.WaitAssertion(() =>
        {
            var defibrillator = Server.System<Content.Server.Medical.DefibrillatorSystem>();
            var component = SEntMan.GetComponent<DefibrillatorComponent>(serverDefib);

            Assert.That(defibrillator.TryStartZap((serverDefib, component), serverTarget, SPlayer), Is.True);
            Assert.That(component.ChargeSoundEntity, Is.Not.Null,
                "an accepted lifepak use must create a tracked charging stream");
            Assert.That(SEntMan.GetComponent<AudioComponent>(component.ChargeSoundEntity!.Value).FileName,
                Is.EqualTo(LifepakChargeSound));
        });

        await RunTicks(3);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.EntityQuery<AudioComponent>(),
                Has.Some.Matches<AudioComponent>(audio => audio.FileName == LifepakChargeSound),
                "the authoritative charging stream must reach the reviver's client");
        });
    }

    [Test]
    public async Task CanDefibCritControlsCriticalTargets()
    {
        await SpawnTarget(TargetId);
        var target = STarget!.Value;
        var damageable = Server.System<DamageableSystem>();
        var criticalDamage = new DamageSpecifier(ProtoMan.Index(Blunt), FixedPoint2.New(50));

        await Server.WaitPost(() => damageable.SetDamage(
            (target, SEntMan.GetComponent<DamageableComponent>(target)), criticalDamage));
        await RunTicks(3);
        Assert.That(SEntMan.GetComponent<MobStateComponent>(target).CurrentState, Is.EqualTo(MobState.Critical));

        var defaultDefib = ToServer(await PlaceInHands(DefibrillatorId, enableToggleable: true));
        await Server.WaitAssertion(() =>
        {
            var defibrillator = Server.System<Content.Server.Medical.DefibrillatorSystem>();
            var component = SEntMan.GetComponent<DefibrillatorComponent>(defaultDefib);
            Assert.Multiple(() =>
            {
                Assert.That(component.CanDefibCrit, Is.True,
                    "the default component contract must continue to permit critical targets");
                Assert.That(defibrillator.CanZap((defaultDefib, component), target, SPlayer), Is.True);
            });
        });

        var restrictedDefib = ToServer(await PlaceInHands(NoCriticalDefibrillatorId, enableToggleable: true));
        await Server.WaitAssertion(() =>
        {
            var defibrillator = Server.System<Content.Server.Medical.DefibrillatorSystem>();
            var component = SEntMan.GetComponent<DefibrillatorComponent>(restrictedDefib);
            Assert.Multiple(() =>
            {
                Assert.That(component.CanDefibCrit, Is.False,
                    "canDefibCrit must deserialize from the prototype override");
                Assert.That(defibrillator.CanZap((restrictedDefib, component), target, SPlayer), Is.False,
                    "a defibrillator with canDefibCrit disabled must reject critical targets");
            });
        });
    }

    [Test]
    public async Task SkillDelayDuplicateAudioAndBlockingContracts()
    {
        await SpawnTarget(TargetId);
        var defibNet = await PlaceInHands(DefibrillatorId, enableToggleable: true);
        var defib = ToServer(defibNet);
        var target = STarget!.Value;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<DefibrillatorMergeProbeSystem>();
            var defibrillator = Server.System<Content.Server.Medical.DefibrillatorSystem>();
            var powerCell = Server.System<PowerCellSystem>();
            var battery = Server.System<BatterySystem>();
            var skills = Server.System<SkillsSystem>();
            var component = SEntMan.GetComponent<DefibrillatorComponent>(defib);
            var draw = SEntMan.GetComponent<PowerCellDrawComponent>(defib);

            Assert.That(powerCell.TryGetBatteryFromSlot(defib, out var batteryEntity), Is.True);
            battery.SetCharge(batteryEntity!.Value.AsNullable(), draw.UseCharge);
            skills.SetSkill(SPlayer, "RMCSkillMedical", 0);
            component.DoAfterDuration = TimeSpan.FromSeconds(1);
            component.SkillMultiplierDuration = TimeSpan.FromSeconds(3);
            component.AllowDoAfterMovement = false;

            Assert.That(defibrillator.TryStartZap((defib, component), target, SPlayer), Is.True);
            var active = GetActiveDoAfters();
            Assert.Multiple(() =>
            {
                Assert.That(active, Has.Length.EqualTo(1));
                Assert.That(active[0].Args.Event, Is.TypeOf<DefibrillatorZapDoAfterEvent>());
                Assert.That(active[0].Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(4)));
                Assert.That(active[0].Args.NeedHand, Is.True);
                Assert.That(active[0].Args.BreakOnMove, Is.True);
                Assert.That(active[0].Args.BreakOnHandChange, Is.False);
                Assert.That(active[0].Args.DuplicateCondition, Is.EqualTo(DuplicateConditions.SameEvent));
                Assert.That(active[0].Args.TargetEffect, Is.EqualTo("RMCEffectHealBusy"));
                Assert.That(active[0].Args.MovementThreshold, Is.EqualTo(0.5f));
                Assert.That(active[0].Args.RootEntity, Is.True);
                Assert.That(component.ChargeSoundEntity, Is.Not.Null,
                    "a successful start must track the charging stream");
                Assert.That(battery.GetCharge(batteryEntity.Value.AsNullable()), Is.EqualTo(draw.UseCharge),
                    "starting the do-after must not consume the last charge");
            });

            Assert.That(defibrillator.TryStartZap((defib, component), target, SPlayer), Is.False,
                "the same-event duplicate must be rejected");
            Assert.Multiple(() =>
            {
                Assert.That(GetActiveDoAfters(), Is.Empty,
                    "the rejected duplicate follows SharedDoAfter's cancel-existing contract");
                Assert.That(component.ChargeSoundEntity, Is.Null,
                    "cancelling the tracked do-after must stop and clear its charging stream");
                Assert.That(battery.GetCharge(batteryEntity.Value.AsNullable()), Is.EqualTo(draw.UseCharge));
            });

            SEntMan.EnsureComponent<RMCDefibrillatorBlockedComponent>(target);
            Assert.That(defibrillator.CanZap((defib, component), target, SPlayer), Is.False,
                "a directly blocked target must be rejected");
            SEntMan.RemoveComponent<RMCDefibrillatorBlockedComponent>(target);

            var wearer = SEntMan.SpawnEntity("MobHuman", SEntMan.GetCoordinates(TargetCoords));
            var outer = SEntMan.SpawnEntity(BlockingOuterId, SEntMan.GetCoordinates(TargetCoords));
            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(wearer, outer, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(defibrillator.CanZap((defib, component), wearer, SPlayer), Is.False,
                "RMCDefibrillatorBlocked on worn outer clothing must relay through the inventory gate");
        });
    }

    [Test]
    public async Task RedirectsPrecedePowerAndDeadGroupHealingRevives()
    {
        await AddAtmosphere();
        await SpawnTarget(TargetId);
        var defibNet = await PlaceInHands(DefibrillatorId, enableToggleable: true);
        var defib = ToServer(defibNet);
        var target = STarget!.Value;
        EntityUid blocked = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<DefibrillatorMergeProbeSystem>();
            var powerCell = Server.System<PowerCellSystem>();
            Assert.That(powerCell.TryGetBatteryFromSlot(defib, out var batteryEntity), Is.True);
            blocked = SEntMan.SpawnEntity(null, SEntMan.GetCoordinates(TargetCoords));
            SEntMan.EnsureComponent<RMCDefibrillatorBlockedComponent>(blocked);
            SEntMan.EnsureComponent<DefibrillatorMergeProbeComponent>(SPlayer);
            SEntMan.EnsureComponent<DefibrillatorMergeProbeComponent>(defib);
        });

        var defibrillator = Server.System<Content.Server.Medical.DefibrillatorSystem>();
        var battery = Server.System<BatterySystem>();
        var powerCell = Server.System<PowerCellSystem>();
        var damageable = Server.System<DamageableSystem>();
        var component = SEntMan.GetComponent<DefibrillatorComponent>(defib);
        var draw = SEntMan.GetComponent<PowerCellDrawComponent>(defib);
        Assert.That(powerCell.TryGetBatteryFromSlot(defib, out var batteryEntity), Is.True);

        component.ZapDelay = TimeSpan.Zero;
        component.ZapHeal = new DamageSpecifier();
        component.RMCZapDamage = [(Brute, -200)];

        await Server.WaitAssertion(() =>
        {
            var selfProbe = SEntMan.GetComponent<DefibrillatorMergeProbeComponent>(SPlayer);
            var targetProbe = SEntMan.GetComponent<DefibrillatorMergeProbeComponent>(target);

            SetLastCharge(defib, component, batteryEntity!.Value.AsNullable(), draw, battery);
            selfProbe.CancelSelf = true;
            defibrillator.Zap((defib, component), target, SPlayer);
            Assert.That(battery.GetCharge(batteryEntity.Value.AsNullable()), Is.EqualTo(draw.UseCharge),
                "self cancellation must happen before charge consumption");

            selfProbe.CancelSelf = false;
            selfProbe.RedirectSelf = blocked;
            defibrillator.Zap((defib, component), target, SPlayer);
            Assert.That(battery.GetCharge(batteryEntity.Value.AsNullable()), Is.EqualTo(draw.UseCharge),
                "a self-event redirect must be revalidated before charge consumption");

            selfProbe.RedirectSelf = null;
            targetProbe.CancelTarget = true;
            defibrillator.Zap((defib, component), target, SPlayer);
            Assert.That(battery.GetCharge(batteryEntity.Value.AsNullable()), Is.EqualTo(draw.UseCharge),
                "target cancellation must happen before charge consumption");

            targetProbe.CancelTarget = false;
            targetProbe.RedirectTarget = blocked;
            defibrillator.Zap((defib, component), target, SPlayer);
            Assert.That(battery.GetCharge(batteryEntity.Value.AsNullable()), Is.EqualTo(draw.UseCharge),
                "a target-event redirect must be revalidated before charge consumption");

            targetProbe.RedirectTarget = null;
            defibrillator.Zap((defib, component), target, SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(battery.GetCharge(batteryEntity.Value.AsNullable()), Is.EqualTo(0f),
                    "a valid use must accept and consume the final available charge");
                Assert.That(targetProbe.AttemptEvents, Is.Zero,
                    "living targets must not enter the RMC dead-target damage-modification path");
                Assert.That(targetProbe.DefibrillatedEvents, Is.EqualTo(1));
            });
        });

        var deathDamage = new DamageSpecifier(ProtoMan.Index(Blunt), FixedPoint2.New(100));
        await Server.WaitPost(() => damageable.SetDamage(
            (target, SEntMan.GetComponent<DamageableComponent>(target)), deathDamage));
        await RunTicks(3);
        Assert.That(SEntMan.GetComponent<MobStateComponent>(target).CurrentState, Is.EqualTo(MobState.Dead));

        await Server.WaitAssertion(() =>
        {
            var targetProbe = SEntMan.GetComponent<DefibrillatorMergeProbeComponent>(target);
            var defibProbe = SEntMan.GetComponent<DefibrillatorMergeProbeComponent>(defib);
            SetLastCharge(defib, component, batteryEntity!.Value.AsNullable(), draw, battery);
            targetProbe.CancelAttempt = true;
            defibrillator.Zap((defib, component), target, SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(damageable.GetTotalDamage(target), Is.EqualTo(FixedPoint2.New(100)),
                    "heart/organ cancellation must replace the heal with an empty specifier");
                Assert.That(SEntMan.GetComponent<MobStateComponent>(target).CurrentState, Is.EqualTo(MobState.Dead));
                Assert.That(targetProbe.AttemptEvents, Is.EqualTo(1));
                Assert.That(defibProbe.DamageModifyEvents, Is.EqualTo(1));
            });
        });

        // UseDelay considers EndTime == CurTime active, including a zero-length delay.
        await RunTicks(1);
        await Server.WaitAssertion(() =>
        {
            var targetProbe = SEntMan.GetComponent<DefibrillatorMergeProbeComponent>(target);
            var defibProbe = SEntMan.GetComponent<DefibrillatorMergeProbeComponent>(defib);
            SetLastCharge(defib, component, batteryEntity!.Value.AsNullable(), draw, battery);
            targetProbe.CancelAttempt = false;
            defibrillator.Zap((defib, component), target, SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(targetProbe.AttemptEvents, Is.EqualTo(2));
                Assert.That(defibProbe.DamageModifyEvents, Is.EqualTo(2));
                Assert.That(damageable.GetTotalDamage(target), Is.EqualTo(FixedPoint2.Zero),
                    "RMC group healing must be the effective returned heal when ZapHeal is empty");
                Assert.That(SEntMan.GetComponent<MobStateComponent>(target).CurrentState, Is.EqualTo(MobState.Alive),
                    "a fully repaired dead target must pass VerifyThresholds after revival");
            });
        });
    }

    [Test]
    public async Task InanimateTargetsRetainUpstreamDamageAndEventPath()
    {
        await SpawnTarget(InanimateId);
        var defibNet = await PlaceInHands(DefibrillatorId, enableToggleable: true);
        var defib = ToServer(defibNet);
        var target = STarget!.Value;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<DefibrillatorMergeProbeSystem>();
            var defibrillator = Server.System<Content.Server.Medical.DefibrillatorSystem>();
            var damageable = Server.System<DamageableSystem>();
            var powerCell = Server.System<PowerCellSystem>();
            var battery = Server.System<BatterySystem>();
            var component = SEntMan.GetComponent<DefibrillatorComponent>(defib);
            var draw = SEntMan.GetComponent<PowerCellDrawComponent>(defib);
            var targetDamageable = SEntMan.GetComponent<DamageableComponent>(target);
            var targetProbe = SEntMan.GetComponent<DefibrillatorMergeProbeComponent>(target);
            Assert.That(powerCell.TryGetBatteryFromSlot(defib, out var batteryEntity), Is.True);

            component.ZapDelay = TimeSpan.Zero;
            component.ZapHeal = new DamageSpecifier(ProtoMan.Index(Blunt), FixedPoint2.New(-10));
            component.RMCZapDamage = null;
            damageable.SetDamage((target, targetDamageable),
                new DamageSpecifier(ProtoMan.Index(Blunt), FixedPoint2.New(20)));
            SetLastCharge(defib, component, batteryEntity!.Value.AsNullable(), draw, battery);

            defibrillator.Zap((defib, component), target, SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(damageable.GetTotalDamage(target), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(targetProbe.AttemptEvents, Is.Zero);
                Assert.That(targetProbe.DefibrillatedEvents, Is.EqualTo(1));
                Assert.That(battery.GetCharge(batteryEntity.Value.AsNullable()), Is.EqualTo(0f));
            });
        });
    }

    private DoAfterData[] GetActiveDoAfters()
    {
        return SEntMan.GetComponent<DoAfterComponent>(SPlayer).DoAfters.Values
            .Where(doAfter => !doAfter.Cancelled && !doAfter.Completed)
            .ToArray();
    }

    private void SetLastCharge(
        EntityUid defib,
        DefibrillatorComponent component,
        Entity<BatteryComponent?> batteryEntity,
        PowerCellDrawComponent draw,
        BatterySystem battery)
    {
        battery.SetCharge(batteryEntity, draw.UseCharge);
        if (!ItemToggleSys.IsActivated(defib))
            Assert.That(ItemToggleSys.TryActivate(defib, user: SPlayer), Is.True);

        Assert.That(component.ZapDelay, Is.EqualTo(TimeSpan.Zero));
    }
}

[RegisterComponent]
public sealed partial class DefibrillatorMergeProbeComponent : Component
{
    public bool CancelSelf;
    public bool CancelTarget;
    public bool CancelAttempt;
    public EntityUid? RedirectSelf;
    public EntityUid? RedirectTarget;
    public int AttemptEvents;
    public int DamageModifyEvents;
    public int DefibrillatedEvents;
}

public sealed class DefibrillatorMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DefibrillatorMergeProbeComponent, SelfBeforeDefibrillatorZapsEvent>(OnSelfBeforeZap);
        SubscribeLocalEvent<DefibrillatorMergeProbeComponent, TargetBeforeDefibrillatorZapsEvent>(OnTargetBeforeZap);
        SubscribeLocalEvent<DefibrillatorMergeProbeComponent, RMCDefibrillatorAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<DefibrillatorMergeProbeComponent, RMCDefibrillatorDamageModifyEvent>(OnDamageModify,
            after: [typeof(RMCDefibrillatorSystem)]);
        SubscribeLocalEvent<DefibrillatorMergeProbeComponent, TargetDefibrillatedEvent>(OnDefibrillated);
    }

    private static void OnSelfBeforeZap(
        Entity<DefibrillatorMergeProbeComponent> ent,
        ref SelfBeforeDefibrillatorZapsEvent args)
    {
        if (ent.Comp.RedirectSelf is { } target)
            args.DefibTarget = target;
        if (ent.Comp.CancelSelf)
            args.Cancel();
    }

    private static void OnTargetBeforeZap(
        Entity<DefibrillatorMergeProbeComponent> ent,
        ref TargetBeforeDefibrillatorZapsEvent args)
    {
        if (ent.Comp.RedirectTarget is { } target)
            args.DefibTarget = target;
        if (ent.Comp.CancelTarget)
            args.Cancel();
    }

    private static void OnAttempt(
        Entity<DefibrillatorMergeProbeComponent> ent,
        ref RMCDefibrillatorAttemptEvent args)
    {
        ent.Comp.AttemptEvents++;
        if (ent.Comp.CancelAttempt)
            args.Cancel("rmc-defibrillator-unrevivable");
    }

    private static void OnDamageModify(
        Entity<DefibrillatorMergeProbeComponent> ent,
        ref RMCDefibrillatorDamageModifyEvent args)
    {
        ent.Comp.DamageModifyEvents++;
    }

    private static void OnDefibrillated(
        Entity<DefibrillatorMergeProbeComponent> ent,
        ref TargetDefibrillatedEvent args)
    {
        ent.Comp.DefibrillatedEvents++;
    }
}
