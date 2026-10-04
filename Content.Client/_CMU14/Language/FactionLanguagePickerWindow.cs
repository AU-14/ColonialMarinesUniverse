using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._CMU14.Language;

public sealed class FactionLanguagePickerWindow : DefaultWindow
{
    public event Action<string>? OnLanguagePicked;

    private readonly BoxContainer _buttonContainer;

    public FactionLanguagePickerWindow()
    {
        Title = "Choose Faction Language";
        Resizable = false;
        CloseButton.Visible = false;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8)
        };

        root.AddChild(new Label
        {
            Text = "Choose a language for your faction.\nAll members will speak and understand it.",
            Margin = new Thickness(0, 0, 0, 8)
        });

        var scroll = new ScrollContainer { MinHeight = 300 };
        _buttonContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical
        };
        scroll.AddChild(_buttonContainer);
        root.AddChild(scroll);

        Contents.AddChild(root);
        SetSize = new Vector2(320, 400);
    }

    public void Populate(List<string> languages, string factionTag)
    {
        Title = $"Choose Language | {factionTag}";
        _buttonContainer.RemoveAllChildren();

        foreach (var lang in languages)
        {
            var btn = new Button { Text = lang };
            var captured = lang;
            btn.OnPressed += _ => OnLanguagePicked?.Invoke(captured);
            _buttonContainer.AddChild(btn);
        }
    }
}