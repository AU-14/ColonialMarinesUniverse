#pragma warning disable RA0002 // Arrange a real relay offer and exercise the connected tactical-map UI.

using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Server._RMC14.TacticalMap;
using Content.Server.CMU14.Fob;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Fob;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task RelayOffersReachPersonalAndComputerMapsAcrossLinkedFloors(bool personal)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid viewer = default, first = default, second = default;
        Entity<CMUFobNetworkComponent> bank = default;
        var reserve = Server.System<CMUFobReserveSystem>();
        var source = _console;
        Enum key = Key;
        var depth = 0;
        try
        {
            await Server.WaitAssertion(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_lower);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                var tactical = Server.System<TacticalMapSystem>();
                Assert.That(tactical.TryGetTacticalMap(out var firstMap), Is.True);
                var relayMap = firstMap.Owner == _upper ? _lower : _upper;
                depth = relayMap == _upper ? 0 : -1;
                viewer = SEntMan.SpawnAtPosition("CMMobHuman", new EntityCoordinates(_upper, new Vector2(0.5f, 1.5f)));
                SEntMan.EnsureComponent<MarineComponent>(viewer).Faction = "GOVFOR";
                // This fixture bypasses role assignment, which normally synchronizes these flags.
                var user = SComp<TacticalMapUserComponent>(viewer);
                user.Marines = false;
                user.Govfor = true;
                if (personal) { source = viewer; key = TacticalMapUserUi.Key; }
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUFobReserveEnabled, true);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundStartingEvent(1));
                first = SEntMan.SpawnAtPosition("CMUFobRelay", new EntityCoordinates(relayMap, new Vector2(3.5f, 2.5f)));
                second = SEntMan.SpawnAtPosition("CMUFobRelay", new EntityCoordinates(relayMap, new Vector2(-3.5f, 2.5f)));
                Assert.That(reserve.TryCreateNetwork(first, "govfor", out bank), Is.True);
            });
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                if (personal)
                    SEntMan.EventBus.RaiseLocalEvent(viewer, new OpenTacticalMapActionEvent { Performer = viewer });
                else
                    Assert.That(_ui.TryOpenUi(source, key, viewer), Is.True);
                Assert.That(reserve.TryOfferRelays(bank, first, second, out _), Is.True);
            });
            await Pair.RunTicksSync(60);
            await Client.WaitAssertion(() =>
            {
                var view = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                    .OfType<CMUReconstructionWindow>().Single().SurveyView;
                var markers = view.TrackedContacts.Where(c => c.Blip.CMUFobLabel != null).ToArray();
                Assert.That(markers.Select(c => c.Blip.Indices), Is.EquivalentTo(new[] { new Vector2i(3, 2), new Vector2i(-4, 2) }),
                    "Both offered objectives must reach the actual client UI, even when another tactical map was registered first.");
                Assert.That(markers.All(c => c.Depth == depth), Is.True);
            });
            await Server.WaitAssertion(() =>
            {
                Assert.That(reserve.TryGetRelayJob(first, out var job, out _), Is.True);
                Assert.That(reserve.TryCompleteRelay(first, job, out _), Is.True);
            });
            await Pair.RunTicksSync(25);
            await Client.WaitAssertion(() =>
            {
                var view = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                    .OfType<CMUReconstructionWindow>().Single().SurveyView;
                Assert.That(view.TrackedContacts.Any(c => c.Blip.CMUFobLabel != null), Is.False,
                    "Completing either relay must remove both objective markers without publishing the map again.");
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (viewer.IsValid()) _ui.CloseUi(source, key, viewer);
                Server.PlayerMan.SetAttachedEntity(session, original);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
                if (viewer.IsValid() && SEntMan.EntityExists(viewer)) SEntMan.DeleteEntity(viewer);
            });
            await Pair.RunUntilSynced();
        }
    }
}
