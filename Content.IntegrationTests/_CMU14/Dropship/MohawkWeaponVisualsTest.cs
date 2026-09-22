using System.Linq;
using System.Numerics;
using Content.Client.Clickable;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.PowerLoader;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkWeaponVisualsTest
{
    [Test]
    public async Task StandardMountsKeepTheirOriginalAttachmentArtwork()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        NetEntity weapon = default;
        NetEntity engine = default;
        EntityUid map = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            map = entities.System<SharedMapSystem>().CreateMap();
            var coordinates = new EntityCoordinates(map, Vector2.Zero);
            var weaponPoint = entities.SpawnEntity("CMAlamoWall61", coordinates);
            var enginePoint = entities.SpawnEntity("RMCEngineAttachmentPoint", coordinates);
            weapon = entities.GetNetEntity(weaponPoint);
            engine = entities.GetNetEntity(enginePoint);
            var appearance = entities.System<SharedAppearanceSystem>();
            appearance.SetData(weaponPoint, DropshipWeaponVisuals.Sprite, "_RMC14/Objects/dropship_attachments.rsi");
            appearance.SetData(weaponPoint, DropshipWeaponVisuals.State, "30mm_cannon_installed");
            appearance.SetData(enginePoint, DropshipUtilityVisuals.Sprite, "_RMC14/Objects/dropship_attachments.rsi");
            appearance.SetData(enginePoint, DropshipUtilityVisuals.State, "cooling_system_installed");
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            entities.System<AppearanceSystem>().FrameUpdate(0f);
            var weaponLayer = entities.GetComponent<SpriteComponent>(entities.GetEntity(weapon))[DropshipWeaponPointLayers.Layer];
            Assert.That(weaponLayer.Visible, Is.True);
            Assert.That(weaponLayer.RsiState.ToString(), Is.EqualTo("30mm_cannon_installed"));
            Assert.That(((SpriteComponent.Layer) weaponLayer).DirOffset, Is.EqualTo(SpriteComponent.DirectionOffset.Clockwise));
            var engineLayer = entities.GetComponent<SpriteComponent>(entities.GetEntity(engine))[DropshipPointVisualsLayers.AttachedUtility];
            Assert.That(engineLayer.Visible, Is.True);
            Assert.That(engineLayer.RsiState.ToString(), Is.EqualTo("cooling_system_installed"));
            Assert.That(((SpriteComponent.Layer) engineLayer).Rotation, Is.EqualTo(Angle.FromDegrees(180)));
        });
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.DeleteEntity(map));
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha", "RMCDropshipAttachmentGau21Cannon", "RMCDropshipAttachmentAmmoGAU", "30mm_cannon", 64)]
    [TestCase("midway", "RMCDropshipAttachmentGau21Cannon", "RMCDropshipAttachmentAmmoGAU", "30mm_cannon", 64)]
    [TestCase("omaha_navy", "RMCDropshipAttachmentGau21Cannon", "RMCDropshipAttachmentAmmoGAU", "30mm_cannon", 64)]
    [TestCase("midway_navy", "RMCDropshipAttachmentGau21Cannon", "RMCDropshipAttachmentAmmoGAU", "30mm_cannon", 64)]
    [TestCase("omaha", "RMCDropshipAttachmentGuidedMissileLauncher", "RMCDropshipAttachmentAmmoRocketWidowmaker", "rocket_pod", 51)]
    [TestCase("midway", "RMCDropshipAttachmentRocketPod", "RMCDropshipAttachmentAmmoRocketMiniMike", "minirocket_pod", 40)]
    public async Task InstalledWeaponsAndAmmoAppearOnConnectedClients(string variant, string weaponPrototype,
        string ammoPrototype, string fullState, int fullHeight)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        EntityUid ship = default;
        var mounts = new Dictionary<NetEntity, (EntityUid Weapon, string State)>();
        var ammunition = new Dictionary<EntityUid, EntityUid>();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedMapSystem>().CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
        });
        await pair.RunSeconds(1);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var containers = entities.System<SharedContainerSystem>();
            foreach (var point in entities.EntityQuery<DropshipWeaponPointComponent>())
            {
                var slot = containers.EnsureContainer<ContainerSlot>(point.Owner, point.WeaponContainerSlotId);
                var weapon = slot.ContainedEntity;
                if (weapon == null)
                {
                    weapon = entities.SpawnEntity(weaponPrototype,
                        entities.GetComponent<TransformComponent>(point.Owner).Coordinates);
                    Assert.That(containers.Insert(weapon.Value, slot), Is.True);
                }
                var attached = entities.GetComponent<DropshipWeaponComponent>(weapon.Value).WeaponAttachedSprite!;
                mounts.Add(entities.GetNetEntity(point.Owner),
                    (weapon.Value, attached.RsiState.ToString() == "m90_minigun" ? "m90_minigun" : fullState));
            }
            Assert.That(mounts, Has.Count.EqualTo(variant.StartsWith("omaha") ? 4 : 5));
        });
        await pair.RunUntilSynced();

        async Task AssertVisible(bool visible)
        {
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                entities.System<AppearanceSystem>().FrameUpdate(0f);
                foreach (var (net, expected) in mounts)
                {
                    var sprite = entities.GetComponent<SpriteComponent>(entities.GetEntity(net));
                    var layer = sprite[DropshipWeaponPointLayers.Layer];
                    if (expected.State == "m90_minigun")
                        Assert.That(((SpriteComponent.Layer) layer).Offset, Is.EqualTo(new Vector2(1.5f, 1f)),
                            "The chin gun must retain the source's 96px origin and 16px horizontal placement offset.");
                    Assert.That(layer.Visible, Is.EqualTo(visible), $"{net} attachment visibility");
                    if (visible)
                    {
                        Assert.That(layer.RsiState.ToString(), Is.EqualTo(expected.State), $"{net} installed weapon artwork");
                        Assert.That(sprite.Visible && sprite.Color.A > 0f && layer.Color.A > 0f, Is.True);
                        var rsi = layer.ActualRsi!;
                        var state = rsi[layer.RsiState];
                        var direction = sprite.DirectionOverride.Convert(state.RsiDirections)
                            .OffsetRsiDir(((SpriteComponent.Layer) layer).DirOffset);
                        var clicks = pair.Client.ResolveDependency<IClickMapManager>();
                        var minY = rsi.Size.Y;
                        var maxY = -1;
                        for (var x = 0; x < rsi.Size.X; x++)
                        for (var y = 0; y < rsi.Size.Y; y++)
                        {
                            if (!clicks.IsOccluding(rsi, layer.RsiState, direction, 0, new Vector2i(x, y)))
                                continue;
                            minY = Math.Min(minY, y);
                            maxY = Math.Max(maxY, y);
                        }
                        Assert.That(maxY, Is.GreaterThanOrEqualTo(minY), $"{net} selects a transparent frame.");
                        if (expected.State != "m90_minigun")
                        {
                            // Click maps expand opaque pixels by two pixels, clipped to the frame.
                            Assert.That(maxY - minY + 1, Is.EqualTo(Math.Min(rsi.Size.Y, fullHeight + 4)),
                                "The underside must display the whole weapon, not the short hull-edge fragment.");
                            Assert.That(((SpriteComponent.Layer) layer).DirOffset, Is.EqualTo(SpriteComponent.DirectionOffset.None));
                        }
                    }
                }
            });
        }

        await AssertVisible(true);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var containers = entities.System<SharedContainerSystem>();
            foreach (var net in mounts.Keys.ToArray())
            {
                var uid = entities.GetEntity(net);
                var point = entities.GetComponent<DropshipWeaponPointComponent>(uid);
                var ammo = entities.SpawnEntity(mounts[net].State == "m90_minigun" ? "CMUMohawkM90Ammo" : ammoPrototype,
                    entities.GetComponent<TransformComponent>(uid).Coordinates);
                Assert.That(containers.Insert(ammo, containers.EnsureContainer<ContainerSlot>(uid, point.AmmoContainerSlotId)), Is.True);
                ammunition.Add(uid, ammo);
            }
        });
        await pair.RunUntilSynced();
        await AssertVisible(true);
        foreach (var remaining in new[] { 5, 4, 3, 2, 1, 0 })
        {
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                foreach (var (point, ammo) in ammunition)
                {
                    var rounds = entities.GetComponent<DropshipAmmoComponent>(ammo);
#pragma warning disable RA0002 // Exercise each visual threshold without firing timed CAS volleys.
                    rounds.Rounds = Math.Min(remaining, rounds.MaxRounds);
#pragma warning restore RA0002
                    entities.System<PowerLoaderSystem>().SyncAppearance(point);
                }
            });
            await pair.RunUntilSynced();
            await AssertVisible(true);
        }
        await pair.Server.WaitAssertion(() =>
        {
            foreach (var installed in mounts.Values)
                pair.Server.EntMan.DeleteEntity(installed.Weapon);
        });
        await pair.RunUntilSynced();
        await AssertVisible(false);
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.DeleteEntity(ship));
        await pair.CleanReturnAsync();
    }
}
