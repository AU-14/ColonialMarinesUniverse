#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._RMC14.Damage;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Damageable;

[TestFixture]
[TestOf(typeof(DamageableSystem))]
public sealed class DamageableMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: damageContainer
  id: DamageMergeContainer
  supportedTypes: [ Blunt ]

- type: damageModifierSet
  id: DamageMergeHalfBlunt
  coefficients:
    Blunt: 0.5

- type: entity
  id: DamageMergeTarget
  components:
  - type: Damageable
    damageModifierSet: DamageMergeHalfBlunt
    displacement: Dwarfism
    radiationDamageTypes: [ Radiation, Heat ]
  - type: Injurable
    damageContainer: DamageMergeContainer
  - type: DoAfter
  - type: DamageMergeProbe

- type: entity
  id: DamageMergeClone

- type: entity
  id: DamageMergeTool

- type: entity
  id: DamageMergeContactSource
  components:
  - type: DamageContacts
    damage:
      types:
        Blunt: 2
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix1:
        shape: !type:PhysShapeCircle
          radius: 0.25
        mask: [ MobMask ]
        layer: [ MobLayer ]
        hard: false

- type: entity
  id: DamageMergeContactTarget
  parent: DamageMergeTarget
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      fix1:
        shape: !type:PhysShapeCircle
          radius: 0.25
        density: 10
        mask: [ MobMask ]
        layer: [ MidImpassable ]
        hard: false

- type: entity
  id: DamageMergeTriggerBlunt
  parent: DamageMergeTarget
  components:
  - type: DamageOnTrigger
    damage:
      types:
        Blunt: 2

- type: entity
  id: DamageMergeTriggerStructural
  parent: DamageMergeTarget
  components:
  - type: DamageOnTrigger
    damage:
      types:
        Structural: 2

";

    [SidedDependency(Side.Server)] private DamageableSystem _damageable = default!;
    [SidedDependency(Side.Server)] private SharedDoAfterSystem _doAfter = default!;
    [SidedDependency(Side.Server)] private DamageContactsSystem _contacts = default!;
    [SidedDependency(Side.Server)] private TriggerSystem _trigger = default!;
    [SidedDependency(Side.Server)] private IConfigurationManager _serverCfg = default!;

    [Test]
    public async Task MetadataAndOrderingSurviveResistanceAfterResistAndUniversalModifiers()
    {
        await Server.WaitIdleAsync();
        var originalModifier = 0f;
        var modifierOverridden = false;
        try
        {
            await Server.WaitAssertion(() =>
            {
                originalModifier = _serverCfg.GetCVar(CCVars.PlaytestAllDamageModifier);
                _serverCfg.SetCVar(CCVars.PlaytestAllDamageModifier, 2f);
                modifierOverridden = true;
            });

            await Server.WaitAssertion(() =>
            {
                var target = SEntMan.Spawn("DamageMergeTarget");
                var origin = SEntMan.Spawn("DamageMergeTool");
                var tool = SEntMan.Spawn("DamageMergeTool");
                try
                {
                    var damageable = SEntMan.GetComponent<DamageableComponent>(target);
                    var probe = SEntMan.GetComponent<DamageMergeProbeComponent>(target);
                    probe.MutatePipeline = true;
                    probe.SetLocationalTarget = true;

                    var changed = _damageable.TryChangeDamage(
                        (target, damageable),
                        Damage("Blunt", 10),
                        out var result,
                        origin: origin,
                        tool: tool,
                        armorPiercing: 7,
                        impact: DamageImpact.Projectile);

                    Assert.Multiple(() =>
                    {
                        Assert.That(changed, Is.True);
                        AssertDamage(result, "Blunt", 22,
                            "5 after prototype resistance, 10 after DamageModify, 11 after after-resist, 22 after universal");
                        AssertDamage(damageable.Damage, "Blunt", 22, "stored damage");
                        Assert.That(probe.Order.Take(3), Is.EqualTo(new[] { "before", "modify", "after" }));
                        Assert.That(probe.Order, Does.Contain("dealt"));
                        Assert.That(probe.Order, Does.Contain("changed"));
                        Assert.That(probe.ModifyArmorPiercing, Is.EqualTo(7));
                        Assert.That(probe.BeforeTool, Is.EqualTo(tool));
                        Assert.That(probe.ModifyTool, Is.EqualTo(tool));
                        Assert.That(probe.AfterTool, Is.EqualTo(tool));
                        Assert.That(probe.DealtTool, Is.EqualTo(tool));
                        Assert.That(probe.ChangedTool, Is.EqualTo(tool));
                        Assert.That(probe.ModifyImpact, Is.EqualTo(DamageImpact.Projectile));
                        Assert.That(probe.AfterImpact, Is.EqualTo(DamageImpact.Explosion));
                        Assert.That(probe.DealtImpact, Is.EqualTo(DamageImpact.SnaggingContact));
                        Assert.That(probe.ChangedImpact, Is.EqualTo(DamageImpact.SnaggingContact));
                        Assert.That(probe.ModifySlots, Is.EqualTo(SlotFlags.HEAD));
                        Assert.That(probe.ModifyPart, Is.EqualTo(BodyPartType.Head));
                        Assert.That(probe.ModifyZone, Is.EqualTo(TargetBodyZone.Head));
                        AssertDamage(probe.ModifyDamage!, "Blunt", 5, "resistance must precede DamageModify");
                        AssertDamage(probe.AfterDamage!, "Blunt", 10, "DamageModify must precede after-resist");
                        AssertDamage(probe.DealtDamage!, "Blunt", 22, "universal modifiers must precede DamageDealt");
                        AssertDamage(probe.ChangedDelta!, "Blunt", 22, "DamageChanged must receive the applied delta");
                    });

                    probe.Reset();
                    probe.MutatePipeline = true;
                    var ignored = _damageable.ChangeDamage(
                        (target, damageable),
                        Damage("Blunt", 2),
                        ignoreResistances: true,
                        origin: origin,
                        tool: tool,
                        armorPiercing: 99,
                        impact: DamageImpact.Projectile);
                    Assert.Multiple(() =>
                    {
                        Assert.That(probe.Order, Does.Not.Contain("modify"),
                            "ignoreResistances must bypass DamageModify");
                        Assert.That(probe.Order.Take(2), Is.EqualTo(new[] { "before", "after" }),
                            "after-resist must still run when resistances are ignored");
                        AssertDamage(ignored, "Blunt", 6,
                            "2 plus after-resist 1, then universal x2");
                        Assert.That(probe.AfterTool, Is.EqualTo(tool));
                        Assert.That(probe.AfterImpact, Is.EqualTo(DamageImpact.Projectile));
                        Assert.That(probe.ChangedImpact, Is.EqualTo(DamageImpact.SnaggingContact));
                    });

                    probe.Reset();
                    var beforeGlobalBypass = damageable.TotalDamage;
                    var globalBypass = _damageable.ChangeDamage(
                        (target, damageable),
                        Damage("Blunt", 2),
                        ignoreResistances: true,
                        origin: origin,
                        tool: tool,
                        impact: DamageImpact.Projectile,
                        ignoreGlobalModifiers: true);
                    Assert.Multiple(() =>
                    {
                        AssertDamage(globalBypass, "Blunt", 2,
                            "ignoreGlobalModifiers preserves the unscaled canonical damage");
                        Assert.That(damageable.TotalDamage - beforeGlobalBypass, Is.EqualTo(FixedPoint2.New(2)));
                    });
                }
                finally
                {
                    SEntMan.DeleteEntity(target);
                    SEntMan.DeleteEntity(origin);
                    SEntMan.DeleteEntity(tool);
                }
            });
        }
        finally
        {
            if (modifierOverridden)
            {
                await Server.WaitAssertion(() =>
                    _serverCfg.SetCVar(CCVars.PlaytestAllDamageModifier, originalModifier));
            }
        }
    }

    [Test]
    public async Task NullableCompatibilityReturnsAppliedDeltaWhileBoolOutKeepsAttemptedSemantics()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var target = SEntMan.Spawn("DamageMergeTarget");
            try
            {
                var component = SEntMan.GetComponent<DamageableComponent>(target);
                var probe = SEntMan.GetComponent<DamageMergeProbeComponent>(target);

                Assert.That(_damageable.TryChangeDamage(null, Damage("Blunt", 1)), Is.Null,
                    "missing nullable target");

                var validEmpty = _damageable.TryChangeDamage(target, new DamageSpecifier());
                Assert.That(validEmpty, Is.Not.Null.And.Property(nameof(DamageSpecifier.Empty)).True,
                    "a valid target with an empty request returns a non-null empty delta");

                probe.CancelBefore = true;
                Assert.That(_damageable.TryChangeDamage(target, Damage("Blunt", 1)), Is.Null,
                    "fork nullable compatibility reports BeforeDamageChanged cancellation as null");
                Assert.That(_damageable.TryChangeDamage(
                        (target, component),
                        Damage("Blunt", 1),
                        out var cancelled),
                    Is.False,
                    "canonical bool/out reports cancellation as false");
                Assert.That(cancelled.Empty, Is.True);
                probe.CancelBefore = false;

                probe.EmptyAfter = true;
                var beforeEmptyAfter = component.TotalDamage;
                var nullableEmptyAfter = _damageable.TryChangeDamage(target, Damage("Blunt", 4));
                Assert.Multiple(() =>
                {
                    Assert.That(nullableEmptyAfter, Is.Not.Null);
                    Assert.That(nullableEmptyAfter!.Empty, Is.True,
                        "modifier-to-empty is a valid nullable call with no applied delta");
                    Assert.That(component.TotalDamage, Is.EqualTo(beforeEmptyAfter));
                });
                Assert.That(_damageable.TryChangeDamage(
                        (target, component),
                        Damage("Blunt", 4),
                        out var canonicalEmptyAfter),
                    Is.False,
                    "canonical bool/out reports modifier-to-empty as false");
                Assert.That(canonicalEmptyAfter.Empty, Is.True);
                Assert.That(component.TotalDamage, Is.EqualTo(beforeEmptyAfter));
                probe.EmptyAfter = false;

                var unsupported = _damageable.TryChangeDamage(target, Damage("Structural", 4));
                Assert.Multiple(() =>
                {
                    Assert.That(unsupported, Is.Not.Null);
                    Assert.That(unsupported!.Empty, Is.True,
                        "unsupported damage is a successful valid call with no applied delta");
                    Assert.That(component.TotalDamage, Is.EqualTo(FixedPoint2.Zero));
                });

                Assert.That(_damageable.TryChangeDamage(
                        (target, component),
                        Damage("Structural", 4),
                        out var attemptedUnsupported),
                    Is.True,
                    "the upstream bool/out path reports its non-empty attempted post-modifier spec");
                AssertDamage(attemptedUnsupported, "Structural", 4, "canonical attempted spec");
                Assert.That(component.TotalDamage, Is.EqualTo(FixedPoint2.Zero),
                    "unsupported canonical damage is still filtered by Injurable");

                var wastedHealing = _damageable.TryChangeDamage(target, Damage("Blunt", -4));
                Assert.That(wastedHealing, Is.Not.Null.And.Property(nameof(DamageSpecifier.Empty)).True,
                    "healing clamped at zero has an empty applied delta");

                var applied = _damageable.TryChangeDamage(target, Damage("Blunt", 3));
                Assert.Multiple(() =>
                {
                    Assert.That(applied, Is.Not.Null);
                    AssertDamage(applied!, "Blunt", 1.5f, "nullable returns the resistance-adjusted applied delta");
                    Assert.That(component.TotalDamage, Is.EqualTo(FixedPoint2.New(1.5f)),
                        "nullable compatibility must apply exactly once");
                });

                var clamped = _damageable.TryChangeDamage(target, Damage("Blunt", -10));
                Assert.Multiple(() =>
                {
                    Assert.That(clamped, Is.Not.Null);
                    AssertDamage(clamped!, "Blunt", -1.5f,
                        "nullable returns the actual partial healing delta after the zero clamp");
                    Assert.That(component.TotalDamage, Is.EqualTo(FixedPoint2.Zero));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(target);
            }
        });
    }

    [Test]
    public async Task StateCopyDamageGroupingAndDoAfterInterruptionRemainIntact()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var source = SEntMan.Spawn("DamageMergeTarget");
            var clone = SEntMan.Spawn("DamageMergeClone");
            var noInterrupt = SEntMan.Spawn("DamageMergeTarget");
            try
            {
                var sourceComp = SEntMan.GetComponent<DamageableComponent>(source);
                _damageable.ChangeDamage((source, sourceComp), Damage("Blunt", 6));
                Assert.That(sourceComp.TotalDamage, Is.EqualTo(FixedPoint2.New(3)));

                var grouped = _damageable.GetDamages(sourceComp.DamagePerGroup, sourceComp.Damage);
                AssertDamage(new DamageSpecifier { DamageDict = grouped }, "Blunt", 3, "GetDamages");

                var state = (DamageableComponentState) SEntMan.GetComponentState(
                    SEntMan.EventBus,
                    sourceComp,
                    null,
                    GameTick.Zero)!;
                Assert.Multiple(() =>
                {
                    Assert.That(state.Displacement?.Id, Is.EqualTo("Dwarfism"));
                    Assert.That(state.ModifierSetId?.Id, Is.EqualTo("DamageMergeHalfBlunt"));
                    AssertDamage(state.Damage, "Blunt", 3, "replicated damage state");
                });

                _damageable.CopyComponent((source, sourceComp), clone);
                var cloneComp = SEntMan.GetComponent<DamageableComponent>(clone);
                Assert.Multiple(() =>
                {
                    Assert.That(cloneComp.Displacement?.Id, Is.EqualTo("Dwarfism"));
                    Assert.That(cloneComp.DamageModifierSetId?.Id, Is.EqualTo("DamageMergeHalfBlunt"));
                    Assert.That(cloneComp.RadiationDamageTypeIDs.Select(id => id.Id),
                        Is.EqualTo(new[] { "Radiation", "Heat" }));
                    Assert.That(cloneComp.Damage.Empty, Is.True,
                        "CopyComponent copies configuration but not accumulated damage");
                });

                var interrupted = new DamageMergeDoAfterEvent();
                Assert.That(_doAfter.TryStartDoAfter(new DoAfterArgs(
                    SEntMan,
                    source,
                    TimeSpan.FromMinutes(1),
                    interrupted,
                    null)
                {
                    Broadcast = true,
                    BreakOnDamage = true,
                    DamageThreshold = 1,
                }), Is.True);
                _damageable.ChangeDamage((source, sourceComp), Damage("Blunt", 4), interruptsDoAfters: true);
                Assert.That(interrupted.Cancelled, Is.True,
                    "a positive applied delta above the threshold interrupts DoAfter");

                var noInterruptEvent = new DamageMergeDoAfterEvent();
                var noInterruptComp = SEntMan.GetComponent<DamageableComponent>(noInterrupt);
                Assert.That(_doAfter.TryStartDoAfter(new DoAfterArgs(
                    SEntMan,
                    noInterrupt,
                    TimeSpan.FromMinutes(1),
                    noInterruptEvent,
                    null)
                {
                    Broadcast = true,
                    BreakOnDamage = true,
                    DamageThreshold = 1,
                }), Is.True);
                _damageable.ChangeDamage(
                    (noInterrupt, noInterruptComp),
                    Damage("Blunt", 4),
                    interruptsDoAfters: false);
                Assert.That(noInterruptEvent.Cancelled, Is.False,
                    "interruptsDoAfters=false must survive positive damage");

                _damageable.ChangeDamage(
                    (noInterrupt, noInterruptComp),
                    Damage("Structural", 20),
                    interruptsDoAfters: true);
                Assert.That(noInterruptEvent.Cancelled, Is.False,
                    "a zero applied delta must not interrupt DoAfter");
            }
            finally
            {
                SEntMan.DeleteEntity(source);
                SEntMan.DeleteEntity(clone);
                SEntMan.DeleteEntity(noInterrupt);
            }
        });
    }

    [Test]
    public async Task ContactCollisionInitializesAndPropagatesStructuredImpact()
    {
        var map = await Pair.CreateTestMap();
        EntityUid source = default;
        EntityUid target = default;

        await Server.WaitPost(() =>
        {
            source = SEntMan.SpawnEntity("DamageMergeContactSource", map.GridCoords);
            target = SEntMan.SpawnEntity("DamageMergeContactTarget", map.GridCoords);
        });
        await Pair.RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            var sourceDamage = SEntMan.GetComponent<DamageContactsComponent>(source).Damage;
            var damaged = SEntMan.GetComponent<DamagedByContactComponent>(target);
            var expectedImpact = DamageImpact.ForContact(sourceDamage);
            Assert.Multiple(() =>
            {
                AssertDamage(damaged.Damage!, "Blunt", 2,
                    "StartCollide copies the contact damage onto the touched entity");
                Assert.That(damaged.Impact, Is.EqualTo(expectedImpact),
                    "StartCollide derives structured contact impact from source damage");
            });

            var probe = SEntMan.GetComponent<DamageMergeProbeComponent>(target);
            probe.Reset();
            damaged.NextSecond = TimeSpan.Zero;
            _contacts.Update(1);
            Assert.Multiple(() =>
            {
                Assert.That(probe.ModifyImpact, Is.EqualTo(expectedImpact),
                    "periodic contact damage forwards impact into resistance modifiers");
                Assert.That(probe.ChangedImpact, Is.EqualTo(expectedImpact),
                    "periodic contact damage forwards impact into DamageChanged");
            });
        });

        await Server.WaitPost(() =>
        {
            SEntMan.DeleteEntity(source);
            SEntMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task DamageOnTriggerHandledSemanticsRemainIntact()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var cancelledTrigger = SEntMan.Spawn("DamageMergeTriggerBlunt");
            var emptiedTrigger = SEntMan.Spawn("DamageMergeTriggerBlunt");
            var supportedTrigger = SEntMan.Spawn("DamageMergeTriggerBlunt");
            var unsupportedTrigger = SEntMan.Spawn("DamageMergeTriggerStructural");
            try
            {
                var cancelledProbe = SEntMan.GetComponent<DamageMergeProbeComponent>(cancelledTrigger);
                cancelledProbe.CancelBefore = true;
                Assert.That(_trigger.Trigger(cancelledTrigger, predicted: false), Is.False,
                    "DamageOnTrigger must not report handled after BeforeDamageChanged cancellation");

                var emptiedProbe = SEntMan.GetComponent<DamageMergeProbeComponent>(emptiedTrigger);
                emptiedProbe.EmptyAfter = true;
                Assert.That(_trigger.Trigger(emptiedTrigger, predicted: false), Is.False,
                    "DamageOnTrigger must not report handled when a modifier empties the spec");

                Assert.That(_trigger.Trigger(supportedTrigger, predicted: false), Is.True,
                    "non-empty supported damage handles the trigger");
                Assert.That(_trigger.Trigger(unsupportedTrigger, predicted: false), Is.True,
                    "DamageOnTrigger follows canonical attempted-spec semantics even when Injurable filters the type");
                Assert.That(SEntMan.GetComponent<DamageableComponent>(unsupportedTrigger).TotalDamage,
                    Is.EqualTo(FixedPoint2.Zero));
            }
            finally
            {
                SEntMan.DeleteEntity(cancelledTrigger);
                SEntMan.DeleteEntity(emptiedTrigger);
                SEntMan.DeleteEntity(supportedTrigger);
                SEntMan.DeleteEntity(unsupportedTrigger);
            }
        });
    }

    private static DamageSpecifier Damage(string type, float amount)
    {
        return new DamageSpecifier
        {
            DamageDict =
            {
                [type] = FixedPoint2.New(amount),
            },
        };
    }

    private static void AssertDamage(DamageSpecifier damage, string type, float amount, string message)
    {
        Assert.That(damage.DamageDict.TryGetValue(type, out var actual), Is.True, message);
        Assert.That(actual, Is.EqualTo(FixedPoint2.New(amount)), message);
    }
}

[RegisterComponent]
public sealed partial class DamageMergeProbeComponent : Component
{
    public readonly List<string> Order = [];
    public bool MutatePipeline;
    public bool SetLocationalTarget;
    public bool CancelBefore;
    public bool EmptyAfter;
    public EntityUid? BeforeTool;
    public EntityUid? ModifyTool;
    public EntityUid? AfterTool;
    public EntityUid? DealtTool;
    public EntityUid? ChangedTool;
    public int ModifyArmorPiercing;
    public DamageImpact ModifyImpact;
    public DamageImpact AfterImpact;
    public DamageImpact DealtImpact;
    public DamageImpact ChangedImpact;
    public SlotFlags ModifySlots;
    public BodyPartType? ModifyPart;
    public TargetBodyZone? ModifyZone;
    public DamageSpecifier? ModifyDamage;
    public DamageSpecifier? AfterDamage;
    public DamageSpecifier? DealtDamage;
    public DamageSpecifier? ChangedDelta;

    public void Reset()
    {
        Order.Clear();
        MutatePipeline = false;
        SetLocationalTarget = false;
        CancelBefore = false;
        EmptyAfter = false;
        BeforeTool = null;
        ModifyTool = null;
        AfterTool = null;
        DealtTool = null;
        ChangedTool = null;
        ModifyArmorPiercing = 0;
        ModifyImpact = default;
        AfterImpact = default;
        DealtImpact = default;
        ChangedImpact = default;
        ModifySlots = default;
        ModifyPart = null;
        ModifyZone = null;
        ModifyDamage = null;
        AfterDamage = null;
        DealtDamage = null;
        ChangedDelta = null;
    }
}

public sealed class DamageMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageMergeProbeComponent, BeforeDamageChangedEvent>(OnBefore);
        SubscribeLocalEvent<DamageMergeProbeComponent, DamageModifyEvent>(OnModify);
        SubscribeLocalEvent<DamageMergeProbeComponent, DamageModifyAfterResistEvent>(OnAfter);
        SubscribeLocalEvent<DamageMergeProbeComponent, DamageDealtEvent>(OnDealt);
        SubscribeLocalEvent<DamageMergeProbeComponent, DamageChangedEvent>(OnChanged);
    }

    private static void OnBefore(Entity<DamageMergeProbeComponent> ent, ref BeforeDamageChangedEvent args)
    {
        ent.Comp.Order.Add("before");
        ent.Comp.BeforeTool = args.Source;
        if (ent.Comp.SetLocationalTarget)
        {
            args.TargetSlots = SlotFlags.HEAD;
            args.TargetPart = BodyPartType.Head;
            args.TargetZone = TargetBodyZone.Head;
        }

        if (ent.Comp.CancelBefore)
            args.Cancelled = true;
    }

    private static void OnModify(Entity<DamageMergeProbeComponent> ent, ref DamageModifyEvent args)
    {
        ent.Comp.Order.Add("modify");
        ent.Comp.ModifyDamage = args.Damage.Clone();
        ent.Comp.ModifyTool = args.Tool;
        ent.Comp.ModifyArmorPiercing = args.ArmorPiercing;
        ent.Comp.ModifyImpact = args.Impact;
        ent.Comp.ModifySlots = args.TargetSlots;
        ent.Comp.ModifyPart = args.TargetPart;
        ent.Comp.ModifyZone = args.TargetZone;
        if (ent.Comp.MutatePipeline)
        {
            args.Damage *= 2;
            args.Impact = DamageImpact.Explosion;
        }
    }

    private static void OnAfter(Entity<DamageMergeProbeComponent> ent, ref DamageModifyAfterResistEvent args)
    {
        ent.Comp.Order.Add("after");
        ent.Comp.AfterDamage = args.Damage.Clone();
        ent.Comp.AfterTool = args.Tool;
        ent.Comp.AfterImpact = args.Impact;
        if (ent.Comp.EmptyAfter)
        {
            args.Damage = new DamageSpecifier();
            return;
        }

        if (ent.Comp.MutatePipeline)
        {
            args.Damage += DamageableMergeRegressionTestDamage.OneBlunt;
            args.Impact = DamageImpact.SnaggingContact;
        }
    }

    private static void OnDealt(Entity<DamageMergeProbeComponent> ent, ref DamageDealtEvent args)
    {
        ent.Comp.Order.Add("dealt");
        ent.Comp.DealtDamage = args.Damage.Clone();
        ent.Comp.DealtTool = args.Tool;
        ent.Comp.DealtImpact = args.Impact;
    }

    private static void OnChanged(Entity<DamageMergeProbeComponent> ent, ref DamageChangedEvent args)
    {
        ent.Comp.Order.Add("changed");
        ent.Comp.ChangedDelta = args.DamageDelta?.Clone();
        ent.Comp.ChangedTool = args.Tool;
        ent.Comp.ChangedImpact = args.Impact;
    }
}

internal static class DamageableMergeRegressionTestDamage
{
    public static readonly DamageSpecifier OneBlunt = new()
    {
        DamageDict =
        {
            ["Blunt"] = FixedPoint2.New(1),
        },
    };
}

[Serializable, NetSerializable]
public sealed partial class DamageMergeDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}

#pragma warning restore RA0002
