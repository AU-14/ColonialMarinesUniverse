using Content.Client.ContextMenu.UI;
using Content.Shared.Verbs;
using Robust.Client.UserInterface;

namespace Content.Client.Verbs.UI;

public sealed partial class VerbMenuUIController
{
    private void CMUFillVerbPopup(ContextMenuPopup popup)
    {
        var previous = new List<Control>(popup.MenuBody.Children);
        var categories = new HashSet<string>();
        var extras = new HashSet<string>();
        foreach (var category in ExtraCategories)
            extras.Add(category.Text);

        var position = 0;
        foreach (var verb in CurrentVerbs)
        {
            if (verb.Category == null)
                CMUPlaceVerb(popup, previous, verb, position++);
            else if (!extras.Contains(verb.Category.Text) && categories.Add(verb.Category.Text))
                CMUPlaceCategory(popup, previous, verb.Category, position++);
        }

        foreach (var category in ExtraCategories)
        {
            if (categories.Add(category.Text))
                CMUPlaceCategory(popup, previous, category, position++);
        }

        foreach (var old in previous)
            old.Orphan();
    }

    private void CMUPlaceCategory(ContextMenuPopup popup, List<Control> previous, VerbCategory category, int position)
    {
        var verbs = new List<Verb>();
        var drawIcons = false;
        foreach (var verb in CurrentVerbs)
        {
            if (verb.Category?.Text != category.Text)
                continue;
            verbs.Add(verb);
            drawIcons |= verb.Icon != null || verb.IconEntity != null;
        }

        var style = verbs.Count > 0 ? verbs[0].TextStyleClass : Verb.DefaultTextStyleClass;
        VerbMenuElement? row = null;
        foreach (var old in previous)
        {
            if (old is VerbMenuElement candidate && candidate.CMUCategory is { } oldCategory
                && oldCategory.Text == category.Text && Equals(oldCategory.Icon, category.Icon)
                && oldCategory.IconsOnly == category.IconsOnly && candidate.CMUCategoryStyle == style)
            {
                row = candidate;
                break;
            }
        }

        if (row == null)
        {
            row = new VerbMenuElement(category, style);
            _context.AddElement(popup, row);
            row.SubMenu = new ContextMenuPopup(_context, row);
        }
        else
        {
            previous.Remove(row);
        }

        row.SetPositionInParent(position);
        var submenu = row.SubMenu!;
        submenu.MenuBody.Columns = category.Columns;
        submenu.CMUPopulate = () => CMUFillCategory(submenu, verbs, drawIcons, category.IconsOnly);
        // Once opened, reconcile existing rows immediately so an open submenu never shows stale verbs.
        if (submenu.CMUContentsCreated)
            submenu.CMUEnsureContents();
    }

    private void CMUFillCategory(ContextMenuPopup popup, List<Verb> verbs, bool drawIcons, bool iconsOnly)
    {
        var previous = new List<Control>(popup.MenuBody.Children);
        for (var i = 0; i < verbs.Count; i++)
        {
            var row = CMUPlaceVerb(popup, previous, verbs[i], i);
            row.IconVisible = drawIcons;
            row.TextVisible = !iconsOnly;
        }
        foreach (var old in previous)
            old.Orphan();
    }

    private VerbMenuElement CMUPlaceVerb(ContextMenuPopup popup, List<Control> previous, Verb verb, int position)
    {
        VerbMenuElement? row = null;
        foreach (var old in previous)
        {
            if (old is VerbMenuElement candidate && candidate.CMUMatches(verb))
            {
                row = candidate;
                break;
            }
        }
        if (row == null)
        {
            row = new VerbMenuElement(verb);
            _context.AddElement(popup, row);
        }
        else
        {
            previous.Remove(row);
        }
        row.SetPositionInParent(position);
        return row;
    }
}
