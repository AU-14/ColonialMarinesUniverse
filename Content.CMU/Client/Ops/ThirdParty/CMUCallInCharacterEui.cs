using System.Linq;
using System.Numerics;
using Content.Client.Eui;
using Content.Shared.CMU14.Ops.ThirdParty;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Ops.ThirdParty;

[UsedImplicitly]
public sealed class CMUCallInCharacterEui : BaseEui
{
    private readonly DefaultWindow _window = new();
    private bool _answered;

    public override void HandleState(EuiStateBase state)
    {
        if (state is not CMUCallInCharacterState offer)
            return;

        _window.Title = Loc.GetString("cmu-call-in-character-title", ("role", offer.Role));
        var layout = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        var instructions = new RichTextLabel();
        instructions.SetMessage(Loc.GetString("cmu-call-in-character-prompt") + "\n" +
                                Loc.GetString("cmu-call-in-character-timeout", ("seconds", offer.Seconds)));
        layout.AddChild(instructions);
        var random = new Button { Text = Loc.GetString("cmu-call-in-character-random") };
        random.OnPressed += _ => Choose(null);
        layout.AddChild(random);

        var scroll = new ScrollContainer { VerticalExpand = true };
        var characters = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        foreach (var (slot, name) in offer.Characters.OrderBy(pair => pair.Key))
        {
            var button = new Button { Text = name };
            button.OnPressed += _ => Choose(slot);
            characters.AddChild(button);
        }
        if (offer.Characters.Count == 0)
            characters.AddChild(new Label { Text = Loc.GetString("cmu-call-in-character-empty") });
        scroll.AddChild(characters);
        layout.AddChild(scroll);
        _window.Contents.AddChild(layout);
        _window.SetSize = new Vector2(460, 340);
        _window.OnClose += () => Choose(null);
        _window.OpenCentered();
    }

    private void Choose(int? slot)
    {
        if (_answered)
            return;
        _answered = true;
        SendMessage(new CMUCallInCharacterChoice(slot));
        _window.Close();
    }

    public override void Closed()
    {
        _answered = true;
        _window.Close();
    }
}
