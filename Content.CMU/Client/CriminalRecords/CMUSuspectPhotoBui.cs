using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.CriminalRecords;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.CriminalRecords;

/// <summary>Shows a suspect photo printed from a records console: the mugshot, with their name and description.</summary>
public sealed class CMUSuspectPhotoBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent(Owner, out CMUSuspectPhotoComponent? photo))
            return;

        var kit = new CrtConsoleKit();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("cmu-records-photo-name", ("name", photo.Name));
        _window.MinSize = new Vector2(260, 320);
        _window.SetSize = new Vector2(280, 360);

        var portrait = new CMURecordPortraitView(new Vector2(200, 200), kit.Small)
        {
            HorizontalAlignment = Control.HAlignment.Center,
        };
        portrait.SetPortrait(photo.Portrait);
        kit.Root.AddChild(portrait);
        kit.Root.AddChild(CrtConsoleKit.BoldWrapping(photo.Name.ToUpperInvariant(), CrtTerminalPalette.TextBright));
        kit.Root.AddChild(CrtConsoleKit.Wrapping(photo.Description, CrtTerminalPalette.Text));
        _window.Contents.AddChild(kit.Tube);
    }
}
