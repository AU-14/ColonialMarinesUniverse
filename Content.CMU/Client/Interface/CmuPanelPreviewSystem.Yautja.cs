using System.Numerics;
using Content.Client.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Interface;

// the bracer's tool windows only open off a worn bracer mid-round, so these open them with made-up states to look
// at. nothing is wired: pressing anything raises an event nobody listens to
public sealed partial class CmuPanelPreviewSystem
{
    private static void OpenYautjaPreview(string name, ref int opened)
    {
        BaseWindow? window = name switch
        {
            "yautjamarks" => CreateMarkPreview(),
            "yautjatranslator" => CreateTranslatorPreview(),
            "yautjathrall" => new YautjaThrallMessageWindow(),
            "yautjarelay" => CreateRelayPreview(),
            "yautjaaudio" => CreateAudioPreview(),
            _ => null,
        };

        if (window == null)
            return;

        window.OpenCenteredAt(new Vector2(0.42f + opened * 0.06f, 0.34f + opened * 0.13f));
        window.MoveToFront();
        opened++;
    }

    private static BaseWindow CreateMarkPreview()
    {
        var window = new YautjaMarkWindow();
        window.UpdateState(new YautjaMarkPanelState(new List<YautjaMarkPanelEntry>
        {
            new YautjaMarkPanelEntry(1, "Cpl. Dwayne Hicks", false,
                new List<YautjaMarkKind> { YautjaMarkKind.Prey, YautjaMarkKind.GearCarrier }, new List<YautjaMarkKind> { YautjaMarkKind.Prey }, true),
            new YautjaMarkPanelEntry(2, "Drone (XX-121)", true, new List<YautjaMarkKind> { YautjaMarkKind.Dishonored }, new List<YautjaMarkKind>(), true),
            new YautjaMarkPanelEntry(3, "Pvt. Ricco Frost", false, new List<YautjaMarkKind>(), new List<YautjaMarkKind>(), true),
            new YautjaMarkPanelEntry(4, "Colonist Amy Jorden", false, new List<YautjaMarkKind> { YautjaMarkKind.Honored }, new List<YautjaMarkKind>(), true),
            new YautjaMarkPanelEntry(5, "Sgt. Al Apone", false,
                new List<YautjaMarkKind> { YautjaMarkKind.Honored, YautjaMarkKind.Dishonored, YautjaMarkKind.GearCarrier, YautjaMarkKind.Blooded },
                new List<YautjaMarkKind>(), true),
            new YautjaMarkPanelEntry(6, "Runner (XX-047)", true, new List<YautjaMarkKind> { YautjaMarkKind.Prey }, new List<YautjaMarkKind>(), false),
        }, 1, false, 0, 1));
        window.Select(1);
        return window;
    }

    private static BaseWindow CreateTranslatorPreview()
    {
        var window = new YautjaTranslatorWindow();
        window.UpdateState(new YautjaTranslatorBuiState(620, 800, 50, 160));
        return window;
    }

    private static BaseWindow CreateRelayPreview()
    {
        var window = new YautjaRelayBeaconWindow();
        window.UpdateState(new YautjaRelayBeaconState(new List<YautjaRelayBeaconDestinationEntry>
        {
            new YautjaRelayBeaconDestinationEntry(YautjaRelayDestinationKind.YautjaShip, "Yautja Ship", true),
            new YautjaRelayBeaconDestinationEntry(YautjaRelayDestinationKind.HumanShip, "Human Ship", false),
            new YautjaRelayBeaconDestinationEntry(YautjaRelayDestinationKind.Ground, "North ridge lodge", true, 0),
        }));
        return window;
    }

    private static BaseWindow CreateAudioPreview()
    {
        var window = new YautjaAudioPanelWindow();
        window.UpdateState(new YautjaAudioPanelState(new List<YautjaAudioPanelEntry>
        {
            new YautjaAudioPanelEntry("click", "Click", "Yautja"),
            new YautjaAudioPanelEntry("roar", "Roar", "Yautja"),
            new YautjaAudioPanelEntry("laugh", "Laugh", "Yautja"),
            new YautjaAudioPanelEntry("growl", "Growl", "Yautja"),
            new YautjaAudioPanelEntry("iseeyou", "I see you.", "Voice Synthesizer"),
            new YautjaAudioPanelEntry("pain", "Pain", "Pain and death"),
        }, System.TimeSpan.FromSeconds(4)));
        return window;
    }
}
