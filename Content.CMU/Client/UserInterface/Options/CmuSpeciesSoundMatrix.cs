using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.UserInterface.Options;

/// <summary>
///     One row per species, one column per sound kind - the voiceline and emote toggles as a table
///     rather than two lists of the same thirteen names.
/// </summary>
public sealed class CmuSpeciesSoundMatrix : Control
{
    private const int SpeciesColumnWidth = 160;
    private const int ToggleColumnWidth = 120;

    private const int RowHeight = 28;

    private readonly BoxContainer _rows;
    private readonly BoxContainer _header;
    private readonly List<(Control Row, string SearchText)> _speciesRows = new();

    /// <summary>The column header row. Shown by the filter whenever any species row is.</summary>
    public Control Header => _header;

    /// <summary>
    ///     One entry per species row, with the text a filter should match it on. Read by
    ///     <see cref="CmuOptionsFilter"/> - the checkboxes in a row carry no text of their own, so
    ///     without this a search for "moth" would find nothing.
    /// </summary>
    public IReadOnlyList<(Control Row, string SearchText)> SpeciesRows => _speciesRows;

    public CmuSpeciesSoundMatrix()
    {
        _rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 0,
        };
        AddChild(_rows);

        _header = NewRow();
        _header.AddChild(HeaderCell(Loc.GetString("cmu-ui-options-species-column"), SpeciesColumnWidth));
        _header.AddChild(HeaderCell(Loc.GetString("rmc-ui-voicelines"), ToggleColumnWidth));
        _header.AddChild(HeaderCell(Loc.GetString("rmc-ui-emotes"), ToggleColumnWidth));
    }

    /// <summary>
    ///     Adds one species row. The two checkboxes come back for the tab to bind to cvars - this
    ///     control owns the layout and nothing else.
    /// </summary>
    public (CheckBox Voiceline, CheckBox Emote) Add(string speciesName)
    {
        var row = NewRow();

        row.AddChild(new Label
        {
            Text = speciesName,
            MinWidth = SpeciesColumnWidth,
            VerticalAlignment = VAlignment.Center,
        });

        var voiceline = new CheckBox { MinWidth = ToggleColumnWidth };
        row.AddChild(voiceline);

        var emote = new CheckBox { MinWidth = ToggleColumnWidth };
        row.AddChild(emote);

        // The column names are in the search text too, so "emotes" finds every row rather than none.
        _speciesRows.Add((row, $"{speciesName} {Loc.GetString("rmc-ui-voicelines")} {Loc.GetString("rmc-ui-emotes")}"));

        return (voiceline, emote);
    }

    private BoxContainer NewRow()
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 0,
        };
        _rows.AddChild(row);
        return row;
    }

    private static Label HeaderCell(string text, int width)
    {
        var label = new Label
        {
            Text = text,
            MinWidth = width,
            MinHeight = RowHeight,
            VerticalAlignment = VAlignment.Center,
        };
        label.AddStyleClass(StyleNano.StyleClassLabelKeyText);
        return label;
    }
}
