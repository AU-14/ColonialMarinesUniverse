using Content.Client.CMU14.MagicMirror; // cmu edit
using Content.Client.Humanoid;
using Content.Shared.Humanoid; // cmu edit
using Content.Shared.Preferences; // cmu edit
using Content.Shared.MagicMirror;
using Robust.Client.UserInterface;

namespace Content.Client.MagicMirror;

public sealed class MagicMirrorBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MagicMirrorWindow? _window;

    private readonly MarkingsViewModel _markingsModel = new();

    // cmu edit start: regulation scissors get the regulation haircut menu
    private CMURegulationMirrorWindow? _cmuRegulationWindow;
    // cmu edit end

    public MagicMirrorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        // cmu edit start: regulation scissors get the regulation haircut menu
        if (EntMan.HasComponent<CMURegulationMirrorComponent>(Owner))
        {
            _cmuRegulationWindow = this.CreateWindow<CMURegulationMirrorWindow>();
            _cmuRegulationWindow.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
            _cmuRegulationWindow.OnCut += markings => SendPredictedMessage(new MagicMirrorSelectMessage(markings));
            return;
        }
        // cmu edit end

        _window = this.CreateWindow<MagicMirrorWindow>();

        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        _window.MarkingsPicker.SetModel(_markingsModel);

        _markingsModel.MarkingsChanged += (_, _) =>
        {
            SendPredictedMessage(new MagicMirrorSelectMessage(_markingsModel.Markings));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MagicMirrorUiState data)
            return;

        // cmu edit start: regulation scissors get the regulation haircut menu
        if (_cmuRegulationWindow != null)
        {
            var species = HumanoidCharacterProfile.DefaultSpecies;
            var sex = Sex.Male;
            if (EntMan.TryGetComponent(Owner, out MagicMirrorComponent? mirror) &&
                EntMan.TryGetComponent(mirror.Target, out HumanoidProfileComponent? humanoid))
            {
                species = humanoid.Species;
                sex = humanoid.Sex;
            }

            _cmuRegulationWindow.Update(data.OrganMarkingData, data.AppliedMarkings, species, sex);
            return;
        }
        // cmu edit end

        _markingsModel.OrganData = data.OrganMarkingData;
        _markingsModel.OrganProfileData = data.OrganProfileData;
        _markingsModel.Markings = data.AppliedMarkings;
    }
}

