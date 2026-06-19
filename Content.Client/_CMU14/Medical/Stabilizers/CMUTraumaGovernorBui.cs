using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._CMU14.Medical.Stabilizers;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._CMU14.Medical.Stabilizers;

[UsedImplicitly]
public sealed partial class CMUTraumaGovernorBui : BoundUserInterface
{
    [Dependency] private IClyde _displayManager = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;

    private readonly TransformSystem _transform;

    [ViewVariables]
    private CMUTraumaGovernorMenu? _menu;

    public CMUTraumaGovernorBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _transform = EntMan.System<TransformSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<CMUTraumaGovernorMenu>();
        var parent = _menu.FindControl<RadialContainer>("Main");

        AddOrganButton(CMUOrganStabilizerTarget.Heart, parent);
        AddOrganButton(CMUOrganStabilizerTarget.Lungs, parent);
        AddOrganButton(CMUOrganStabilizerTarget.Brain, parent);
        AddOrganButton(CMUOrganStabilizerTarget.Liver, parent);
        AddOrganButton(CMUOrganStabilizerTarget.Kidneys, parent);
        AddOrganButton(CMUOrganStabilizerTarget.Stomach, parent);
        AddOrganButton(CMUOrganStabilizerTarget.Eyes, parent);
        AddOrganButton(CMUOrganStabilizerTarget.Ears, parent);

        var vpSize = _displayManager.ScreenSize;
        var pos = _inputManager.MouseScreenPosition.Position / vpSize;
        if (_player.LocalEntity is { } local)
            pos = _eye.WorldToScreen(_transform.GetMapCoordinates(local).Position) / vpSize;

        _menu.OpenCenteredAt(pos);
    }

    private void AddOrganButton(CMUOrganStabilizerTarget target, RadialContainer parent)
    {
        var label = new Label
        {
            Text = Abbreviation(target),
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
            FontColorOverride = Color.White,
        };

        var button = new RadialMenuTextureButton
        {
            StyleClasses = { "RadialMenuButton" },
            SetSize = new Vector2(64, 64),
            ToolTip = Loc.GetString(SharedCMUTraumaGovernorSystem.GetTargetLocaleKey(target)),
        };

        button.OnButtonDown += _ => SendPredictedMessage(new CMUTraumaGovernorChooseOrganBuiMsg(target));
        button.AddChild(label);
        parent.AddChild(button);
    }

    private static string Abbreviation(CMUOrganStabilizerTarget target)
    {
        return target switch
        {
            CMUOrganStabilizerTarget.Heart => "HRT",
            CMUOrganStabilizerTarget.Lungs => "LNG",
            CMUOrganStabilizerTarget.Brain => "BRN",
            CMUOrganStabilizerTarget.Liver => "LVR",
            CMUOrganStabilizerTarget.Kidneys => "KID",
            CMUOrganStabilizerTarget.Stomach => "STM",
            CMUOrganStabilizerTarget.Eyes => "EYE",
            CMUOrganStabilizerTarget.Ears => "EAR",
            _ => "?",
        };
    }
}

