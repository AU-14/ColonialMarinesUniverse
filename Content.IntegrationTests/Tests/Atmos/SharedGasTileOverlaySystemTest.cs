using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.CCVar;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Atmos;

/// <summary>
/// Checks networking of temperature data inside GasTileOverlay
/// </summary>
public sealed partial class SharedGasTileOverlayTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/DeltaPressure/deltapressuretest.yml");
    public override PoolSettings PoolSettings => new()
    {
        Connected = true
    };

    [SidedDependency(Side.Server)] private readonly SharedMapSystem _mapSys = default!;
    [SidedDependency(Side.Server)] private ItemToggleSystem _itemToggle = default!;

    [Test]
    [EnsureCVar(Side.Server, typeof(RMCCVars), nameof(RMCCVars.RMCGasTileOverlayUpdate), true)]
    [Description("Checks networking of temperature data inside GasTileOverlay.")]
    public async Task TestGasTileTemperatureOverlayDataSync()
    {
        var (gridCoords, tileIndices, mixture, cOverlay) = await PrepareGasTileTest();

        await InjectHotPlasma(ProcessEnt, tileIndices, mixture, 400f);

        await CheckForInjectedGas(cOverlay, tileIndices, 400f);

        await InjectHotPlasma(ProcessEnt, tileIndices, mixture, 800f + ThermalByte.TempDegreeResolution - 1); // Rounding test

        await CheckForInjectedGas(cOverlay, tileIndices, 800f);

        await InjectHotPlasma(ProcessEnt, tileIndices, mixture, ThermalByte.TempMaximum + 200f); // This one hits max temperature

        await CheckForInjectedGas(cOverlay, tileIndices, ThermalByte.TempMaximum);

        await InjectHotPlasma(ProcessEnt, tileIndices, mixture, ThermalByte.TempMinimum);
        await InjectHotPlasma(ProcessEnt, tileIndices, mixture, ThermalByte.TempMinimum + (ThermalByte.TempDegreeResolution * 2) - 1); // Test the networking optimisation, this should not be networked yet

        await CheckForInjectedGas(cOverlay, tileIndices, ThermalByte.TempMinimum);

        await InjectHotPlasma(ProcessEnt, tileIndices, mixture, ThermalByte.TempMinimum + (ThermalByte.TempDegreeResolution * 2)); // This should

        await CheckForInjectedGas(cOverlay, tileIndices, ThermalByte.TempMinimum + (ThermalByte.TempDegreeResolution * 2));
    }

    [Test]
    [EnsureCVar(Side.Server, typeof(RMCCVars), nameof(RMCCVars.RMCGasTileOverlayUpdate), false)]
    public async Task DisabledOverlayRetainsInvalidationUntilEnabled()
    {
        var (_, tileIndices, mixture, cOverlay) = await PrepareGasTileTest(expectInitialChunks: false);

        await InjectHotPlasma(ProcessEnt, tileIndices, mixture, 400f);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProcessEnt.Comp2.InvalidTiles, Does.Contain(tileIndices));
                Assert.That(ProcessEnt.Comp2.Chunks, Is.Empty,
                    "The fork CVar defaults off so invalidated gas tiles must remain unprocessed.");
            });
        });
        await Client.WaitAssertion(() => Assert.That(cOverlay.Chunks, Is.Empty));

        await Server.WaitPost(() =>
        {
            var cfg = Server.ResolveDependency<IConfigurationManager>();
            cfg.SetCVar(RMCCVars.RMCGasTileOverlayUpdate, true);
            SAtmos.InvalidateVisuals((ProcessEnt.Owner, ProcessEnt.Comp2), tileIndices);
        });
        await Server.WaitRunTicks(10);
        await RunUntilSynced();

        await Server.WaitAssertion(() =>
            Assert.That(ProcessEnt.Comp2.InvalidTiles, Does.Not.Contain(tileIndices)));
        await CheckForInjectedGas(cOverlay, tileIndices, 400f);

        await Server.WaitPost(() =>
        {
            mixture.Clear();
            SAtmos.InvalidateVisuals((ProcessEnt.Owner, ProcessEnt.Comp2), tileIndices);
        });
        await Server.WaitRunTicks(10);
        await RunUntilSynced();
        await CheckForVacuum(cOverlay, tileIndices);
    }

    private async Task CheckForVacuum(GasTileOverlayComponent overlay, Vector2i indices)
    {
        await Client.WaitAssertion(() =>
        {
            var chunkIndices = SharedGasTileOverlaySystem.GetGasChunkIndices(indices);
            Assert.That(overlay.Chunks.TryGetValue(chunkIndices, out var chunk), Is.True);
            Assert.That(chunk, Is.Not.Null);

            var localX = MathHelper.Mod(indices.X, SharedGasTileOverlaySystem.ChunkSize);
            var localY = MathHelper.Mod(indices.Y, SharedGasTileOverlaySystem.ChunkSize);
            var tile = chunk.TileData[localX + localY * SharedGasTileOverlaySystem.ChunkSize];
            Assert.Multiple(() =>
            {
                Assert.That(tile.ByteGasTemperature.IsVacuum, Is.True);
                Assert.That(tile.ByteGasTemperature.TryGetTemperature(out var temperature), Is.True);
                Assert.That(temperature, Is.EqualTo(Atmospherics.TCMB));
            });
        });
    }

    private async Task CheckForInjectedGas(GasTileOverlayComponent overlay, Vector2i indices, float expectedTemp)
    {
        await Client.WaitPost(() =>
        {
            var chunkIndices = SharedGasTileOverlaySystem.GetGasChunkIndices(indices);

            Assert.That(overlay.Chunks.TryGetValue(chunkIndices, out var chunk), "Chunk not found");
            Assert.That(chunk, Is.Not.Null, "Chunk not found");

            // Calculate the exact index in the TileData array
            var localX = MathHelper.Mod(indices.X, SharedGasTileOverlaySystem.ChunkSize);
            var localY = MathHelper.Mod(indices.Y, SharedGasTileOverlaySystem.ChunkSize);
            var tileIndex = localX + localY * SharedGasTileOverlaySystem.ChunkSize;

            var tile = chunk.TileData[tileIndex];
            tile.ByteGasTemperature.TryGetTemperature(out var actualTemp);

            Assert.That(actualTemp, Is.EqualTo(expectedTemp).Within(0.01f), $"Tile at {indices} had wrong temperature!");
        });
    }

    private async Task InjectHotPlasma(EntityUid gridEnt, Vector2i tileIndices, GasMixture mixture, float temperature)
    {
        //Server makes atmos
        await Server.WaitPost(() =>
        {
            if (mixture != null)
            {
                mixture.Clear();
                mixture.AdjustMoles(Gas.Plasma, 100f); // Inject hot plasma
                mixture.Temperature = temperature;
                SAtmos.InvalidateVisuals(gridEnt, tileIndices);
            }
        });
        await Server.WaitRunTicks(10);
        await RunUntilSynced();
    }
}
