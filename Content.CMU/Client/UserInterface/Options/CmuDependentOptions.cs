using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.UserInterface.Options;

/// <summary>
///     Settings that only do something while another one is on. Indented under it, and greyed out
///     and unclickable while it is off.
/// </summary>
public sealed class CmuDependentOptions : BoxContainer
{
    private const float Indent = 32;
    private const float InactiveAlpha = 0.4f;

    private Func<bool>? _isActive;
    private bool? _wasActive;

    public CmuDependentOptions()
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 5;
        HorizontalExpand = true;
        Margin = new Thickness(Indent, 0, 0, 0);
    }

    public void DependOn(CheckBox parent)
    {
        DependOn(() => parent.Pressed);
    }

    public void DependOn(Func<bool> isActive)
    {
        _isActive = isActive;
        _wasActive = null;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_isActive == null)
            return;

        var active = _isActive();
        if (active == _wasActive)
            return;

        _wasActive = active;
        Modulate = active ? Color.White : Color.White.WithAlpha(InactiveAlpha);
        SetDisabled(this, !active);
    }

    private static void SetDisabled(Control node, bool disabled)
    {
        foreach (var child in node.Children)
        {
            if (child is BaseButton button)
                button.Disabled = disabled;

            SetDisabled(child, disabled);
        }
    }
}
