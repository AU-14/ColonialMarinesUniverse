using Content.Server.Maps;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
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
    public async Task MedicompVariantsUseCmss13SizeAndEquipSlot()
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

                foreach (var prototype in MedicompVariants)
                {
                    var medicomp = entMan.SpawnEntity(prototype, gridCoords);

                    Assert.That(entMan.GetComponent<ItemComponent>(medicomp).Size.Id, Is.EqualTo("Small"), prototype);
                    Assert.That(entMan.GetComponent<ClothingComponent>(medicomp).Slots,
                        Is.EqualTo(SlotFlags.SUITSTORAGE),
                        prototype);
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
