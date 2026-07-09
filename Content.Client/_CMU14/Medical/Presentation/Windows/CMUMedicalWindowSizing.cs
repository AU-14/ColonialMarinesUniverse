using System.Collections.Generic;
using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._CMU14.Medical.Presentation.Windows;

internal static class CMUMedicalWindowSizing
{
    private static readonly Vector2 WindowMargin = new(64f, 80f);
    private static readonly Vector2 AbsoluteFloor = new(360f, 300f);
    private static readonly Dictionary<string, Vector2> RememberedSizes = new();

    public static Vector2 GetInitialSize(string key, Vector2 preferredSize)
    {
        return RememberedSizes.TryGetValue(key, out var size) && IsValidSize(size)
            ? size
            : preferredSize;
    }

    public static void FitToScreen(
        BaseWindow window,
        Vector2 preferredSize,
        Vector2 minimumSize,
        bool clampPosition = true)
    {
        var rootSize = window.UserInterfaceManager.WindowRoot.Size;
        if (rootSize.X <= 0f || rootSize.Y <= 0f)
            return;

        var maxSize = Vector2.Min(rootSize, Vector2.Max(AbsoluteFloor, rootSize - WindowMargin));
        var minSize = Vector2.Min(minimumSize, maxSize);
        var current = ResolveSetSize(window.SetSize, preferredSize);
        var target = Vector2.Max(minSize, Vector2.Min(current, maxSize));

        if (window.MinSize != minSize)
            window.MinSize = minSize;
        if (window.MaxSize != maxSize)
            window.MaxSize = maxSize;
        if (window.SetSize != target)
            window.SetSize = target;

        if (!clampPosition || window.Parent is not { } parent)
            return;

        var maxPosition = Vector2.Max(Vector2.Zero, parent.Size - target);
        var position = Vector2.Max(Vector2.Zero, Vector2.Min(window.Position, maxPosition));
        if (window.Position != position)
            LayoutContainer.SetPosition(window, position);
    }

    public static void RememberSize(string key, BaseWindow window)
    {
        var size = window.SetSize;
        if (!IsValidSize(size))
            size = window.Size;

        if (!IsValidSize(size))
            return;

        if (RememberedSizes.TryGetValue(key, out var remembered) && remembered == size)
            return;

        RememberedSizes[key] = size;
    }

    private static Vector2 ResolveSetSize(Vector2 current, Vector2 fallback)
    {
        return new Vector2(
            float.IsNaN(current.X) ? fallback.X : current.X,
            float.IsNaN(current.Y) ? fallback.Y : current.Y);
    }

    private static bool IsValidSize(Vector2 size)
    {
        return size.X > 0f &&
            size.Y > 0f &&
            !float.IsNaN(size.X) &&
            !float.IsNaN(size.Y) &&
            !float.IsInfinity(size.X) &&
            !float.IsInfinity(size.Y);
    }
}
