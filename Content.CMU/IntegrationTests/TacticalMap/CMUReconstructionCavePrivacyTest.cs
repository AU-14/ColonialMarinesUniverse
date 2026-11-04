using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
    [Test]
    public async Task GeneratedCavesDoNotExpandOrDisableTheAutomaticColonyAtlas()
    {
        var caves = new List<EntityUid>();
        await Server.WaitAssertion(() =>
        {
            var levels = new Dictionary<EntityUid, int>();
            for (var depth = -2; depth >= -10; depth--)
            {
                var map = _maps.CreateMap(runMapInit: true);
                caves.Add(map);
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                SEntMan.EnsureComponent<ZGeneratedStoneComponent>(map).StoneGrid = map;
                _maps.SetTile(map, grid, Vector2i.Zero, new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId));
                levels.Add(map, depth);
            }
            var network = (_network, SEntMan.GetComponent<CMUZLevelsNetworkComponent>(_network));
            Assert.That(Server.System<CMUZLevelsSystem>().TryAddMapsIntoZNetwork(network, levels), Is.True);
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            Send(new CMUReconViewMessage(Vector2i.Zero) { RequestId = 20 });
            var snapshot = _recon.BuildSnapshot(_console, _actor)!;
            Assert.That(snapshot.MinDepth, Is.EqualTo(-1), "No generated cave slice belongs in the automatic colony atlas.");
            Assert.That(snapshot.Levels, Is.EqualTo(2), "Adding a deep cavern network must not break the original authored-floor view.");
        });
        foreach (var cave in caves) await Pair.DeleteEntityTreeLeafFirst(cave);
    }
}
