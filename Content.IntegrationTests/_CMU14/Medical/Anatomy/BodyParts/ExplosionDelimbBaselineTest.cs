#pragma warning disable RA0002 // Balance fixture reads authored explosion and regional injury data.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Inventory;
using Content.Shared.Explosion.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;

/// <summary>Opt-in balance measurement using the world explosion flood-fill and real equipped armor.</summary>
[TestFixture]
public sealed class ExplosionDelimbBaselineTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test, Explicit("Balance experiment: select this test explicitly to print the blast matrix.")]
    public async Task MeasureAuthoredExplosions()
    {
        var map = await Pair.CreateTestMap();
        var queued = typeof(ExplosionSystem).GetField("_explosionQueue", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var active = typeof(ExplosionSystem).GetField("_activeExplosion", BindingFlags.Instance | BindingFlags.NonPublic)!;
        string[] profiles = ["CMGrenadeHighExplosive", "CMGrenadeFrag", "RMCExplosivePlastic",
            "AU14IED", "AU14IEDLarge", "CMULTBCannonImpactHE", "CMULTBCannonImpactHEAT",
            "CMULTBCannonImpactNapalm", "RMCProjectileRocket84mm", "RMCProjectileRocket84mmAntiArmor",
            "CMUProjectileRocket70mmHE", "RMCProjectileRocketHJRA12HE", "RMCProjectileRocketHJRA12AT",
            "RMCDropshipMissileExplosionWidowmaker", "RMCDropshipMissileExplosionKeeper",
            "RMCDropshipMissileExplosionHarpoon"];
        TestContext.Out.WriteLine("BLAST,Profile,Armor,Distance,Trials,Hit,AnyLimbLoss,ArmsLegsLost,HandsFeetLost,MeanDamage,MaxSeveranceFraction");
        foreach (var profile in profiles)
        {
            var samples = new List<(EntityUid Body, Vector2 Center, bool Armored, int Distance, BlastBalanceProbeComponent Probe)>();
            RMCExplosion blast = default!;
            await Server.WaitAssertion(() =>
            {
                Server.ResolveDependency<IRobustRandom>().SetSeed(5397);
                map.Grid.Comp.CanSplit = false;
                var prototype = SProtoMan.Index<EntityPrototype>(profile);
                if (prototype.Components.TryGetValue("Explosive", out var entry))
                {
                    var explosive = (ExplosiveComponent) entry.Component;
                    blast = new RMCExplosion { Type = explosive.ExplosionType, Total = explosive.TotalIntensity,
                        Slope = explosive.IntensitySlope, Max = explosive.MaxIntensity };
                }
                else
                {
                    blast = ((DropshipAmmoComponent) prototype.Components["DropshipAmmo"].Component).Explosion!;
                }
                Assert.That(blast, Is.Not.Null, profile);
                foreach (var armored in new[] { false, true })
                foreach (var distance in new[] { 0, 1, 2, 4, 6 })
                for (var trial = 0; trial < 8; trial++)
                {
                    var center = new Vector2(64.5f + samples.Count % 10 * 64, 64.5f + samples.Count / 10 * 64);
                    var maps = Server.System<SharedMapSystem>();
                    for (var x = -10; x <= 10; x++)
                    for (var y = -10; y <= 10; y++)
                        maps.SetTile(map.Grid.Owner, map.Grid.Comp,
                            new Vector2i((int) center.X + x, (int) center.Y + y), map.Tile.Tile);
                    var body = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid.Owner, center + new Vector2(distance, 0)));
                    Server.System<SharedTransformSystem>().SetLocalRotation(body, Angle.FromDegrees(trial % 4 * 90));
                    if (armored)
                    {
                        var armor = SEntMan.SpawnEntity("AU14ArmorM3JungleOne", new EntityCoordinates(body, Vector2.Zero));
                        Assert.That(Server.System<InventorySystem>().TryEquip(body, armor, "outerClothing", silent: true, force: true), Is.True);
                        var helmet = SEntMan.SpawnEntity("ArmorHelmetM10", new EntityCoordinates(body, Vector2.Zero));
                        Assert.That(Server.System<InventorySystem>().TryEquip(body, helmet, "head", silent: true, force: true), Is.True);
                    }
                    var probe = SEntMan.AddComponent<BlastBalanceProbeComponent>(body);
                    samples.Add((body, center, armored, distance, probe));
                }
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                foreach (var sample in samples)
                    Server.System<ExplosionSystem>().QueueExplosion(Server.System<SharedTransformSystem>().ToMapCoordinates(new EntityCoordinates(map.Grid.Owner, sample.Center)),
                        blast.Type, blast.Total, blast.Slope, blast.Max, cause: null, maxTileBreak: 0, canCreateVacuum: false);
            });
            // Allow the explosion processing budget to drain before reading results.
            var drained = false;
            for (var ticks = 0; ticks < 3600 && !drained; ticks += 30)
            {
                await Pair.RunTicksSync(30);
                await Server.WaitAssertion(() =>
                {
                    var system = Server.System<ExplosionSystem>();
                    drained = ((System.Collections.ICollection) queued.GetValue(system)!).Count == 0 && active.GetValue(system) == null;
                });
            }
            await Server.WaitAssertion(() =>
            {
                Assert.That(drained, Is.True, $"Explosion queue must drain for {profile}");
                foreach (var group in samples.GroupBy(sample => (sample.Armored, sample.Distance)))
                {
                    Assert.That(group.Where(sample => sample.Distance == 0).All(sample => sample.Probe.Hits > 0), Is.True,
                        $"{profile} / {group.Key}: hit counts {string.Join(',', group.Select(sample => sample.Probe.Hits))}");
                    var hits = group.Count(sample => sample.Probe.Hits > 0);
                    var lost = group.Count(sample => sample.Probe.ArmsLegsLost + sample.Probe.HandsFeetLost > 0);
                    var arms = group.Sum(sample => sample.Probe.ArmsLegsLost);
                    var hands = group.Sum(sample => sample.Probe.HandsFeetLost);
                    var damage = group.Average(sample => sample.Probe.Damage);
                    var fraction = group.Max(sample => sample.Probe.MaxSeveranceFraction);
                    TestContext.Out.WriteLine(FormattableString.Invariant(
                        $"BLAST,{profile},{(group.Key.Armored ? "M3+M10" : "None")},{group.Key.Distance},{group.Count()},{hits},{lost},{arms},{hands},{damage:F2},{fraction:F3}"));
                }
                foreach (var sample in samples)
                    if (SEntMan.EntityExists(sample.Body)) SEntMan.DeleteEntity(sample.Body);
            });
        }
    }
}

[RegisterComponent]
public sealed partial class BlastBalanceProbeComponent : Component
{
    public int Hits;
    public int ArmsLegsLost;
    public int HandsFeetLost;
    public float Damage;
    public float MaxSeveranceFraction;
}

public sealed class BlastBalanceProbeSystem : EntitySystem
{
    [Dependency] private CMUMedicalBodyIndexSystem _index = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BlastBalanceProbeComponent, ExplosionReceivedEvent>(OnBlast,
            after: [typeof(CMUExplosionMedicalTraumaSystem)], before: [typeof(SharedRMCExplosionSystem)]);
    }

    private void OnBlast(Entity<BlastBalanceProbeComponent> ent, ref ExplosionReceivedEvent args)
    {
        ent.Comp.Hits++;
        ent.Comp.Damage += args.Damage.GetTotal().Float();
        var limbs = 0;
        var extremities = 0;
        foreach (var (part, anatomy) in _index.GetBodyParts(ent.Owner))
        {
            if (anatomy.PartType is BodyPartType.Arm or BodyPartType.Leg) limbs++;
            else if (anatomy.PartType is BodyPartType.Hand or BodyPartType.Foot) extremities++;
            else continue;
            if (TryComp<BodyPartHealthComponent>(part, out var health))
                ent.Comp.MaxSeveranceFraction = MathF.Max(ent.Comp.MaxSeveranceFraction,
                    (health.SeveranceDamage / (health.Max + health.SeveranceThreshold)).Float());
        }
        ent.Comp.ArmsLegsLost = 4 - limbs;
        ent.Comp.HandsFeetLost = 4 - extremities;
    }
}
