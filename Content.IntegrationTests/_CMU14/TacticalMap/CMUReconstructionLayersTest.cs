using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Model independently published platoon, squad and faction overlays.
    [TestCase(false)]
    [TestCase(true)]
    public async Task PersonalLayerSelectorFiltersDrawingsAndIconsWithoutReloadingTerrain(bool ghost)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid viewer = default, squad = default, squadContact = default;
        NetEntity netViewer = default;
        CMUReconstructionWindow window = null!;
        CMUReconHandshakeTestSystem probe = null!;
        byte[] cells = null!;
        CMUReconCamera camera = default;
        int generation = 0, atlasId = 0;
        var platoonBlip = new TacticalMapBlip { Indices = new(2, 3), Color = Color.Blue };
        var squadBlip = platoonBlip with { Indices = new(3, 4), Color = Color.Green };
        var enemyBlip = platoonBlip with { Color = Color.Red };
        var xenoBlip = platoonBlip with { Color = Color.Purple };
        try
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                var map = SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                map.GovforLines = [new(default, default, Color.Blue, 3, [new(2, 3)])];
                map.GovforLabels = new() { [new(2, 3)] = "Platoon plan" };
                map.OpforLines = [new(default, default, Color.Red, 3, [new(4, 5)])];
                map.OpforLabels = new() { [new(4, 5)] = "OPFOR plan" };
                map.XenoLines = [new(default, default, Color.Purple, 3, [new(5, 6)])];
                map.XenoLabels = new() { [new(5, 6)] = "Hive plan" };
                map.NextUpdate = TimeSpan.MaxValue;
                foreach (var faction in map.NextUpdatePerFaction.Keys.ToArray()) map.NextUpdatePerFaction[faction] = TimeSpan.MaxValue;
                viewer = SEntMan.SpawnEntity(ghost ? "MobObserver" : "CMMobHuman", new EntityCoordinates(_upper, new Vector2(1.5f)));
                if (!ghost)
                {
                    SEntMan.EnsureComponent<MarineComponent>(viewer).Faction = "GOVFOR";
                    squad = SEntMan.SpawnEntity("SquadGovforBravo", MapCoordinates.Nullspace);
                    var team = SComp<SquadTeamComponent>(squad);
                    team.TacMapLines = [new(default, default, Color.Green, 3, [new(3, 4)])];
                    team.TacMapLabels = new() { [new(3, 4)] = "Squad plan" };
                    SEntMan.System<SquadSystem>().AssignSquad(viewer, squad, null);
                    squadContact = SEntMan.SpawnEntity(null, new EntityCoordinates(_upper, new Vector2(3.5f)));
                }
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                netViewer = SEntMan.GetNetEntity(viewer);
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() =>
            {
                var user = SComp<TacticalMapUserComponent>(viewer);
                if (!ghost)
                {
                    user.Marines = false;
                    user.Govfor = true;
                    user.HasSquad = true;
                }
                SEntMan.EventBus.RaiseLocalEvent(viewer, new OpenTacticalMapActionEvent { Performer = viewer });
            });
            await Pair.RunTicksSync(50);
            await Server.WaitPost(() =>
            {
                var user = SComp<TacticalMapUserComponent>(viewer);
                user.GovforBlips = new() { [_actor.Id] = platoonBlip };
                user.OpforBlips = new() { [_console.Id] = enemyBlip };
                user.XenoBlips = new() { [viewer.Id] = xenoBlip };
                if (!ghost) user.SquadBlips = new() { [squadContact.Id] = squadBlip };
            });
            await Pair.RunTicksSync(20);
            await Client.WaitAssertion(() =>
            {
                window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                var view = window.SurveyView;
                Assert.That(view.Scene, Is.Not.Null);
                Assert.That(view.Scene!.LoadedChunks, Is.EqualTo(view.Scene.TotalChunks));
                Assert.That(window.FindControl<Control>("LayerSelector").VisibleInTree, Is.True);
                Assert.That(window.FindControl<Control>("DrawingToolbar").VisibleInTree, Is.False);
                var choices = window.FindControl<OptionButton>("LayerSelection");
                var expected = ghost
                    ? new[] { CMUReconLayer.Combined, CMUReconLayer.Marines, CMUReconLayer.Govfor, CMUReconLayer.Opfor,
                        CMUReconLayer.Xenos, CMUReconLayer.Clf, CMUReconLayer.WeYu, CMUReconLayer.Abomination, CMUReconLayer.Yautja }
                    : new[] { CMUReconLayer.Combined, CMUReconLayer.Platoon, CMUReconLayer.Squad };
                Assert.That(Enumerable.Range(0, choices.ItemCount).Select(i => (CMUReconLayer) choices.GetItemId(i)), Is.EquivalentTo(expected));
                generation = view.Scene.Generation;
                atlasId = view.Scene.AtlasId;
                cells = view.Scene.Cells;
                view.SetTopDown();
                view.Pan(new Vector2(2, -1));
                camera = view.CaptureCamera();
                probe = CEntMan.System<CMUReconHandshakeTestSystem>();
                probe.Target = CEntMan.GetEntity(netViewer);
                probe.Chunks = 0;
            });

            var cases = ghost
                ? new[] { (CMUReconLayer.Govfor, new[] { Color.Blue }, new[] { "Platoon plan" }),
                    (CMUReconLayer.Opfor, new[] { Color.Red }, new[] { "OPFOR plan" }),
                    (CMUReconLayer.Xenos, new[] { Color.Purple }, new[] { "Hive plan" }),
                    (CMUReconLayer.Marines, Array.Empty<Color>(), Array.Empty<string>()),
                    (CMUReconLayer.Combined, new[] { Color.Blue, Color.Red, Color.Purple }, new[] { "Platoon plan", "OPFOR plan", "Hive plan" }) }
                : new[] { (CMUReconLayer.Platoon, new[] { Color.Blue }, new[] { "Platoon plan" }),
                    (CMUReconLayer.Squad, new[] { Color.Green }, new[] { "Squad plan" }),
                    (CMUReconLayer.Combined, new[] { Color.Blue, Color.Green }, new[] { "Platoon plan", "Squad plan" }) };
            foreach (var (layer, colors, labels) in cases)
            {
                await SelectMapLayer(window, layer);
                await Pair.RunTicksSync(20);
                await Client.WaitAssertion(() =>
                {
                    var view = window.SurveyView;
                    Assert.That(view.Scene!.Layer, Is.EqualTo(layer));
                    Assert.That(view.Orders.Where(o => o.Text == null).Select(o => o.Color!.Value), Is.EquivalentTo(colors));
                    Assert.That(view.Orders.Where(o => o.Text != null).Select(o => o.Text), Is.EquivalentTo(labels));
                    Assert.That(view.TrackedContacts.Select(c => c.Blip.Color), Is.EquivalentTo(colors));
                    Assert.That(view.Scene.Cells, Is.SameAs(cells), "Switching overlays must retain the loaded geometry.");
                    Assert.That((view.Scene.Generation, view.Scene.AtlasId), Is.EqualTo((generation, atlasId)));
                    Assert.That(view.CaptureCamera(), Is.EqualTo(camera));
                    Assert.That(probe.Chunks, Is.Zero);
                    Assert.That(window.FindControl<Control>("DrawingToolbar").VisibleInTree, Is.False);
                });
            }

            // The personal action remains read-only; forged or stale selections cannot expand access.
            await Server.WaitAssertion(() =>
            {
                var before = _recon.BuildSnapshot(viewer, viewer)!;
                void Request(BoundUserInterfaceMessage message)
                {
                    message.Actor = viewer; message.UiKey = TacticalMapUserUi.Key;
                    SEntMan.EventBus.RaiseLocalEvent(viewer, (object) message);
                }
                Request(new CMUReconLayerMessage(generation, ghost ? CMUReconLayer.Squad : CMUReconLayer.Opfor));
                Request(new CMUReconLayerMessage(generation, CMUReconLayer.Combined | CMUReconLayer.Opfor));
                Request(new CMUReconLayerMessage(generation - 1, ghost ? CMUReconLayer.Xenos : CMUReconLayer.Squad));
                Request(new CMUReconSendMessage(generation, 123, [new(0, [new(1, 1)], CMUReconInk.Red)], []));
                var after = _recon.BuildSnapshot(viewer, viewer)!;
                Assert.That(after.Layer, Is.EqualTo(before.Layer));
                Assert.That(after.Orders, Is.EqualTo(before.Orders));
                Assert.That(after.CanOrder, Is.False);
            });

            var retained = ghost ? CMUReconLayer.Opfor : CMUReconLayer.Squad;
            await SelectMapLayer(window, retained);
            await Pair.RunTicksSync(20);
            await Server.WaitPost(() => _ui.CloseUi(viewer, TacticalMapUserUi.Key, viewer));
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(viewer, new OpenTacticalMapActionEvent { Performer = viewer }));
            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() =>
            {
                window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                Assert.That(window.SurveyView.Scene!.Layer, Is.EqualTo(retained), "A cached reopen should restore the selected layer.");
                Assert.That(window.SurveyView.Scene.AtlasId, Is.EqualTo(atlasId));
            });
            if (!ghost)
            {
                await Server.WaitPost(() => SEntMan.System<SquadSystem>().RemoveSquad(viewer, null));
                await Pair.RunTicksSync(20);
                await Client.WaitAssertion(() =>
                {
                    Assert.That(window.SurveyView.Scene!.Layer, Is.EqualTo(CMUReconLayer.Combined));
                    Assert.That(window.SurveyView.Orders.Where(o => o.Text == null).Select(o => o.Color), Is.EquivalentTo(new[] { (Color?) Color.Blue }));
                    Assert.That(window.FindControl<Control>("LayerSelector").VisibleInTree, Is.False);
                });
            }
        }
        finally
        {
            await Client.WaitPost(() => { if (probe != null) probe.Target = default; });
            await Server.WaitPost(() =>
            {
                if (viewer.IsValid()) _ui.CloseUi(viewer, TacticalMapUserUi.Key, viewer);
                Server.PlayerMan.SetAttachedEntity(session, original);
                foreach (var uid in new[] { viewer, squad, squadContact })
                    if (uid.IsValid() && SEntMan.EntityExists(uid)) SEntMan.DeleteEntity(uid);
            });
            await Pair.RunUntilSynced();
        }
    }
#pragma warning restore RA0002

    private async Task SelectMapLayer(CMUReconstructionWindow window, CMUReconLayer layer)
    {
        await Client.WaitAssertion(() =>
        {
            var selector = window.FindControl<OptionButton>("LayerSelection");
            Assert.That(selector.Disabled, Is.False);
            Click(selector, Vector2.One);
        });
        await Pair.RunTicksSync(1);
        await Client.WaitAssertion(() =>
        {
            var selector = window.FindControl<OptionButton>("LayerSelection");
            var buttons = LayerOptionButtons(selector.OptionsScroll).ToArray();
            Click(buttons[selector.GetIdx((int) layer)], Vector2.One);
        });
    }

    private static IEnumerable<Button> LayerOptionButtons(Control root)
    {
        foreach (var child in root.Children)
        {
            if (child is Button { ToggleMode: true } button) yield return button;
            foreach (var option in LayerOptionButtons(child)) yield return option;
        }
    }
}
