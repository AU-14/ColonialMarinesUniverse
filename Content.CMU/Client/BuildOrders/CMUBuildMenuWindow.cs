using System.Numerics;
using Content.Client.CMU14.Interface;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.BuildOrders;

/// <summary>The commander's build menu: pick what to build next, then mark the spot with the other action.</summary>
public sealed class CMUBuildMenuWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public event Action<string>? OnSelect;
    public event Action? OnClear;

    private readonly CrtConsoleKit _kit = new();

    public CMUBuildMenuWindow(List<string> options, string? selected)
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("cmu-build-menu-title");
        MinSize = new Vector2(360, 340);
        SetSize = new Vector2(380, 420);

        var tag = _kit.Tag(out var tagText);
        CrtConsoleKit.SetTag(tag, tagText, Loc.GetString("cmu-build-menu-tag"), CrtTerminalPalette.Accent);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-build-menu-heading"), out var sub, tag));
        sub.Text = Loc.GetString("cmu-build-menu-subheading");

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var body = CrtConsoleKit.Column(6);
        body.HorizontalExpand = true;
        scroll.AddChild(body);
        _kit.Root.AddChild(scroll);

        body.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-build-menu-hint"), CrtTerminalPalette.TextDim));

        foreach (var option in options)
        {
            var name = _prototypes.TryIndex<EntityPrototype>(option, out var proto) ? proto.Name : option;
            var chosen = option == selected;
            var button = _kit.Button(chosen ? $"> {name}" : name,
                chosen ? CmuButtonStyles.Variant.Affirm : CmuButtonStyles.Variant.Neutral);
            button.HorizontalExpand = true;
            var id = option;
            button.OnPressed += _ =>
            {
                OnSelect?.Invoke(id);
                Close();
            };
            body.AddChild(button);
        }

        var clear = _kit.Button(Loc.GetString("cmu-build-menu-clear"), CmuButtonStyles.Variant.Caution);
        clear.OnPressed += _ =>
        {
            OnClear?.Invoke();
            Close();
        };
        body.AddChild(clear);

        _kit.Root.AddChild(_kit.Footer(out var footer));
        footer.Text = Loc.GetString("cmu-build-menu-footer");

        Contents.AddChild(_kit.Tube);
    }
}
