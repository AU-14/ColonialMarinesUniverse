using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Lobby;

/// <summary>Wraps native choice cards using the available editor width.</summary>
public sealed class CmuAdaptiveGrid(float columnWidth) : GridContainer
{
    public CmuAdaptiveGrid() : this(200) { }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var measured = base.MeasureOverride(availableSize);
        if (float.IsPositiveInfinity(availableSize.X))
            return measured;

        // Use the offered width, not the previous arranged width: otherwise an old column count
        // can keep the minimum size wider than a newly narrowed window.
        var minimumWidth = columnWidth;
        var visibleCount = 0;
        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            visibleCount++;
            minimumWidth = Math.Max(minimumWidth, child.DesiredSize.X);
        }

        var columns = Math.Clamp((int) (availableSize.X / (minimumWidth + (HSeparationOverride ?? 4))),
            1, Math.Max(1, visibleCount));
        if (Columns == columns)
            return measured;

        Columns = columns;
        return base.MeasureOverride(availableSize);
    }
}
