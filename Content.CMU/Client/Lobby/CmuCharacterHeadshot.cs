using System.Numerics;
using Content.Client.Lobby.UI.ProfileEditorControls;
using Content.Shared.Preferences;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Lobby;

/// <summary>A cropped live profile portrait, updated only when the edited appearance changes.</summary>
public sealed class CmuCharacterHeadshot : Control
{
    private readonly Func<HumanoidCharacterProfile?> _profile;
    private readonly ProfilePreviewSpriteView _view = new()
    {
        Scale = new Vector2(3), Stretch = SpriteView.StretchMode.None,
        OverrideDirection = Direction.South, MouseFilter = MouseFilterMode.Ignore,
    };
    private HumanoidCharacterProfile? _previous;

    public CmuCharacterHeadshot(Func<HumanoidCharacterProfile?> profile)
    {
        _profile = profile;
        MinSize = MaxSize = new Vector2(56);
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Ignore;
        AddChild(_view);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _view.Measure(new Vector2(96));
        return new Vector2(56);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        _view.Arrange(UIBox2.FromDimensions(new Vector2(-20, -3), new Vector2(96)));
        return finalSize;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var profile = _profile();
        if (profile == null || ReferenceEquals(profile, _previous)) return;
        var previous = _previous;
        _previous = profile;
        if (previous != null && previous.Species == profile.Species && previous.Sex == profile.Sex &&
            previous.Appearance == profile.Appearance) return;
        _view.LoadPreview(profile, showClothes: false);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _previous = null;
    }
}
