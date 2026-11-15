using System.Numerics;
using Content.Client.Inventory;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;

namespace Content.Client.UserInterface.Systems.Inventory.Controls;

public sealed partial class ItemSlotButtonContainer
{
    private readonly List<CMULayoutSlot> _cmuLayoutSlots = new();
    private readonly List<Control> _cmuLayoutChildren = new();
    private readonly List<Control> _cmuLayoutSpacers = new();
    private int _cmuLayoutColumns;

    private readonly record struct CMULayoutSlot(string Name, Vector2i Position, SlotControl? Button);

    /// <summary>
    /// Refresh presentation independently; only a changed slot layout needs to move controls.
    /// Snapshots belong to this container so replacing the HUD cannot retain the old tree.
    /// </summary>
    public bool CMUUpdateClothingLayout(InventorySlotsComponent inventory)
    {
        var same = ChildCount == _cmuLayoutChildren.Count && Columns == _cmuLayoutColumns;
        var count = 0;
        foreach (var (name, data) in inventory.SlotData)
        {
            if (data.HasSlotGroup)
                continue;

            var slot = new CMULayoutSlot(name, data.ButtonOffset, GetButton(name));
            same &= count < _cmuLayoutSlots.Count && _cmuLayoutSlots[count] == slot;
            count++;
        }

        same &= count == _cmuLayoutSlots.Count;
        if (same)
        {
            for (var i = 0; i < ChildCount; i++)
                same &= GetChild(i) == _cmuLayoutChildren[i];
        }

        if (same)
            return count != 0;

        _cmuLayoutSlots.Clear();
        var width = 0;
        foreach (var (name, data) in inventory.SlotData)
        {
            if (data.HasSlotGroup)
                continue;

            _cmuLayoutSlots.Add(new CMULayoutSlot(name, data.ButtonOffset, GetButton(name)));
            width = Math.Max(width, data.ButtonOffset.X + 1);
        }

        for (var i = ChildCount - 1; i >= 0; i--)
        {
            if (GetChild(i) is not SlotControl)
                RemoveChild(GetChild(i));
        }

        var usedSpacers = 0;
        if (count != 0)
        {
            MaxColumns = width;
            Columns = width;
            var maxIndex = 0;
            foreach (var slot in _cmuLayoutSlots)
                maxIndex = Math.Max(maxIndex, slot.Position.Y * width + slot.Position.X);

            for (var i = 0; i <= maxIndex; i++)
            {
                var found = false;
                foreach (var slot in _cmuLayoutSlots)
                {
                    if (slot.Position.Y * width + slot.Position.X != i)
                        continue;

                    slot.Button?.SetPositionLast();
                    found = true;
                    break;
                }

                if (found)
                    continue;

                if (usedSpacers == _cmuLayoutSpacers.Count)
                    _cmuLayoutSpacers.Add(new Control { MinSize = new Vector2(64, 64) });
                AddChild(_cmuLayoutSpacers[usedSpacers++]);
            }
        }

        // Do not retain spare controls from a previous, larger inventory layout.
        _cmuLayoutSpacers.RemoveRange(usedSpacers, _cmuLayoutSpacers.Count - usedSpacers);
        _cmuLayoutChildren.Clear();
        for (var i = 0; i < ChildCount; i++)
            _cmuLayoutChildren.Add(GetChild(i));
        _cmuLayoutColumns = Columns;
        return count != 0;
    }

    public override void ClearButtons()
    {
        base.ClearButtons();
        foreach (var spacer in _cmuLayoutSpacers)
        {
            if (spacer.Parent == this)
                RemoveChild(spacer);
        }

        _cmuLayoutSpacers.Clear();
        _cmuLayoutSlots.Clear();
        _cmuLayoutChildren.Clear();
    }
}
