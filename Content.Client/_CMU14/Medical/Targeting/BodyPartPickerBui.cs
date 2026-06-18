using System.Collections.Generic;
using Content.Client.UserInterface.Controls;
using Content.Shared._CMU14.Medical.Targeting;
using Content.Shared._CMU14.Medical.Human.Data;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Medical.Targeting;

[UsedImplicitly]
public sealed class BodyPartPickerBui : BoundUserInterface
{
    private static readonly ResPath RadialBodySelectRsi = new("/Textures/_CMU14/Medical/HUD/radial_body_select.rsi");

    [ViewVariables]
    private SimpleRadialMenu? _menu;

    private BodyPartPickerBuiState? _state;

    public BodyPartPickerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);

        if (_state != null)
            Refresh(_state);
        else if (State is BodyPartPickerBuiState s)
            Refresh(s);

        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not BodyPartPickerBuiState s)
            return;

        _state = s;
        Refresh(s);
    }

    private void Refresh(BodyPartPickerBuiState state)
    {
        if (_menu is null)
            return;

        _menu.SetButtons(ConvertToButtons(state.Available));
    }

    private IEnumerable<RadialMenuActionOption> ConvertToButtons(List<BodyPartPickerEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new RadialMenuActionOption<BodyPartPickerEntry>(OnPartPressed, entry)
            {
                Sprite = SpriteForRegion(entry),
                ToolTip = TooltipForEntry(entry),
            };
        }
    }

    private void OnPartPressed(BodyPartPickerEntry entry)
    {
        SendMessage(new BodyPartPickerSelectMessage(entry.Part, entry.Region));
    }

    private static string TooltipForEntry(BodyPartPickerEntry entry)
    {
        var part = Loc.GetString(LocaleForRegion(entry.Region));
        if (entry.UntreatedWounds <= 0)
            return part;

        return Loc.GetString(
            "cmu-body-part-picker-wounds",
            ("part", part),
            ("count", entry.UntreatedWounds));
    }

    private static SpriteSpecifier SpriteForRegion(BodyPartPickerEntry entry)
    {
        var part = entry.Region switch
        {
            BodyRegion.Head => "head",
            BodyRegion.Chest => "chest",
            BodyRegion.Groin => "groin",
            BodyRegion.LeftArm => "l_arm",
            BodyRegion.RightArm => "r_arm",
            BodyRegion.LeftHand => "l_hand",
            BodyRegion.RightHand => "r_hand",
            BodyRegion.LeftLeg => "l_leg",
            BodyRegion.RightLeg => "r_leg",
            BodyRegion.LeftFoot => "l_foot",
            BodyRegion.RightFoot => "r_foot",
            _ => "chest",
        };

        var status = entry.RadialStatus switch
        {
            BodyPartPickerRadialStatus.Brute => "brute",
            BodyPartPickerRadialStatus.Burn => "burn",
            BodyPartPickerRadialStatus.Both => "both",
            BodyPartPickerRadialStatus.Surgery => "surgery",
            _ => "un",
        };

        return new SpriteSpecifier.Rsi(RadialBodySelectRsi, $"radial_{part}_{status}");
    }

    private static string LocaleForRegion(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.Head => "cmu-body-part-picker-head",
            BodyRegion.Chest => "cmu-body-part-picker-chest",
            BodyRegion.Groin => "cmu-body-part-picker-groin",
            BodyRegion.LeftArm => "cmu-body-part-picker-left-arm",
            BodyRegion.RightArm => "cmu-body-part-picker-right-arm",
            BodyRegion.LeftHand => "cmu-body-part-picker-left-hand",
            BodyRegion.RightHand => "cmu-body-part-picker-right-hand",
            BodyRegion.LeftLeg => "cmu-body-part-picker-left-leg",
            BodyRegion.RightLeg => "cmu-body-part-picker-right-leg",
            BodyRegion.LeftFoot => "cmu-body-part-picker-left-foot",
            BodyRegion.RightFoot => "cmu-body-part-picker-right-foot",
            _ => "cmu-body-part-picker-chest",
        };
    }
}
