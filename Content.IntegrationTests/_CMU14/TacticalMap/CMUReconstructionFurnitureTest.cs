using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared.Access.Components;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
    [Test]
    public async Task FurnitureVariantsReachTheClientWithTheirModelAppearanceAndFacing()
    {
        var examples = new (string Prototype, CMUReconMaterial Material)[]
        {
            ("CMChair", CMUReconMaterial.FoldingChair), ("CMChairAlt", CMUReconMaterial.Chair),
            ("CMChairWood", CMUReconMaterial.WoodChair), ("CMChairWoodWings", CMUReconMaterial.WoodWingChair),
            ("CMChairOfficeDark", CMUReconMaterial.OfficeChair), ("CMChairOfficeWhite", CMUReconMaterial.OfficeChair),
            ("CMChairComfyBlue", CMUReconMaterial.Armchair), ("CMChairComfyAlpha", CMUReconMaterial.Armchair),
            ("AU14ChairComfyAI", CMUReconMaterial.Armchair), ("RMCStool", CMUReconMaterial.Stool),
            ("RMCBenchLeft", CMUReconMaterial.BenchLeft), ("RMCBenchRight", CMUReconMaterial.BenchRight),
            ("RMCSofaRed", CMUReconMaterial.Sofa), ("RMCSofaBlack", CMUReconMaterial.Sofa),
            ("RMCCouchMidRed", CMUReconMaterial.CouchMiddle), ("RMCCouchEndLowerRed", CMUReconMaterial.CouchLeft),
            ("RMCCouchEndUpperRed", CMUReconMaterial.CouchRight), ("CMTable", CMUReconMaterial.Table),
            ("CMTableWoodenFancy", CMUReconMaterial.WoodTable), ("CMTableReinforced", CMUReconMaterial.Desk),
            ("RMCTableReinforcedRequisitionBrown", CMUReconMaterial.Counter), ("RMCBedAlt", CMUReconMaterial.Bed),
            ("RMCBedBunkBlue", CMUReconMaterial.BunkBed), ("RMCBedBunkGreenUnanchored", CMUReconMaterial.BunkBed),
            ("CMOperatingTable", CMUReconMaterial.OperatingTable), ("RMCRackBrown", CMUReconMaterial.Shelf),
            ("RMCBookcaseMedical", CMUReconMaterial.Bookcase), ("AU14WeymartRacks3", CMUReconMaterial.Shelf),
        };
        var session = ServerSession!;
        var expectedFacing = new int[examples.Length];
        var original = session.AttachedEntity;
        EntityUid map = default, table = default, actor = default;
        var movedChair = EntityUid.Invalid;
        try
        {
            await Server.WaitAssertion(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                map = _maps.CreateMap(runMapInit: true);
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++)
                    _maps.SetTile(map, grid, new Vector2i(x, y), floor);
                var transform = SEntMan.System<SharedTransformSystem>();
                for (var i = 0; i < examples.Length; i++)
                {
                    var tile = new Vector2i(1 + i % 14, 3 + i / 14);
                    var entity = SEntMan.SpawnEntity(examples[i].Prototype, new EntityCoordinates(map, (Vector2) tile + new Vector2(0.5f)));
                    transform.SetLocalRotation(entity, new Angle(i % 4 * Math.PI / 2));
                    expectedFacing[i] = SComp<TransformComponent>(entity).NoLocalRotation ? 0 : i % 4;
                    Assert.That(_recon.ReadCell(map, tile), Is.EqualTo((byte) examples[i].Material), examples[i].Prototype);
                    if (i == 4) movedChair = entity;
                }
                SEntMan.SpawnEntity("CMChairFolded", new EntityCoordinates(map, new Vector2(2.5f, 10.5f)));
                SEntMan.SpawnEntity("Pen", new EntityCoordinates(map, new Vector2(3.5f, 10.5f)));
                SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map, new Vector2(4.5f, 10.5f)));
                // A child/held chair must not enter the scenery query.
                var carrier = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map, new Vector2(5.5f, 10.5f)));
                var held = SEntMan.SpawnEntity("CMChairOfficeWhite", new EntityCoordinates(map, new Vector2(5.5f, 10.5f)));
                transform.SetParent(held, carrier);
                for (var x = 2; x <= 5; x++)
                    Assert.That(_recon.ReadCell(map, new Vector2i(x, 10)), Is.EqualTo((byte) CMUReconMaterial.Floor));
                table = SEntMan.SpawnEntity("CMUTacticalReconstructionTableGovfor", new EntityCoordinates(map, new Vector2(0.5f)));
                SEntMan.RemoveComponent<AccessReaderComponent>(table);
                actor = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map, new Vector2(0.5f, 1.5f)));
                SEntMan.System<SkillsSystem>().SetSkill(actor, "RMCSkillLeadership", 2);
                Server.PlayerMan.SetAttachedEntity(session, actor);
            });
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() => Assert.That(_ui.TryOpenUi(table, Key, actor), Is.True));
            await Pair.RunTicksSync(50);
            await Client.WaitAssertion(() =>
            {
                var scene = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView.Scene!;
                Assert.That(scene.LoadedChunks, Is.EqualTo(scene.TotalChunks));
                for (var i = 0; i < examples.Length; i++)
                {
                    var tile = new Vector2i(1 + i % 14, 3 + i / 14) - scene.Origin;
                    var at = CMUReconGeometry.Index(tile.X, tile.Y, 0, scene.Width, scene.Height);
                    Assert.That(scene.Cells[at], Is.EqualTo((byte) examples[i].Material), examples[i].Prototype);
                    Assert.That(scene.Directions[at] & 3, Is.EqualTo(expectedFacing[i]), examples[i].Prototype);
                    Assert.That(scene.Surfaces.Single(s => s.Id == scene.Appearance[at] >> 16).Prototype, Is.EqualTo(examples[i].Prototype));
                    Assert.That(scene.Appearance[at] & 0xffff, Is.Not.Zero, "Furniture must retain the independently textured floor.");
                }
            });
            await Server.WaitAssertion(() =>
            {
                SEntMan.System<SharedTransformSystem>().SetCoordinates(movedChair, new EntityCoordinates(map, new Vector2(8.5f, 10.5f)));
                Assert.That(_recon.ReadCell(map, new Vector2i(8, 10)), Is.EqualTo((byte) CMUReconMaterial.OfficeChair));
            });
            await Pair.RunTicksSync(20);
            await Client.WaitAssertion(() =>
            {
                var scene = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView.Scene!;
                var before = new Vector2i(5, 3) - scene.Origin;
                var after = new Vector2i(8, 10) - scene.Origin;
                Assert.That(scene.Cells[CMUReconGeometry.Index(before.X, before.Y, 0, scene.Width, scene.Height)], Is.EqualTo((byte) CMUReconMaterial.OfficeChair));
                Assert.That(scene.Cells[CMUReconGeometry.Index(after.X, after.Y, 0, scene.Width, scene.Height)], Is.EqualTo((byte) CMUReconMaterial.Floor),
                    "Movable furniture is captured once; moving it must not restart live geometry updates.");
            });
        }
        finally
        {
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
