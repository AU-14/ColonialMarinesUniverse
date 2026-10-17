using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ReopeningRemembersTheViewEvenWithoutCachedTerrain(bool warm)
    {
        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<CMUReconstructionCacheSystem>();
            CMUReconSnapshotMessage Scene(int request) => new(request, new(-16, -16), -1, 2, [], [], false, 32, 32)
            {
                AtlasId = 900000 + (warm ? 1 : 0), RequestId = request,
                MapChoice = CMUReconMapChoice.Planet, HasPlanet = true,
                OperatorPosition = new(5, 6), OperatorDepth = 0,
            };
            using var first = new CMUReconstructionWindow
            {
                CenterOnOpening = true, LoadView = cache.GetView, SaveView = cache.SaveView,
            };
            first.OpenCentered();
            first.BeginViewRequest(1);
            first.Receive(Scene(1));
            Assert.That(first.SurveyView.ShowNames, Is.False, "Names remain off on first use.");
            first.SurveyView.SetTopDown();
            first.SurveyView.SelectLevel(0);
            first.SurveyView.Pan(new Vector2(2, 3));
            Click(first.FindControl<CheckBox>("MarineNames"), Vector2.One);
            Click(first.FindControl<CheckBox>("Contacts"), Vector2.One);
            var camera = first.SurveyView.CaptureCamera();
            first.RememberView();
            var scene = first.SurveyView.Scene!;
            first.Close();

            using var reopened = new CMUReconstructionWindow
            {
                CenterOnOpening = true, LoadView = cache.GetView, SaveView = cache.SaveView,
            };
            reopened.OpenCentered();
            if (warm) reopened.RestoreCached(scene, camera);
            reopened.BeginViewRequest(2, keepScene: warm);
            reopened.Receive(Scene(2));
            if (warm)
                reopened.Receive(new CMUReconPatchMessage(2, [], [], false));
            Assert.That(reopened.SurveyView.CaptureCamera(), Is.EqualTo(camera));
            Assert.That(reopened.FindControl<CheckBox>("MarineNames").Pressed, Is.True);
            Assert.That(reopened.FindControl<CheckBox>("Contacts").Pressed, Is.False);
            Assert.That(reopened.SurveyView.SelectedLevel, Is.Zero, "The selected floor must override automatic centering.");
            reopened.Close();
        });
    }
}
