using System.Numerics;
using Content.Client.Options.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.UserInterface.Options;

/// <summary>
///     Narrows every tab of the options menu to the settings matching a typed query, and puts it
///     all back exactly as it was when the query is cleared.
/// </summary>
public sealed class CmuOptionsFilter
{
    private readonly TabContainer _tabs;

    private readonly Dictionary<Control, bool> _originalVisible = new();
    private readonly Dictionary<CmuOptionSection, bool> _originalExpanded = new();
    private bool[]? _originalTabVisible;

    public CmuOptionsFilter(TabContainer tabs)
    {
        _tabs = tabs;
    }

    /// <summary>
    ///     Applies <paramref name="query"/> and returns how many settings match. An empty query
    ///     clears the filter and returns 0.
    /// </summary>
    public int Apply(string query)
    {
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            Clear();
            return 0;
        }

        _originalTabVisible ??= SnapshotTabs();

        var perTab = new int[_tabs.ChildCount];
        var total = 0;

        for (var i = 0; i < _tabs.ChildCount; i++)
        {
            // A tab the menu was already hiding - the Admin tab, for a non-admin - is left entirely alone.
            if (!_originalTabVisible[i])
                continue;

            var tab = _tabs.GetChild(i);
            perTab[i] = FilterTab(tab, TabContainer.GetTabTitle(tab) ?? string.Empty, tokens);
            total += perTab[i];
        }

        if (total == 0)
        {
            // Nothing anywhere.
            RestoreTabs();
            return 0;
        }

        for (var i = 0; i < _tabs.ChildCount; i++)
        {
            _tabs.SetTabVisible(i, _originalTabVisible[i] && perTab[i] > 0);
        }

        if (perTab[_tabs.CurrentTab] == 0)
        {
            for (var i = 0; i < perTab.Length; i++)
            {
                if (perTab[i] == 0)
                    continue;

                _tabs.CurrentTab = i;
                break;
            }
        }

        ScrollToTop(_tabs.GetChild(_tabs.CurrentTab));
        return total;
    }

    /// <summary>
    ///     Puts every control, group and tab back to the state it was in before filtering began.
    /// </summary>
    public void Clear()
    {
        foreach (var (control, visible) in _originalVisible)
        {
            control.Visible = visible;
        }

        foreach (var (section, expanded) in _originalExpanded)
        {
            section.Expanded = expanded;
        }

        RestoreTabs();

        _originalVisible.Clear();
        _originalExpanded.Clear();
        _originalTabVisible = null;
    }

    private int FilterTab(Control tab, string tabTitle, string[] tokens)
    {
        var matches = 0;
        foreach (var section in FindSections(tab))
        {
            matches += FilterSection(section, tabTitle, tokens);
        }

        return matches;
    }

    private int FilterSection(CmuOptionSection section, string tabTitle, string[] tokens)
    {
        if (!Remember(section))
            return 0;

        var context = $"{tabTitle} {section.Title}";
        var matches = 0;

        foreach (var option in section.Options)
        {
            matches += FilterNode(option, context, tokens);
        }

        section.Visible = matches > 0;

        // A folded group with a hit in it would otherwise report a match you cannot see.
        if (matches > 0 && !section.Expanded)
        {
            _originalExpanded.TryAdd(section, false);
            section.Expanded = true;
        }

        return matches;
    }

    private int FilterNode(Control node, string context, string[] tokens)
    {
        if (!Remember(node))
            return 0;

        switch (node)
        {
            case CmuSpeciesSoundMatrix matrix:
            {
                var matches = 0;
                foreach (var (row, text) in matrix.SpeciesRows)
                {
                    if (!Remember(row))
                        continue;

                    var show = Matches($"{context} {text}", tokens);
                    row.Visible = show;
                    if (show)
                        matches++;
                }

                if (Remember(matrix.Header))
                    matrix.Header.Visible = matches > 0;

                matrix.Visible = matches > 0;
                return matches;
            }

            // The paired-column layouts.
            case BoxContainer layout:
            {
                var matches = 0;
                foreach (var child in layout.Children)
                {
                    matches += FilterNode(child, context, tokens);
                }

                layout.Visible = matches > 0;
                return matches;
            }

            default:
            {
                var show = Matches($"{context} {SearchText(node)}", tokens);
                node.Visible = show;
                return show ? 1 : 0;
            }
        }
    }

    private static string SearchText(Control node)
    {
        var label = node switch
        {
            CheckBox checkBox => checkBox.Text,
            OptionDropDown dropDown => dropDown.Title,
            OptionSlider slider => slider.Title,
            OptionColorSlider colour => colour.Title,
            // Keybind rows are a private type inside the keybind tab, so they are recognised by shape.
            _ => FirstLabel(node),
        };

        return $"{label} {node.ToolTip}";
    }

    private static string? FirstLabel(Control node)
    {
        foreach (var child in node.Children)
        {
            if (child is Label label)
                return label.Text;

            if (FirstLabel(child) is { } found)
                return found;
        }

        return null;
    }

    private static bool Matches(string text, string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (!text.Contains(token, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static IEnumerable<CmuOptionSection> FindSections(Control root)
    {
        foreach (var child in root.Children)
        {
            if (child is CmuOptionSection section)
            {
                yield return section;
                continue;
            }

            foreach (var inner in FindSections(child))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    ///     Records <paramref name="control"/>'s visibility the first time it is touched and returns
    ///     that original value on every later call - see the class remarks.
    /// </summary>
    private bool Remember(Control control)
    {
        if (!_originalVisible.TryGetValue(control, out var visible))
        {
            visible = control.Visible;
            _originalVisible[control] = visible;
        }

        return visible;
    }

    private bool[] SnapshotTabs()
    {
        var visible = new bool[_tabs.ChildCount];
        for (var i = 0; i < visible.Length; i++)
        {
            visible[i] = _tabs.GetTabVisible(i);
        }

        return visible;
    }

    private void RestoreTabs()
    {
        if (_originalTabVisible == null)
            return;

        for (var i = 0; i < _originalTabVisible.Length; i++)
        {
            _tabs.SetTabVisible(i, _originalTabVisible[i]);
        }
    }

    private static void ScrollToTop(Control tab)
    {
        foreach (var child in tab.Children)
        {
            if (child is ScrollContainer scroll)
            {
                scroll.SetScrollValue(Vector2.Zero);
                return;
            }

            ScrollToTop(child);
        }
    }
}
