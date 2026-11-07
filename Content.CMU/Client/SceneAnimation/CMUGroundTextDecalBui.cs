using Content.Shared.CMU14.SceneAnimation;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.SceneAnimation;

public sealed class CMUGroundTextDecalBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUGroundTextDecalWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUGroundTextDecalWindow>();
        _window.OnPlace += (text, letterSpacing, lineSpacing, wonkiness, color) =>
            SendMessage(new CMUGroundTextDecalMessage(text, letterSpacing, lineSpacing, wonkiness, color));
    }
}
