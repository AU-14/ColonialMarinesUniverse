using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Client._RMC14.TacticalMap;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Input;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Set up the operator and the authorized squad feed.
    [TestCase(false)]
    [TestCase(true)]
    public async Task RealOverwatchConsolePublishesDrawingThroughItsTacticalMapWindow(bool classic)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid console = default, squad = default, member = default;
        var path = new Vector2[] { new(2.25f, 3.75f), new(4.5f, 5.25f) };
        try
        {
            await Server.WaitPost(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                var areas = SEntMan.EnsureComponent<AreaGridComponent>(_upper);
                areas.Colors[new(-8, -8)] = Color.Gray;
                areas.Colors[new(8, 8)] = Color.Gray;
                console = SEntMan.SpawnEntity("RMCOverwatchConsoleGovfor", new EntityCoordinates(_upper, new Vector2(1.5f)));
                SEntMan.RemoveComponent<AccessReaderComponent>(console);
                squad = SEntMan.SpawnEntity("SquadGovforBravo", MapCoordinates.Nullspace);
                SEntMan.System<SkillsSystem>().SetSkill(_actor, "RMCSkillOverwatch", 1);
                var select = new OverwatchConsoleSelectSquadBuiMsg(SEntMan.GetNetEntity(squad))
                    { Actor = _actor, UiKey = OverwatchConsoleUI.Key };
                SEntMan.EventBus.RaiseLocalEvent(console, (object) select);
                member = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(3.5f)));
                SEntMan.System<SquadSystem>().AssignSquad(member, squad, null);
                var user = SComp<TacticalMapUserComponent>(member);
                user.Marines = false;
                user.Govfor = true;
                user.HasSquad = true;
                // Keep a recipient's map open before the overwatch update.
                SEntMan.EventBus.RaiseLocalEvent(member, new OpenTacticalMapActionEvent { Performer = member });
                var view = new CMUReconViewMessage(Vector2i.Zero) { Actor = member, UiKey = TacticalMapUserUi.Key };
                SEntMan.EventBus.RaiseLocalEvent(member, (object) view);
                Server.PlayerMan.SetAttachedEntity(session, _actor);
            });
            await Pair.RunUntilSynced();
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, classic));
            await Server.WaitPost(() =>
            {
                var open = new OverwatchViewTacticalMapBuiMsg { Actor = _actor, UiKey = OverwatchConsoleUI.Key };
                SEntMan.EventBus.RaiseLocalEvent(console, (object) open);
            });
            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() =>
            {
                if (classic)
                {
                    var wrapper = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<TacticalMapWindow>().Single().Wrapper;
                    var canvas = wrapper.Canvas;
                    Assert.That(canvas.Texture, Is.Not.Null);
                    canvas.Drawing = true;
                    canvas.Color = Color.Green;
                    canvas.Measure(new Vector2(510, 510));
                    canvas.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(510, 510)));
                    var parameters = ((Vector2 Size, Vector2 TopLeft, float Scale)) typeof(TacticalMapControl)
                        .GetMethod("GetDrawParameters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                        .Invoke(canvas, null)!;
                    Vector2 Screen(Vector2 world) => (parameters.TopLeft +
                        new Vector2(world.X + 8, 9 - world.Y) * parameters.Size / 17) / canvas.UIScale;
                    var from = Screen(path[0]);
                    var to = Screen(path[1]);
                    Dispatch(canvas, "KeyBindDown", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down,
                        default, true, from, from * canvas.UIScale));
                    Dispatch(canvas, "MouseMove", new GUIMouseMoveEventArgs(to - from, canvas, to, default, to, to * canvas.UIScale));
                    Dispatch(canvas, "KeyBindUp", new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up,
                        default, true, to, to * canvas.UIScale));
                    Click(wrapper.UpdateCanvasButton, Vector2.One);
                    return;
                }
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single();
                var scene = window.SurveyView.Scene!;
                Assert.That(scene.CanOrder, Is.True, "A trained overwatch operator must be able to publish.");
                window.OnSend!.Invoke(new CMUReconSendMessage(scene.Generation, 1,
                    [new(0, path, CMUReconInk.Green)], []));
            });
            await Pair.RunTicksSync(40);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SComp<SquadTeamComponent>(squad).TacMapLines, Has.Count.EqualTo(1));
                Assert.That(SComp<TacticalMapComponent>(_upper).GovforLines, Is.Empty);
                Assert.That(SComp<TacticalMapUserComponent>(member).SquadLines, Has.Count.EqualTo(1));
                var received = _recon.BuildSnapshot(member, member)!.Orders;
                Assert.That(received, Has.Length.EqualTo(1),
                    "An already-open squad map must receive the overwatch drawing.");
                Assert.That(Vector2.Distance(received[0].Waypoints![0], path[0]), Is.LessThan(0.01f));
                Assert.That(Vector2.Distance(received[0].Waypoints![^1], path[^1]), Is.LessThan(0.01f),
                    "Overwatch strokes must keep their position when delivered to the squad's 3D map.");
            });
        }
        finally
        {
            await Client.WaitPost(() => Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUTacMapClassic, false));
            await Server.WaitPost(() =>
            {
                if (console.IsValid()) _ui.CloseUi(console, TacticalMapComputerUi.Key, _actor);
                if (member.IsValid()) _ui.CloseUi(member, TacticalMapUserUi.Key, member);
                Server.PlayerMan.SetAttachedEntity(session, original);
                foreach (var uid in new[] { member, console, squad })
                    if (uid.IsValid() && SEntMan.EntityExists(uid)) SEntMan.DeleteEntity(uid);
            });
            await Pair.RunUntilSynced();
        }
    }
#pragma warning restore RA0002
}
