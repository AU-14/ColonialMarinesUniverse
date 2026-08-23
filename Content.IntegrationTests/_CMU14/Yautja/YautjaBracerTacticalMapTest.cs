using System.Numerics;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaBracerTacticalMapTest
{
    private static readonly SpriteSpecifier.Rsi PredatorIcon =
        new(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "predator");

    private static readonly SpriteSpecifier.Rsi StolenBracerIcon =
        new(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "bracer_stolen");

    private static readonly SpriteSpecifier.Rsi HellhoundIcon =
        new(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "hellhound");

    [Test]
    public async Task HunterBracerEquipsYautjaOnlyTacticalMapMarkerLikeCmss13Minimap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            entMan.EnsureComponent<TacticalMapComponent>(map.Grid);
            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            entMan.EnsureComponent<YautjaComponent>(hunter);

            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var tacticalMaps = entMan.System<TacticalMapSystem>();
            var tacticalMap = entMan.GetComponent<TacticalMapComponent>(map.Grid);
            var icon = entMan.GetComponent<TacticalMapIconComponent>(hunter);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<YautjaMapTrackedComponent>(hunter), Is.True,
                    "CMSS13 normal hunter bracer equipped in WEAR_HANDS registers a Yautja minimap marker.");
                Assert.That(icon.Icon, Is.EqualTo(PredatorIcon),
                    "Local default Yautja profile has no assigned_equipment_preset, so the adapter uses the imported CMSS13 predator blip.");
                Assert.That(tacticalMaps.TryGetBlip(tacticalMap, "YAUTJA", hunter.Id, out var blip), Is.True,
                    "The marker must live in a Yautja-only tactical-map bucket, matching MINIMAP_FLAG_YAUTJA rather than leaking into marine/opfor maps.");
                Assert.That(blip.Image, Is.EqualTo(PredatorIcon));
                Assert.That(tacticalMaps.TryGetBlip(tacticalMap, "MARINES", hunter.Id, out _), Is.False,
                    "Bracer-owned Yautja minimap markers must not be exposed through the marine tactical-map bucket.");
            });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (!entMan.Deleted(bracer))
                entMan.DeleteEntity(bracer);
            if (!entMan.Deleted(hunter))
                entMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterBracerDropRemovesYautjaTacticalMapMarkerLikeCmss13Dropped()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            entMan.EnsureComponent<TacticalMapComponent>(map.Grid);
            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            entMan.EnsureComponent<YautjaComponent>(hunter);

            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var tacticalMaps = entMan.System<TacticalMapSystem>();
            var tacticalMap = entMan.GetComponent<TacticalMapComponent>(map.Grid);
            Assert.That(tacticalMaps.TryGetBlip(tacticalMap, "YAUTJA", hunter.Id, out _), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            Assert.That(inventory.TryUnequip(hunter, "gloves", true, true), Is.True);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var tacticalMaps = entMan.System<TacticalMapSystem>();
            var tacticalMap = entMan.GetComponent<TacticalMapComponent>(map.Grid);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<YautjaMapTrackedComponent>(hunter), Is.False,
                    "CMSS13 bracer dropped() unregisters the minimap signal and removes the marker from the wearer.");
                Assert.That(tacticalMaps.TryGetBlip(tacticalMap, "YAUTJA", hunter.Id, out _), Is.False,
                    "The bracer-owned Yautja tactical marker should be removed when the bracer leaves the gloves slot.");
                Assert.That(entMan.HasComponent<TacticalMapTrackedComponent>(hunter), Is.True,
                    "Dropping the Yautja bracer must not remove the human's preexisting generic tactical tracking component.");
            });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (!entMan.Deleted(bracer))
                entMan.DeleteEntity(bracer);
            if (!entMan.Deleted(hunter))
                entMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterBracerDropRestoresExistingTacticalMapIcon()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var tacticalMaps = entMan.System<TacticalMapSystem>();

            entMan.EnsureComponent<TacticalMapComponent>(map.Grid);
            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            entMan.EnsureComponent<YautjaComponent>(hunter);

            tacticalMaps.SetIcon(hunter, HellhoundIcon);
            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<TacticalMapIconComponent>(hunter).Icon, Is.EqualTo(PredatorIcon));
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            Assert.That(inventory.TryUnequip(hunter, "gloves", true, true), Is.True);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var tacticalMap = entMan.GetComponent<TacticalMapComponent>(map.Grid);
            var tacticalMaps = entMan.System<TacticalMapSystem>();
            var icon = entMan.GetComponent<TacticalMapIconComponent>(hunter);

            Assert.Multiple(() =>
            {
                Assert.That(icon.Icon, Is.EqualTo(HellhoundIcon),
                    "The bracer-owned marker must restore a preexisting tactical-map icon instead of deleting it on drop.");
                Assert.That(tacticalMaps.TryGetBlip(tacticalMap, "YAUTJA", hunter.Id, out _), Is.False);
            });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (!entMan.Deleted(bracer))
                entMan.DeleteEntity(bracer);
            if (!entMan.Deleted(hunter))
                entMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonYautjaWearingHunterBracerUsesCmss13StolenBracerTacticalMapIcon()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wearer = default;
        EntityUid bracer = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            entMan.EnsureComponent<TacticalMapComponent>(map.Grid);
            wearer = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            Assert.That(inventory.TryEquip(wearer, bracer, "gloves", silent: true, force: true), Is.True);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var tacticalMaps = entMan.System<TacticalMapSystem>();
            var tacticalMap = entMan.GetComponent<TacticalMapComponent>(map.Grid);
            var icon = entMan.GetComponent<TacticalMapIconComponent>(wearer);

            Assert.Multiple(() =>
            {
                Assert.That(icon.Icon, Is.EqualTo(StolenBracerIcon),
                    "CMSS13 update_minimap_icon() uses map_blips.dmi bracer_stolen when a non-Yautja owns a hunter bracer marker.");
                Assert.That(tacticalMaps.TryGetBlip(tacticalMap, "YAUTJA", wearer.Id, out var blip), Is.True);
                Assert.That(blip.Image, Is.EqualTo(StolenBracerIcon));
                Assert.That(tacticalMaps.TryGetBlip(tacticalMap, "MARINES", wearer.Id, out _), Is.False,
                    "A stolen hunter bracer marker still uses the Yautja minimap flag, not the wearer's ordinary marine tactical-map bucket.");
            });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (!entMan.Deleted(bracer))
                entMan.DeleteEntity(bracer);
            if (!entMan.Deleted(wearer))
                entMan.DeleteEntity(wearer);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodBracerEquipDoesNotRegisterNormalCmss13MinimapMarker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            entMan.EnsureComponent<TacticalMapComponent>(map.Grid);
            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            bracer = entMan.SpawnEntity("CMUYautjaBadBloodBracer", map.GridCoords);
            entMan.EnsureComponent<YautjaComponent>(hunter);

            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var tacticalMaps = entMan.System<TacticalMapSystem>();
            var tacticalMap = entMan.GetComponent<TacticalMapComponent>(map.Grid);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<YautjaMapTrackedComponent>(hunter), Is.False,
                    "CMSS13 base bracer equipped() returns before minimap registration when src.badblood is true.");
                Assert.That(tacticalMaps.TryGetBlip(tacticalMap, "YAUTJA", hunter.Id, out _), Is.False);
            });
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            if (!entMan.Deleted(bracer))
                entMan.DeleteEntity(bracer);
            if (!entMan.Deleted(hunter))
                entMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }
}
