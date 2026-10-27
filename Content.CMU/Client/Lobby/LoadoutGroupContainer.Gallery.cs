using System.Linq;
using Content.Client.CMU14.Lobby;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI.Loadouts;

public sealed partial class LoadoutGroupContainer
{
    private bool _cmuGallery;

    public void EnableCmuGallery()
    {
        _cmuGallery = true;
        ApplyCmuGallery();
    }

    private void ApplyCmuGallery()
    {
        if (!_cmuGallery) return;
        var items = CollectItems(LoadoutsContainer).ToArray();
        foreach (var item in items) item.Orphan();
        LoadoutsContainer.ReleaseChildren();
        var grid = new CmuAdaptiveGrid(170) { HorizontalExpand = true };
        foreach (var item in items)
        {
            foreach (var toggle in item.Children.OfType<ToggleLoadoutButton>().ToArray())
                toggle.Orphan();
            item.UseCmuGallery();
            grid.AddChild(item);
        }
        LoadoutsContainer.AddChild(grid);
    }

    private static IEnumerable<LoadoutContainer> CollectItems(Control parent)
    {
        foreach (var child in parent.Children)
        {
            if (child is LoadoutContainer item) yield return item;
            else
                foreach (var descendant in CollectItems(child)) yield return descendant;
        }
    }
}
