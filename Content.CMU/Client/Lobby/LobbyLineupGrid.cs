using System.Linq;
using System.Numerics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Lobby;

/// <summary>Fits every child into the available stage. There is no overflowing or virtualized roster.</summary>
public sealed class LobbyLineupGrid : Control
{
    public bool Sections { get; set; }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children)
            child.Measure(availableSize);
        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var children = Children.ToArray();
        var counts = Sections ? children.Cast<LobbyLineupSection>().Select(section => section.Cards.ChildCount).ToArray() : null;
        var cells = LobbyLineupLayout.Fit(children.Length, finalSize, counts);
        for (var i = 0; i < children.Length; i++)
            children[i].Arrange(cells[i]);
        return finalSize;
    }
}

/// <summary>Choose the grid that gives the smallest character the most room.</summary>
public static class LobbyLineupLayout
{
    public static UIBox2[] Fit(int count, Vector2 size, IReadOnlyList<int>? sectionCounts = null)
    {
        if (count == 0)
            return Array.Empty<UIBox2>();
        var bestColumns = 1;
        var bestScore = -1f;
        for (var columns = 1; columns <= count; columns++)
        {
            var rows = (count + columns - 1) / columns;
            var cell = CellSize(size, columns, rows, out _);
            var score = CharacterRoom(cell);
            if (sectionCounts != null)
            {
                score = float.MaxValue;
                foreach (var members in sectionCounts)
                {
                    var formation = FormationSize(cell);
                    var memberScore = 0f;
                    for (var memberColumns = 1; memberColumns <= Math.Max(1, members); memberColumns++)
                    {
                        var memberRows = (Math.Max(1, members) + memberColumns - 1) / memberColumns;
                        memberScore = Math.Max(memberScore, CharacterRoom(CellSize(formation, memberColumns, memberRows, out _)));
                    }
                    score = Math.Min(score, memberScore);
                }
            }
            // Prefer fewer empty cells when the actual character sizes are equivalent.
            score -= (columns * rows - count) * 0.001f;
            if (score <= bestScore)
                continue;
            bestScore = score;
            bestColumns = columns;
        }
        var totalRows = (count + bestColumns - 1) / bestColumns;
        var cellSize = CellSize(size, bestColumns, totalRows, out var gap);
        var result = new UIBox2[count];
        for (var i = 0; i < count; i++)
        {
            var row = i / bestColumns;
            var inRow = Math.Min(bestColumns, count - row * bestColumns);
            var offset = (size.X - inRow * cellSize.X - (inRow - 1) * gap) / 2;
            var position = new Vector2(offset + i % bestColumns * (cellSize.X + gap), row * (cellSize.Y + gap));
            result[i] = UIBox2.FromDimensions(position, cellSize);
        }
        return result;
    }

    public static Vector2 FormationSize(Vector2 section) =>
        new(Math.Max(0, section.X - 8), Math.Max(0, section.Y - Math.Min(24, section.Y * 0.18f) - 4));

    private static float CharacterRoom(Vector2 cell) => Math.Min(cell.X * 0.85f, cell.Y * 0.58f);

    private static Vector2 CellSize(Vector2 size, int columns, int rows, out float gap)
    {
        gap = Math.Min(6, Math.Min(size.X / columns, size.Y / rows) * 0.06f);
        return new Vector2(Math.Max(0, (size.X - (columns - 1) * gap) / columns),
            Math.Max(0, (size.Y - (rows - 1) * gap) / rows));
    }
}
