#pragma warning disable RA0002 // Observe committed regional health and source-owned resistance values.
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;

[TestFixture]
public sealed class QueuedMedicalExplosionTest : GameTest
{
    [TestCase(false, 1f, 1f, 0)]
    [TestCase(true, 1f, 1f, 0)]
    [TestCase(false, 0.5f, 0.8f, 0)]
    [TestCase(true, 0.5f, 0.8f, 0)]
    [TestCase(true, 1f, 1f, 1)]
    [TestCase(true, 1f, 1f, 2)]
    public async Task QueuedBlastCommitsOneModifiedAggregateAndOneSharePerRegion(
        bool roboticSubtree, float allModifier, float explosionModifier, int nestedPhase)
    {
        var map = await Pair.CreateTestMap();
        var config = Server.ResolveDependency<IConfigurationManager>();
        var oldAll = config.GetCVar(CCVars.PlaytestAllDamageModifier);
        var oldExplosion = config.GetCVar(CCVars.PlaytestExplosionDamageModifier);
        EntityUid patient = default;
        EntityUid? detached = null;
        QueuedMedicalExplosionProbeComponent probe = default!;
        var baseline = new Dictionary<EntityUid, FixedPoint2>();
        try
        {
            await Server.WaitAssertion(() =>
            {
                _ = Server.System<QueuedMedicalExplosionProbeSystem>();
                config.SetCVar(CCVars.PlaytestAllDamageModifier, allModifier);
                config.SetCVar(CCVars.PlaytestExplosionDamageModifier, explosionModifier);
                Assert.That(config.GetCVar(CMUMedicalCCVars.BodyPartDamagePropagation), Is.EqualTo(1f));
                var coordinates = new EntityCoordinates(map.Grid.Owner,
                    new Vector2(map.Tile.GridIndices.X + 0.5f, map.Tile.GridIndices.Y + 0.5f));
                patient = SEntMan.SpawnEntity("CMMobHuman", coordinates);
                var index = Server.System<CMUMedicalBodyIndexSystem>();
                if (roboticSubtree)
                {
                    var torso = Part(patient, BodyPartType.Torso);
                    var arm = Part(patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                    var slot = index.GetBodyPartSlots(torso).Single(entry => entry.Part == arm).SlotId;
                    detached = Server.System<DetachableOrganSystem>().Detach(arm);
                    Assert.That(detached, Is.Not.Null);
                    // A real replacement subtree preserves ten regions while changing
                    // their biological policy; no fixture-only robotic flag is substituted.
                    var replacement = SEntMan.SpawnEntity("CMUPartRoboticLeftArm", coordinates);
                    var hand = SEntMan.SpawnEntity("CMUPartRoboticLeftHand", coordinates);
                    var body = Server.System<SharedBodySystem>();
                    Assert.That(body.AttachPart(replacement, "left_hand", hand), Is.True);
                    Assert.That(body.AttachPart(torso, slot, replacement), Is.True);
                    Assert.That(SEntMan.HasComponent<CMURoboticLimbComponent>(replacement), Is.True);
                    SEntMan.DeleteEntity(detached.Value);
                    detached = null;
                }

                probe = SEntMan.AddComponent<QueuedMedicalExplosionProbeComponent>(patient);
                probe.NestedPhase = nestedPhase;
                probe.NestedPart = Part(patient, BodyPartType.Arm, BodyPartSymmetry.Right);
                foreach (var (part, _) in index.GetBodyParts(patient))
                {
                    var health = SEntMan.GetComponent<BodyPartHealthComponent>(part);
                    Assert.That(health.Current, Is.EqualTo(health.Max));
                    baseline.Add(part, health.Current);
                }
                Assert.That(baseline, Has.Count.EqualTo(10));
                Assert.That(Server.System<DamageableSystem>().GetAllDamage(patient).GetTotal(), Is.EqualTo(FixedPoint2.Zero));
                var prototype = Server.ProtoMan.Index<ExplosionPrototype>("RMC");
                Assert.That(prototype.DamagePerIntensity.DamageDict["Blunt"], Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(prototype.DamagePerIntensity.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(prototype.DamagePerIntensity.DamageDict, Has.Count.EqualTo(2));
            });
            // Populate the actual grid broadphase before queuing the blast.
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<TransformComponent>(patient).GridUid, Is.EqualTo(map.Grid.Owner));
                var epicenter = Server.System<SharedTransformSystem>().GetMapCoordinates(patient);
                // Total 2 is below slope/2 (3), so public flood-fill produces
                // exactly one tile at intensity 2. Floor destruction is unrelated.
                Server.System<ExplosionSystem>().QueueExplosion(epicenter, "RMC", 2f, 6f, 2f,
                    cause: null, maxTileBreak: 0, canCreateVacuum: false);
                Assert.That(probe.ReceivedCount, Is.Zero, "queueing itself must not synthesize a received event");
            });
            for (var ticks = 0; ticks < 30 && probe.ReceivedCount == 0; ticks++)
                await Pair.RunTicksSync(1);

            await Server.WaitAssertion(() =>
            {
                Assert.That(probe.ReceivedCount, Is.EqualTo(1), "a queued world blast must reach the patient exactly once");
                Assert.That(probe.ExplosionChangedCount, Is.EqualTo(1));
                Assert.That(probe.StateAtImpact, Is.EqualTo(MobState.Alive));
                Assert.That(probe.ExplosionTargetPart, Is.Null, "area damage cannot also target one generic hit location");
                Assert.That(probe.HitLocationCalls, Is.EqualTo(nestedPhase == 0 ? 0 : 1));
                Assert.That(probe.NestedApplied.GetTotal(), Is.EqualTo(FixedPoint2.New(nestedPhase == 0 ? 0 : 7)));
                Assert.That(probe.Received.DamageDict.GetValueOrDefault("Piercing"), Is.EqualTo(FixedPoint2.Zero),
                    "a nested direct hit must not be included in the enclosing blast result");

                // RMC=5/type/intensity, intensity=2, human vulnerability=2.25.
                // At the epicenter: exposure=.35*clamp(rawTotal/100,.2,1)+.65;
                // multiplier=.65+.7*exposure. These independent constants catch
                // duplicated vulnerability, exposure, and global multipliers.
                var expectedPerType = allModifier == 1f ? 27.343125f : 10.386f;
                foreach (var type in new[] { "Blunt", "Heat" })
                {
                    Assert.That(probe.Received.DamageDict[type].Float(), Is.EqualTo(expectedPerType).Within(0.011f), type);
                    Assert.That(probe.Prepared.DamageDict[type], Is.EqualTo(probe.Received.DamageDict[type]),
                        "the unarmored patient must not receive another universal modifier at commit");
                    Assert.That(probe.Aggregate.DamageDict[type], Is.EqualTo(probe.Received.DamageDict[type]));
                    Assert.That(probe.Regions.Values.Aggregate(FixedPoint2.Zero,
                        (sum, region) => sum + region.Debt.DamageDict.GetValueOrDefault(type)),
                        Is.EqualTo(probe.Received.DamageDict[type]), "regional shares exactly reconcile, including the rounding remainder");
                }
                Assert.That(probe.Regions, Has.Count.EqualTo(10));
                foreach (var (part, region) in probe.Regions)
                {
                    Assert.That(probe.RegionalExplosionCalls.GetValueOrDefault(part), Is.EqualTo(1), "no generic hit plus blast duplicate");
                    var nested = nestedPhase != 0 && part == probe.NestedPart ? FixedPoint2.New(7) : FixedPoint2.Zero;
                    Assert.That(region.Debt.DamageDict.GetValueOrDefault("Piercing"), Is.EqualTo(nested));
                    var brute = region.Debt.DamageDict["Blunt"] + nested;
                    var burn = region.Debt.DamageDict["Heat"];
                    var structural = brute * region.BruteResistance + burn * region.BurnResistance;
                    Assert.That((baseline[part] - region.Current).Float(), Is.EqualTo(structural.Float()).Within(0.025f),
                        "structural resistance applies once to the region's share");
                    Assert.That(region.Debt.DamageDict["Blunt"], Is.GreaterThan(FixedPoint2.Zero));
                    Assert.That(region.Debt.DamageDict["Heat"], Is.GreaterThan(FixedPoint2.Zero));
                }
                var leftArm = probe.Regions[Part(patient, BodyPartType.Arm, BodyPartSymmetry.Left)];
                if (allModifier == 1f)
                {
                    Assert.That(leftArm.WoundCount, Is.EqualTo(roboticSubtree ? 0 : 2),
                        "this actual arm share exceeds the wound threshold; robotics keep structural injury without organic wounds");
                    var torso = probe.Regions[Part(patient, BodyPartType.Torso)];
                    Assert.That(torso.WoundCount, Is.EqualTo(2), "one brute and one burn wound describe the same regional injury");
                    Assert.That(torso.WoundDamage.Float(), Is.EqualTo((baseline[Part(patient, BodyPartType.Torso)] - torso.Current).Float()).Within(0.025f));
                }

                // A subsequent ordinary hit resolves its own exact site after all
                // queued-blast and nested callbacks have unwound.
                var followupPart = Part(patient, BodyPartType.Hand, BodyPartSymmetry.Right);
                var before = Server.System<SharedBodyPartHealthSystem>().GetAttributedDamage(followupPart, "Slash");
                probe.DirectPart = followupPart;
                var followup = Server.System<DamageableSystem>().TryChangeDamage(patient,
                    new DamageSpecifier { DamageDict = { ["Slash"] = 3 } }, ignoreResistances: true, ignoreGlobalModifiers: true);
                Assert.That(followup!.GetTotal(), Is.EqualTo(FixedPoint2.New(3)));
                Assert.That(Server.System<SharedBodyPartHealthSystem>().GetAttributedDamage(followupPart, "Slash") - before,
                    Is.EqualTo(FixedPoint2.New(3)));
                Assert.That(probe.ReceivedCount, Is.EqualTo(1));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(patient)) SEntMan.DeleteEntity(patient);
                if (detached is { } carrier && SEntMan.EntityExists(carrier)) SEntMan.DeleteEntity(carrier);
                config.SetCVar(CCVars.PlaytestAllDamageModifier, oldAll);
                config.SetCVar(CCVars.PlaytestExplosionDamageModifier, oldExplosion);
            });
        }
    }

    private EntityUid Part(EntityUid patient, BodyPartType type, BodyPartSymmetry symmetry = BodyPartSymmetry.None)
    {
        Assert.That(Server.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient, new(type, symmetry), out var part), Is.True);
        return part;
    }
}

[RegisterComponent]
public sealed partial class QueuedMedicalExplosionProbeComponent : Component
{
    public int NestedPhase;
    public EntityUid NestedPart;
    public EntityUid? DirectPart;
    public bool NestedRan;
    public int HitLocationCalls;
    public int ExplosionChangedCount;
    public int ReceivedCount;
    public EntityUid? ExplosionTargetPart;
    public MobState? StateAtImpact;
    public DamageSpecifier NestedApplied = new();
    public DamageSpecifier Prepared = new();
    public DamageSpecifier Received = new();
    public DamageSpecifier Aggregate = new();
    public readonly Dictionary<EntityUid, int> RegionalExplosionCalls = new();
    public readonly Dictionary<EntityUid, QueuedExplosionRegionSnapshot> Regions = new();
}

public sealed record QueuedExplosionRegionSnapshot(DamageSpecifier Debt, FixedPoint2 Current,
    float BruteResistance, float BurnResistance, int WoundCount, FixedPoint2 WoundDamage);

public sealed class QueuedMedicalExplosionProbeSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _index = default!;
    [Dependency] private SharedBodyPartHealthSystem _parts = default!;
    [Dependency] private CMUWoundLedgerSystem _wounds = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<QueuedMedicalExplosionProbeComponent, ExplosionDamagePreparingEvent>(OnPreparing,
            after: [typeof(CMUExplosionMedicalTraumaSystem)]);
        SubscribeLocalEvent<QueuedMedicalExplosionProbeComponent, ExplosionReceivedEvent>(OnReceived,
            after: [typeof(CMUExplosionMedicalTraumaSystem)], before: [typeof(SharedRMCExplosionSystem)]);
        SubscribeLocalEvent<QueuedMedicalExplosionProbeComponent, DamageModifyAfterResistEvent>(OnAfterResist);
        SubscribeLocalEvent<QueuedMedicalExplosionProbeComponent, DamageChangedEvent>(OnChanged);
        SubscribeLocalEvent<QueuedMedicalExplosionProbeComponent, HitLocationResolveEvent>(OnResolve);
        SubscribeLocalEvent<BodyPartDamagedEvent>(OnPartDamage);
    }

    private void OnPreparing(Entity<QueuedMedicalExplosionProbeComponent> ent, ref ExplosionDamagePreparingEvent args)
        => ent.Comp.Prepared = args.Damage.Clone();

    private void OnAfterResist(Entity<QueuedMedicalExplosionProbeComponent> ent, ref DamageModifyAfterResistEvent args)
    {
        if (args.Impact.Delivery == DamageImpactDelivery.Explosion && ent.Comp.NestedPhase == 1)
            Nest(ent);
    }

    private void OnChanged(Entity<QueuedMedicalExplosionProbeComponent> ent, ref DamageChangedEvent args)
    {
        if (args.Impact.Delivery != DamageImpactDelivery.Explosion) return;
        ent.Comp.ExplosionChangedCount++;
        ent.Comp.ExplosionTargetPart = args.TargetPartEntity;
        if (ent.Comp.NestedPhase == 2) Nest(ent);
    }

    private void Nest(Entity<QueuedMedicalExplosionProbeComponent> ent)
    {
        if (ent.Comp.NestedRan) return;
        ent.Comp.NestedRan = true;
        ent.Comp.DirectPart = ent.Comp.NestedPart;
        ent.Comp.NestedApplied = _damageable.TryChangeDamage(ent.Owner,
            new DamageSpecifier { DamageDict = { ["Piercing"] = 7 } }, ignoreResistances: true,
            ignoreGlobalModifiers: true) ?? new();
        ent.Comp.DirectPart = null;
    }

    private void OnResolve(Entity<QueuedMedicalExplosionProbeComponent> ent, ref HitLocationResolveEvent args)
    {
        ent.Comp.HitLocationCalls++;
        if (ent.Comp.DirectPart is not { } part) return;
        args.ResolvedPartEntity = part;
        args.ResolvedPart = Comp<BodyPartComponent>(part).PartType;
        args.Handled = true;
    }

    private void OnPartDamage(ref BodyPartDamagedEvent args)
    {
        if (args.Impact.Delivery != DamageImpactDelivery.Explosion ||
            !TryComp<QueuedMedicalExplosionProbeComponent>(args.Body, out var probe)) return;
        probe.RegionalExplosionCalls[args.Part] = probe.RegionalExplosionCalls.GetValueOrDefault(args.Part) + 1;
    }

    private void OnReceived(Entity<QueuedMedicalExplosionProbeComponent> ent, ref ExplosionReceivedEvent args)
    {
        ent.Comp.ReceivedCount++;
        ent.Comp.Received = args.Damage.Clone();
        ent.Comp.Aggregate = _damageable.GetAllDamage(ent.Owner);
        ent.Comp.StateAtImpact = args.StateBeforeDamage;
        // Capture the coherent medical result before downstream blast stun/throw
        // and later physiology updates introduce unrelated movement or bleed damage.
        foreach (var (part, _) in _index.GetBodyParts(ent.Owner))
        {
            var health = Comp<BodyPartHealthComponent>(part);
            var debt = new DamageSpecifier();
            foreach (var type in new[] { "Blunt", "Heat", "Piercing" })
                debt.DamageDict[type] = _parts.GetAttributedDamage(part, type);
            IReadOnlyList<CMUWoundEntry> entries = TryComp<BodyPartWoundComponent>(part, out var wounds)
                ? _wounds.GetEntries(wounds) : [];
            ent.Comp.Regions[part] = new(debt, health.Current,
                health.Resistance.GetValueOrDefault("Brute", 1f), health.Resistance.GetValueOrDefault("Burn", 1f),
                entries.Count, entries.Aggregate(FixedPoint2.Zero, (sum, entry) => sum + entry.Wound.Damage));
        }
    }
}
