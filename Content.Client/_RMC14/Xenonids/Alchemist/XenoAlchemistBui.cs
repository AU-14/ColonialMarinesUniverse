using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._RMC14.Xenonids.Alchemist;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Xenonids.Alchemist;

[UsedImplicitly]
public sealed partial class XenoAlchemistBui : BoundUserInterface
{
    [Dependency] private IClyde _displayManager = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;

    [ViewVariables]
    private XenoAlchemistMenu? _menu;

    public XenoAlchemistBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);

        _sprite = EntMan.System<SpriteSystem>();
        _transform = EntMan.System<TransformSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<XenoAlchemistMenu>();
        var parent = _menu.FindControl<RadialContainer>("Main");
        EntMan.TryGetComponent<XenoAlchemistComponent>(Owner, out var stockpile);

        AddChemicalButton(AlchemistChemical.Sagunine, parent, stockpile);
        AddChemicalButton(AlchemistChemical.Cholinine, parent, stockpile);
        AddChemicalButton(AlchemistChemical.Noctine, parent, stockpile);
        AddChemicalButton(AlchemistChemical.None, parent, stockpile);

        var vpSize = _displayManager.ScreenSize;
        var pos = _inputManager.MouseScreenPosition.Position / vpSize;

        if (EntMan.TryGetComponent<EyeComponent>(Owner, out var eyeComp) &&
            eyeComp.Target != null)
        {
            pos = _eye.WorldToScreen(_transform.GetMapCoordinates((EntityUid) eyeComp.Target).Position) / vpSize;
        }
        else if (_player.LocalEntity is { } ent)
        {
            pos = _eye.WorldToScreen(_transform.GetMapCoordinates(ent).Position) / vpSize;
        }

        _menu.OpenCenteredAt(pos);
    }

    private void AddChemicalButton(AlchemistChemical chemical, RadialContainer parent, XenoAlchemistComponent? stockpile)
    {
        var name = chemical.ToString().ToLowerInvariant();
        var chemicalName = Loc.GetString($"cm-xeno-alchemist-chemical-{name}");
        var amount = GetChemical(stockpile, chemical);
        var total = GetTotalStockpile(stockpile);
        var max = stockpile?.MaxStockpile ?? 0;

        var texture = new TextureRect
        {
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalAlignment = Control.HAlignment.Center,
            Texture = _sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Actions/xeno_actions.rsi"), GetIconState(chemical))),
            TextureScale = new Vector2(1.55f, 1.55f),
        };

        var contents = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalAlignment = Control.HAlignment.Center,
            SeparationOverride = 0,
        };
        contents.AddChild(texture);

        if (chemical != AlchemistChemical.None)
        {
            contents.AddChild(new Label
            {
                Text = amount.ToString(),
                Align = Label.AlignMode.Center,
            });
        }

        var button = new RadialMenuTextureButton
        {
            StyleClasses = { "RadialMenuButton" },
            SetSize = new Vector2(72, 72),
            ToolTip = chemical == AlchemistChemical.None
                ? Loc.GetString("cm-xeno-alchemist-chemical-stockpile-total",
                    ("chemical", chemicalName),
                    ("total", total),
                    ("max", max))
                : Loc.GetString("cm-xeno-alchemist-chemical-stockpile",
                    ("chemical", chemicalName),
                    ("amount", amount),
                    ("total", total),
                    ("max", max)),
        };

        button.OnButtonDown += _ => SendPredictedMessage(new XenoAlchemistChooseBuiMsg(chemical));

        button.AddChild(contents);
        parent.AddChild(button);
    }

    private static int GetChemical(XenoAlchemistComponent? comp, AlchemistChemical chemical)
    {
        if (comp == null)
            return 0;

        return chemical switch
        {
            AlchemistChemical.Sagunine => comp.Sagunine,
            AlchemistChemical.Cholinine => comp.Cholinine,
            AlchemistChemical.Noctine => comp.Noctine,
            _ => 0,
        };
    }

    private static int GetTotalStockpile(XenoAlchemistComponent? comp)
    {
        return comp == null ? 0 : comp.Sagunine + comp.Cholinine + comp.Noctine;
    }

    private static string GetIconState(AlchemistChemical chemical)
    {
        return chemical switch
        {
            AlchemistChemical.Sagunine => "heal_xeno",
            AlchemistChemical.Cholinine => "shift_spit_xeno_acid",
            AlchemistChemical.Noctine => "shift_spit_neurotoxin",
            _ => "dump_acid",
        };
    }
}
