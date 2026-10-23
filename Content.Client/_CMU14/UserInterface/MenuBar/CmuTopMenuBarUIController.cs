using System.Linq;
using System.Numerics;
using Content.Client._CMU14.Interface;
using Content.Client._RMC14.Language.Systems;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._CMU14.UserInterface.MenuBar;

/// <summary>
///     Turns the top menu bar into grouped, captioned keys: your own tools, then help, then staff
///     tools, with the escape menu pushed right. Keys move into the tooltips.
/// </summary>
public sealed partial class CmuTopMenuBarUIController : UIController
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _resources = default!;

    private const float KeyMinWidth = 46;
    private const float IconScale = 0.3f;
    private const float GroupGap = 8;
    private const string CaptionName = "CmuCaption";
    private const string LanguageCodeName = "CmuLanguageCode";
    private const string PlateName = "CmuHousingPlate";

    private static readonly string[] KeyStyles =
    {
        StyleNano.StyleClassCmuMenuKeyOutlined, StyleNano.StyleClassCmuMenuKeyRaised, StyleNano.StyleClassCmuMenuKeyCap,
    };

    private readonly Dictionary<MenuButton, string> _baseTooltips = new();
    private LanguageSystem? _languages;
    private Label? _languageCode;
    private bool _pending;
    private static CmuHousingTone? _housing;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;

        _cfg.OnValueChanged(CCVars.CMUMenuBarStyle, _ => _pending = true);
        _cfg.OnValueChanged(CCVars.CrtUiEnabled, _ => _pending = true);
        _cfg.OnValueChanged(CCVars.CMUChatHousing, _ => _pending = true);
    }

    // Deferred a frame: RMC inserts the language button on the same event, in no guaranteed order.
    private void OnScreenLoad()
    {
        _pending = true;
    }

    private void OnScreenUnload()
    {
        if (_languages != null)
            _languages.OnLanguagesChanged -= UpdateLanguageCode;

        _languages = null;
        _languageCode = null;
        _baseTooltips.Clear();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        if (!_pending)
            return;

        _pending = false;
        if (UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>() is { } bar)
            Apply(bar);
    }

    private void Apply(GameTopMenuBar bar)
    {
        var named = new[]
        {
            bar.EscapeButton, bar.GuidebookButton, bar.CharacterButton, bar.EmotesButton, bar.CraftingButton,
            bar.ActionButton, bar.AdminButton, bar.SandboxButton, bar.AHelpButton,
        };
        var language = bar.Children.OfType<MenuButton>().FirstOrDefault(b => !named.Contains(b));

        var order = new List<Control?>
        {
            bar.CharacterButton, language, bar.EmotesButton, bar.CraftingButton, bar.ActionButton,
            Spacer(bar, "CmuGapHelp", false),
            bar.GuidebookButton, bar.AHelpButton,
            Spacer(bar, "CmuGapStaff", false),
            bar.AdminButton, bar.SandboxButton,
            Spacer(bar, "CmuGapGrow", true),
            bar.EscapeButton,
        };

        var index = 0;
        foreach (var control in order)
        {
            control?.SetPositionInParent(index++);
        }

        bar.SeparationOverride = 4;

        _housing = CmuHousingPalette.TryGet(_cfg.GetCVar(CCVars.CMUChatHousing), out var tone) ? tone : null;
        var keyStyle = _housing != null
            ? StyleNano.StyleClassCmuMenuKeyCap
            : _cfg.GetCVar(CCVars.CMUMenuBarStyle) == CCVars.CMUMenuBarStyleRaised
                ? StyleNano.StyleClassCmuMenuKeyRaised
                : StyleNano.StyleClassCmuMenuKeyOutlined;

        Style(bar.CharacterButton, "char", keyStyle);
        Style(bar.EmotesButton, "emote", keyStyle);
        Style(bar.CraftingButton, "craft", keyStyle);
        Style(bar.ActionButton, "action", keyStyle);
        Style(bar.GuidebookButton, "guide", keyStyle);
        Style(bar.AHelpButton, "ahelp", keyStyle);
        Style(bar.AdminButton, "admin", keyStyle);
        Style(bar.SandboxButton, "sandbox", keyStyle);
        Style(bar.EscapeButton, "menu", keyStyle);

        if (language != null)
        {
            Style(language, "language", keyStyle);
            ShowLanguageCode(language);
        }

        UpdatePlate(bar);
    }

    private void UpdatePlate(GameTopMenuBar bar)
    {
        if (bar.Parent?.Parent is not BoxContainer column)
            return;

        var existing = column.Children.FirstOrDefault(c => c.Name == PlateName);
        if (_housing is not { } housing)
        {
            existing?.Orphan();
            return;
        }

        if (existing is not BoxContainer plate)
        {
            plate = new BoxContainer
            {
                Name = PlateName,
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Margin = new Thickness(4, 0, 4, 2),
            };
            plate.AddChild(new Label { Text = Loc.GetString("cmu-hud-housing-plate-name") });
            plate.AddChild(new Control { HorizontalExpand = true });
            plate.AddChild(new Label { Text = Loc.GetString("cmu-hud-housing-plate-serial"), VerticalAlignment = Control.VAlignment.Center });
            column.AddChild(plate);
            plate.SetPositionFirst();
        }

        var labels = plate.Children.OfType<Label>().ToArray();
        labels[0].FontOverride = StyleNano.GetCrtFont(_resources, 12);
        labels[0].FontColorOverride = housing.Ink;
        labels[1].FontOverride = StyleNano.GetCrtFont(_resources, 8);
        labels[1].FontColorOverride = housing.InkDim;
    }

    private static Control Spacer(GameTopMenuBar bar, string name, bool grow)
    {
        if (bar.Children.FirstOrDefault(c => c.Name == name) is { } existing)
            return existing;

        var spacer = new Control
        {
            Name = name,
            MinWidth = GroupGap,
            HorizontalExpand = grow,
        };
        bar.AddChild(spacer);
        return spacer;
    }

    private void Style(MenuButton button, string caption, string keyStyle)
    {
        button.HorizontalExpand = false;
        button.MinSize = new Vector2(KeyMinWidth, 0);

        foreach (var style in KeyStyles)
        {
            if (style != keyStyle)
                button.RemoveStyleClass(style);
        }

        if (!button.HasStyleClass(keyStyle))
            button.AddStyleClass(keyStyle);

        var root = button.ButtonRoot;
        root.SeparationOverride = 2;
        foreach (var child in root.Children)
        {
            switch (child)
            {
                case TextureRect icon:
                    icon.TextureScale = new Vector2(IconScale, IconScale);
                    icon.Margin = new Thickness(0);
                    break;
                // The key label rewrites itself on every rebind, so it is hidden rather than reused.
                case Label { Name: null } keyLabel:
                    keyLabel.Visible = false;
                    break;
            }
        }

        if (root.Children.FirstOrDefault(c => c.Name == CaptionName) is not Label captionLabel)
        {
            captionLabel = new Label { Name = CaptionName, HorizontalAlignment = Control.HAlignment.Center };
            root.AddChild(captionLabel);
        }

        captionLabel.Text = Loc.GetString($"cmu-hud-menu-caption-{caption}");
        captionLabel.FontOverride = GetFont(8);

        if (!_baseTooltips.TryGetValue(button, out var tooltip))
        {
            tooltip = button.ToolTip ?? string.Empty;
            _baseTooltips[button] = tooltip;
        }

        button.ToolTip = _input.TryGetKeyBinding(button.BoundKey, out var binding)
            ? $"{tooltip} ({binding.GetKeyString()})"
            : tooltip;

        button.ColorOverride = KeyColor;
        button.RefreshChildColors();
    }

    private void ShowLanguageCode(MenuButton button)
    {
        var root = button.ButtonRoot;
        if (root.Children.FirstOrDefault(c => c.Name == LanguageCodeName) is not Label code)
        {
            code = new Label
            {
                Name = LanguageCodeName,
                HorizontalAlignment = Control.HAlignment.Center,
                VerticalAlignment = Control.VAlignment.Center,
                MinHeight = 19,
            };
            root.AddChild(code);
            code.SetPositionFirst();
        }

        // A tinted pixel flag among vector icons; the code reads better at this size.
        foreach (var icon in root.Children.OfType<TextureRect>())
        {
            icon.Visible = false;
        }

        code.FontOverride = GetFont(10);
        _languageCode = code;

        if (_languages == null && EntitySystemManager.TryGetEntitySystem(out LanguageSystem? languages))
        {
            _languages = languages;
            _languages.OnLanguagesChanged += UpdateLanguageCode;
        }

        UpdateLanguageCode();
        button.RefreshChildColors();
    }

    private void UpdateLanguageCode()
    {
        if (_languageCode == null)
            return;

        var text = "???";
        if (_languages != null &&
            _player.LocalEntity is { } entity &&
            _prototypes.TryIndex<LanguagePrototype>(_languages.GetCurrentLanguage(entity), out var language))
        {
            text = LanguageCode(language.Name);
        }

        _languageCode.Text = text;
    }

    private static string LanguageCode(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var code = words.Length > 1
            ? string.Concat(words.Select(w => w.Substring(0, 1)))
            : name;

        return (code.Length > 3 ? code.Substring(0, 3) : code).ToUpperInvariant();
    }

    private Font GetFont(int size)
    {
        return StyleNano.CrtUiEnabled
            ? StyleNano.GetCrtFont(_resources, size)
            : _resources.NotoStack(size: size);
    }

    private static Color? KeyColor(MenuButton button)
    {
        var alert = button.HasStyleClass(MenuButton.StyleClassRedTopButton);

        // Keycaps carry printed legends in the plastic's ink; an open window lights them green.
        if (_housing is { } housing && button.HasStyleClass(StyleNano.StyleClassCmuMenuKeyCap))
        {
            return button.DrawMode switch
            {
                DrawModeEnum.Pressed => CrtTerminalPalette.Accent,
                _ when alert => Color.FromHex("#ffd6da"),
                DrawModeEnum.Disabled => housing.InkDim,
                _ => housing.KeyInk,
            };
        }
        var raised = button.HasStyleClass(StyleNano.StyleClassCmuMenuKeyRaised);

        return button.DrawMode switch
        {
            DrawModeEnum.Pressed => alert ? CrtTerminalPalette.Alert : CrtTerminalPalette.TextBright,
            DrawModeEnum.Hover => alert ? CrtTerminalPalette.Alert : CrtTerminalPalette.TextBright,
            DrawModeEnum.Disabled => CrtTerminalPalette.TextDim,
            _ => alert ? CrtTerminalPalette.Alert : raised ? CrtTerminalPalette.Text : CrtTerminalPalette.TextDim,
        };
    }
}
