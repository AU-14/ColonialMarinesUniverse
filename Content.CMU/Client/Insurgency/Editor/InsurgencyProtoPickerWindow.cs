using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._AU14.Insurgency.Editor;

/// <summary>
///     Searchable list picker for plain (id, label) options: jobs, ships, faction icons, and the
///     like. Same search-and-click shape as the entity picker but without sprites. Keeps the editor
///     free of typed prototype ids for these fields too.
/// </summary>
public sealed class InsurgencyProtoPickerWindow : DefaultWindow
{
    private const int MaxRows = 300;

    private readonly BoxContainer _rows;
    private readonly List<(string Id, string Display, string Haystack)> _all;

    public event Action<string>? OnSelected;

    public InsurgencyProtoPickerWindow(string title, IEnumerable<(string Id, string Display)> options)
    {
        Title = title;
        MinSize = new Vector2(460, 520);

        _all = options
            .Select(o => (o.Id, o.Display, $"{o.Display} {o.Id}".ToLowerInvariant()))
            .ToList();

        var search = new LineEdit { PlaceHolder = Loc.GetString("insfor-picker-search"), HorizontalExpand = true };
        _rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };

        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_rows);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        root.AddChild(search);
        root.AddChild(scroll);
        Contents.AddChild(root);

        Refresh(string.Empty);
        search.OnTextChanged += args => Refresh(args.Text);
        InsforUiStyle.Apply(this);
    }

    private void Refresh(string filter)
    {
        _rows.RemoveAllChildren();

        var needle = filter.Trim().ToLowerInvariant();
        var count = 0;
        foreach (var entry in _all)
        {
            if (needle.Length > 0 && !entry.Haystack.Contains(needle))
                continue;

            var id = entry.Id;
            var row = new Button { Text = entry.Display, HorizontalExpand = true, Margin = new Thickness(0, 0, 0, 2) };
            row.OnPressed += _ =>
            {
                OnSelected?.Invoke(id);
                Close();
            };
            _rows.AddChild(row);

            if (++count >= MaxRows)
                break;
        }
    }
}
