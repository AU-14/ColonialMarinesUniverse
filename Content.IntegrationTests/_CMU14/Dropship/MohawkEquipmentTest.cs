using System.Linq;
using System.Numerics;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Light;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared._RMC14.PowerLoader.Events;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared.Physics;
using Content.Shared.Radio.Components;
using Robust.Client.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkEquipmentTest
{
    [TestCase("omaha")]
    [TestCase("midway")]
    [TestCase("omaha_navy")]
    [TestCase("midway_navy")]
    public async Task CabinEquipmentIsPoweredMountedAndSolid(string variant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        EntityUid ship = default;
        EntityUid table = default;
        var fixtures = new Dictionary<NetEntity, (Vector2 Position, Angle Rotation, string Kind)>();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedMapSystem>().CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            foreach (var xform in entities.EntityQuery<TransformComponent>().Where(t => t.ParentUid == ship))
            {
                var meta = entities.GetComponent<MetaDataComponent>(xform.Owner);
                Assert.That(meta.EntityName, Is.Not.AnyOf("Omaha", "Midway", "Mohawk hull", "Platform"));
                Assert.That(meta.EntityPrototype?.ID, Is.Not.AnyOf("RMCCPRDummy", "CMVendorMedicalAllAccess"));
                var parent = meta.EntityPrototype?.Parents?.FirstOrDefault();
                if (parent == "CMUMohawkMapTable")
                    table = xform.Owner;
                if (parent is not ("CMUMohawkCamera" or "CMUMohawkIntercom" or "CMUMohawkMedicalCabinet"
                    or "CMUMohawkLight" or "CMUMohawkBlueLight"))
                    continue;
                Assert.That(xform.Anchored, Is.True, $"{meta.EntityName} must stay attached to the cabin.");
                fixtures.Add(entities.GetNetEntity(xform.Owner), (xform.LocalPosition, xform.LocalRotation, parent));
            }
            Assert.That(table, Is.Not.EqualTo(default(EntityUid)));
            Assert.That(fixtures, Has.Count.EqualTo(variant.StartsWith("omaha") ? 23 : 18));

            var mounts = entities.EntityQuery<DropshipUtilityPointComponent>()
                .Where(p => entities.GetComponent<TransformComponent>(p.Owner).ParentUid == ship &&
                            entities.GetComponent<MetaDataComponent>(p.Owner).EntityPrototype?.ID.StartsWith("CMUMohawkInternal") == true)
                .ToArray();
            Assert.That(mounts, Has.Length.EqualTo(variant.StartsWith("omaha") ? 2 : 3));
            var expectedMounts = variant.StartsWith("omaha")
                ? new[] { new Vector2(-2.5f, -7.5f), new Vector2(3.5f, -7.5f) }
                : new[] { new Vector2(-2.5f, -4.5f), new Vector2(0.5f, -7.5f), new Vector2(3.5f, -4.5f) };
            Assert.That(mounts.Select(m => entities.GetComponent<TransformComponent>(m.Owner).LocalPosition),
                Is.EquivalentTo(expectedMounts));
            foreach (var mount in mounts)
            {
                var xform = entities.GetComponent<TransformComponent>(mount.Owner);
                Assert.That(xform.Anchored, Is.True);
                Assert.That(entities.HasComponent<GunshipHardpointAttachmentPointComponent>(mount.Owner), Is.False);
                Assert.That(entities.System<SharedDropshipSystem>().TryGetGridDropship(mount.Owner, out var owner), Is.True);
                Assert.That(owner.Owner, Is.EqualTo(ship));
                Assert.That(owner.Comp.AttachmentPoints, Does.Contain(mount.Owner));
                var loader = entities.SpawnEntity("RMCMechPowerLoader", xform.Coordinates);
                var medevac = entities.SpawnEntity("RMCDropshipAttachmentMedevac", xform.Coordinates);
                var slot = new GetAttachmentSlotEvent(entities.GetNetEntity(loader), entities.GetNetEntity(medevac));
                entities.EventBus.RaiseLocalEvent(mount.Owner, slot);
                Assert.That(slot.CanUse, Is.True, "The internal mount must accept a real medevac module.");
                Assert.That(slot.SlotId, Is.EqualTo(mount.UtilitySlotId));
                entities.DeleteEntity(loader);
                entities.DeleteEntity(medevac);
            }
        });
        await pair.RunSeconds(2);

        foreach (var degrees in new[] { 0, 90 })
        {
            await pair.Server.WaitAssertion(() =>
                pair.Server.EntMan.System<SharedTransformSystem>().SetWorldRotation(ship, Angle.FromDegrees(degrees)));
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                var transform = entities.System<SharedTransformSystem>();
                foreach (var receiver in entities.EntityQuery<ApcPowerReceiverComponent>()
                             .Where(r => entities.GetComponent<TransformComponent>(r.Owner).GridUid == ship))
                    Assert.That(entities.System<PowerReceiverSystem>().IsPowered(receiver.Owner), Is.True,
                        $"{entities.GetComponent<MetaDataComponent>(receiver.Owner).EntityName} needs onboard power.");
                foreach (var intercom in entities.EntityQuery<IntercomComponent>())
                {
                    Assert.That(intercom.CurrentChannel?.ToString(), Is.EqualTo("MarineCommon"));
                    Assert.That(entities.GetComponent<RadioSpeakerComponent>(intercom.Owner).Enabled, Is.True);
                }
                var tablePosition = entities.GetComponent<TransformComponent>(table).LocalPosition;
                for (var row = 0; row < 2; row++)
                {
                    var point = transform.ToMapCoordinates(new EntityCoordinates(ship, tablePosition + new Vector2(0, row)));
                    var hits = new HashSet<FixtureProxy>();
                    entities.System<EntityLookupSystem>().GetFixturesIntersecting(point.MapId,
                        Box2.CenteredAround(point.Position, new Vector2(0.2f)), hits,
                        new FixtureQueryArgs(new QueryFilter
                        {
                            LayerBits = (long) CollisionGroup.MobLayer,
                            MaskBits = (long) CollisionGroup.MobMask,
                            Flags = QueryFlags.Static,
                        }));
                    Assert.That(hits.Any(hit => hit.Entity == table && hit.Fixture.Hard), Is.True,
                        $"The map table must block people in both rows, including after ship rotation ({degrees}).");
                }
            });
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var transform = entities.System<SharedTransformSystem>();
                foreach (var (net, expected) in fixtures)
                {
                    var uid = entities.GetEntity(net);
                    var xform = entities.GetComponent<TransformComponent>(uid);
                    var sprite = entities.GetComponent<SpriteComponent>(uid);
                    Assert.That(xform.Anchored, Is.True);
                    Assert.That(Vector2.Distance(xform.LocalPosition, expected.Position), Is.LessThan(0.001f));
                    Assert.That(xform.LocalRotation, Is.EqualTo(expected.Rotation));
                    Assert.That(sprite.NoRotation, Is.False);
                    Assert.That(sprite.DrawDepth, Is.GreaterThan((int) Content.Shared.DrawDepth.DrawDepth.Walls));
                    if (expected.Kind == "CMUMohawkMedicalCabinet")
                        Assert.That(sprite.BaseRSI!.Path.ToString(), Does.EndWith("/nanomed.rsi"));
                    if (expected.Kind is "CMUMohawkLight" or "CMUMohawkBlueLight")
                    {
                        Assert.That(entities.HasComponent<RMCLightOffsetComponent>(uid), Is.False);
                        Assert.That(sprite.BaseRSI!.Path.ToString(), Does.EndWith("/Lighting/light_tube.rsi"));
                    }
                    if (expected.Kind == "CMUMohawkCamera")
                    {
                        var mounting = expected.Position switch
                        {
                            { X: -3.5f } => new Vector2(0.5f, 0),
                            { X: -2.5f } or { X: 4.5f } => new Vector2(-0.5f, 0),
                            { X: -0.5f } => new Vector2(0, -0.5f),
                            _ => new Vector2(0, 1),
                        };
                        var actual = transform.GetWorldRotation(uid).RotateVec(sprite.Offset);
                        Assert.That(Vector2.Distance(actual, Angle.FromDegrees(degrees).RotateVec(mounting)), Is.LessThan(0.001f),
                            "Facing and ship rotation must not displace cameras from their mounts.");
                    }
                }
            });
        }
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.DeleteEntity(ship));
        await pair.CleanReturnAsync();
    }
}
