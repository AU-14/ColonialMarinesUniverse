using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared.Access.Components;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
    [Test]
    public async Task SparseColdOpenSkipsEmptyPacketsAndIncludesUnanchoredForestScenery()
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid map = default, table = default, actor = default;
        NetEntity tableNet = default;
        CMUReconHandshakeTestSystem probe = null;
        var scenery = new[] { "RMCFloraTree01", "RMCFloraTreeLarge01", "CMUFloraTreeLarge", "RMCFloraTreeConifer01", "RMCBushJungle1", "RMCRockColourable1" };
        try
        {
            await Server.WaitAssertion(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                map = _maps.CreateMap(runMapInit: true);
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                SEntMan.EnsureComponent<TacticalMapComponent>(map);
                var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                _maps.SetTile(map, grid, Vector2i.Zero, floor);
                _maps.SetTile(map, grid, new Vector2i(511, 511), floor);
                for (var i = 0; i < scenery.Length; i++)
                {
                    var entity = SEntMan.SpawnEntity(scenery[i], new EntityCoordinates(map, new Vector2(i + 2.5f, 3.5f)));
                    Assert.That(SComp<TransformComponent>(entity).Anchored, Is.False, "Exercise the static-but-unanchored scenery that the old extraction missed.");
                }
                table = SEntMan.SpawnEntity("CMUTacticalReconstructionTableGovfor", new EntityCoordinates(map, new Vector2(0.5f)));
                SEntMan.RemoveComponent<AccessReaderComponent>(table);
                actor = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map, new Vector2(0.5f, 1.5f)));
                SEntMan.System<SkillsSystem>().SetSkill(actor, "RMCSkillLeadership", 2);
                tableNet = SEntMan.GetNetEntity(table);
                Server.PlayerMan.SetAttachedEntity(session, actor);
            });
            await Pair.RunUntilSynced();
            await Client.WaitPost(() =>
            {
                probe = CEntMan.System<CMUReconHandshakeTestSystem>();
                probe.Target = CEntMan.GetEntity(tableNet);
                probe.Chunks = probe.ChunkBytes = 0;
            });
            await Server.WaitAssertion(() => Assert.That(_ui.TryOpenUi(table, Key, actor), Is.True));
            await Pair.RunTicksSync(50);
            await Client.WaitAssertion(() =>
            {
                var scene = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView.Scene!;
                Assert.That(scene, Is.Not.Null);
                Assert.That((scene.Width, scene.Height), Is.EqualTo((512, 512)));
                Assert.That(scene.TotalChunks, Is.EqualTo(1024));
                Assert.That(scene.LoadedChunks, Is.EqualTo(scene.TotalChunks));
                Assert.That(scene.Revisions, Has.All.GreaterThan(0));
                Assert.That(probe.Chunks, Is.EqualTo(2), "The 1,022 absent chunks must not occupy individual transport slots.");
                Assert.That(probe.Snapshot.EmptyChunks.Length, Is.EqualTo(128));
                for (var i = 0; i < scenery.Length; i++)
                {
                    var at = CMUReconGeometry.Index(i + 2, 3, 0, scene.Width, scene.Height);
                    Assert.That(scene.Cells[at], Is.Not.Zero, $"Missing static scenery: {scenery[i]}");
                    var surface = scene.Surfaces.Single(s => s.Id == scene.Appearance[at] >> 16);
                    Assert.That(surface.Prototype, Is.EqualTo(scenery[i]));
                }
                Assert.That(scene.Cells[CMUReconGeometry.Index(511, 511, 0, 512, 512)], Is.EqualTo((byte) CMUReconMaterial.Floor));
                TestContext.Out.WriteLine($"512x512 cold fixture: {probe.Chunks}/1024 chunk packets; empty mask {probe.Snapshot.EmptyChunks.Length} bytes; geometry {probe.ChunkBytes} estimated bytes.");
            });
        }
        finally
        {
            await Client.WaitPost(() => { if (probe != null) probe.Target = default; });
            await Server.WaitPost(() =>
            {
                if (table.IsValid()) _ui.CloseUi(table, Key, actor);
                Server.PlayerMan.SetAttachedEntity(session, original);
            });
            await Pair.RunUntilSynced();
            if (map.IsValid()) await Pair.DeleteEntityTreeLeafFirst(map);
        }
    }
}
