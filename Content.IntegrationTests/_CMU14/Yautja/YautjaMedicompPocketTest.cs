using Content.Server.Maps;
using Content.Server.Station.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaMedicompPocketTest
{
    private static readonly string[] MedicompVariants =
    [
        "CMUYautjaMedicomp",
        "CMUYautjaMedicompFull",
        "CMUYautjaMedicompSurvivor",
        "CMUYautjaMedicompThrall",
    ];

    [Test]
    public async Task MedicompVariantsFitYautjaPocket()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityCoordinates gridCoords = default;

        try
        {
            await server.WaitPost(() =>
            {
                var mapSystem = server.System<SharedMapSystem>();
                mapSystem.CreateMap(out var mapId);
                var grid = mapSystem.CreateGridEntity(mapId);
                gridCoords = new EntityCoordinates(grid, 0, 0);

                var tileDefinitionManager = server.ResolveDependency<ITileDefinitionManager>();
                mapSystem.SetTile(grid.Owner, grid.Comp, gridCoords,
                    new Tile(tileDefinitionManager["Plating"].TileId));
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                    .WithName("Medicomp Pocket Test")
                    .WithYautjaProfile(YautjaCharacterProfile.Default.WithName("Medicomp Pocket Test"));
                var yautja = entMan.System<StationSpawningSystem>().SpawnPlayerMob(
                    gridCoords,
                    "CMUYautjaHunter",
                    profile,
                    station: null);

                if (inventory.TryGetSlotEntity(yautja, "pocket2", out var existing))
                    entMan.DeleteEntity(existing.Value);

                foreach (var prototype in MedicompVariants)
                {
                    var medicomp = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);

                    Assert.That(entMan.GetComponent<ItemComponent>(medicomp).Size.Id, Is.EqualTo("Small"), prototype);
                    Assert.That(inventory.TryEquip(yautja, medicomp, "pocket2", silent: true), Is.True, prototype);
                    Assert.That(inventory.TryGetSlotEntity(yautja, "pocket2", out var equipped), Is.True, prototype);
                    Assert.That(equipped, Is.EqualTo(medicomp), prototype);
                    entMan.DeleteEntity(medicomp);
                }
            });
        }
        finally
        {
            server.Dispose();
        }

    }
}
