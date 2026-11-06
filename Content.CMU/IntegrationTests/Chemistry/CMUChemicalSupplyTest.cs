#pragma warning disable RA0002 // The test advances the real supply system's recharge deadline.
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Chemistry;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Chemistry;

[TestFixture]
public sealed class CMUChemicalSupplyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task LinkedDeckDispensersRechargeTogetherWithoutSharingOtherGrids()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var zLevels = SEntMan.System<CMUZLevelsSystem>();
            var chemistry = SEntMan.System<SharedRMCChemistrySystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            var lower = maps.CreateMap(out var lowerId, runMapInit: true);
            var upper = maps.CreateMap(runMapInit: true);
            foreach (var map in new[] { lower, upper })
            {
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                maps.SetTile(map, grid, Vector2i.Zero, floor);
            }

            var isolated = maps.CreateGridEntity(lowerId);
            maps.SetTile(isolated, Vector2i.Zero, floor);
            SEntMan.System<SharedTransformSystem>().SetWorldPosition(isolated, new Vector2(20, 0));
            var network = zLevels.CreateZNetwork();
            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [upper] = 1 }), Is.True);

            var supply = SEntMan.SpawnEntity("RMCChemStorageMedbay", new EntityCoordinates(lower, Vector2.Zero));
            var lowerDispenser = SEntMan.SpawnEntity("RMCChemDispenserMedbay", new EntityCoordinates(lower, Vector2.Zero));
            var upperDispenser = SEntMan.SpawnEntity("RMCChemDispenserMedbay", new EntityCoordinates(upper, Vector2.Zero));
            var isolatedSupply = SEntMan.SpawnEntity("RMCChemStorageMedbay", new EntityCoordinates(isolated, Vector2.Zero));
            var isolatedDispenser = SEntMan.SpawnEntity("RMCChemDispenserMedbay", new EntityCoordinates(isolated, Vector2.Zero));
            var storage = SEntMan.GetComponent<RMCChemicalStorageComponent>(supply);
            var otherStorage = SEntMan.GetComponent<RMCChemicalStorageComponent>(isolatedSupply);
            var lowerComp = SEntMan.GetComponent<RMCChemicalDispenserComponent>(lowerDispenser);
            var upperComp = SEntMan.GetComponent<RMCChemicalDispenserComponent>(upperDispenser);
            var otherComp = SEntMan.GetComponent<RMCChemicalDispenserComponent>(isolatedDispenser);

            chemistry.Update(0);
            Assert.That(chemistry.TryGetStorage((upperDispenser, upperComp), out var selected), Is.True);
            Assert.That(selected.Owner, Is.EqualTo(supply), "the upper deck must draw from its ship's storage");
            Assert.That(chemistry.TryGetStorage((isolatedDispenser, otherComp), out selected), Is.True);
            Assert.That(selected.Owner, Is.EqualTo(isolatedSupply), "a docked grid must not consume the ship's supply");

            var isolatedEnergy = otherStorage.Energy;
            chemistry.ChangeStorageEnergy((supply, storage), FixedPoint2.Zero);
            Assert.Multiple(() =>
            {
                Assert.That(lowerComp.Energy, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(upperComp.Energy, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(otherComp.Energy, Is.EqualTo(isolatedEnergy), "draining the ship must not change another grid's display");
            });

            storage.RechargeAt = SGameTiming.CurTime;
            chemistry.Update(0);
            Assert.Multiple(() =>
            {
                Assert.That(storage.Recharge, Is.EqualTo(storage.BaseRecharge + 2 * storage.RechargePer),
                    "both connected decks must contribute to recharge");
                Assert.That(lowerComp.Energy, Is.EqualTo(storage.Recharge));
                Assert.That(upperComp.Energy, Is.EqualTo(storage.Recharge), "upper-deck UI must receive periodic recharge");
                Assert.That(otherComp.Energy, Is.EqualTo(isolatedEnergy));
            });

            SEntMan.DeleteEntity(lower);
            SEntMan.DeleteEntity(upper);
            SEntMan.DeleteEntity(network);
        });
    }
}
