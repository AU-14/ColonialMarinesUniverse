using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Medical.Presentation.Windows;

public sealed class CMUScaledRichTextLabel : RichTextLabel
{
    private float _uniformScale = 1f;

    public float UniformScale
    {
        get => _uniformScale;
        set
        {
            if (Math.Abs(_uniformScale - value) < 0.001f)
                return;

            _uniformScale = value;
            UIScaleChanged();
        }
    }

    public override float UIScale => base.UIScale * _uniformScale;
}

internal sealed class CMUMedicalUniformScaler
{
    public const float MinimumScale = 0.35f;

    private const float NormalFontSize = 10f;
    private const float HeadingFontSize = 16f;
    private const float HeadingBiggerFontSize = 20f;
    private const float KeyFontSize = 12f;
    private const float SmallFontSize = 8f;

    // ConditionalWeakTable is unavailable in the content sandbox. Keep controls weakly referenced instead.
    private readonly Dictionary<int, List<Baseline>> _baselines = new();

    public void Apply(Control root, float scale, IResourceCache resourceCache)
    {
        PruneBaselines();
        var fonts = new Dictionary<(string Variation, int Size, bool Display), Font>();
        ApplyRecursive(root, Math.Clamp(scale, MinimumScale, 1f), resourceCache, fonts);
    }

    public void Apply(IReadOnlyList<Control> roots, float scale, IResourceCache resourceCache)
    {
        if (roots.Count == 0)
            return;

        PruneBaselines();
        var fonts = new Dictionary<(string Variation, int Size, bool Display), Font>();
        scale = Math.Clamp(scale, MinimumScale, 1f);
        foreach (var root in roots)
            ApplyRecursive(root, scale, resourceCache, fonts);
    }

    private void ApplyRecursive(Control control, float scale, IResourceCache resourceCache,
        Dictionary<(string Variation, int Size, bool Display), Font> fonts)
    {
        var baseline = GetBaseline(control);

        control.Margin = Scale(baseline.Margin, scale);
        control.MinSize = Scale(baseline.MinSize, scale);
        control.SetSize = ScaleOptional(baseline.SetSize, scale);
        control.MaxSize = ScaleOptional(baseline.MaxSize, scale);

        if (control is BoxContainer box)
            box.SeparationOverride = ScaleOptional(baseline.SeparationOverride, scale);

        if (control is Label label)
        {
            var font = GetFont(label, scale, resourceCache, fonts);
            if (!ReferenceEquals(label.FontOverride, font))
                label.FontOverride = font;
        }

        if (control is CMUScaledRichTextLabel rich)
            rich.UniformScale = scale;

        foreach (var child in control.Children)
            ApplyRecursive(child, scale, resourceCache, fonts);
    }

    private Baseline GetBaseline(Control control)
    {
        var hash = RuntimeHelpers.GetHashCode(control);
        if (!_baselines.TryGetValue(hash, out var bucket))
        {
            bucket = new List<Baseline>();
            _baselines.Add(hash, bucket);
        }

        // Identity hashes may collide, so compare the live controls within each bucket.
        foreach (var entry in bucket)
        {
            if (entry.Control.TryGetTarget(out var target) && ReferenceEquals(target, control))
                return entry;
        }

        var baseline = new Baseline(
            control,
            control.Margin,
            control.MinSize,
            control.SetSize,
            control.MaxSize,
            control is BoxContainer box ? box.SeparationOverride : null);
        bucket.Add(baseline);
        return baseline;
    }

    private void PruneBaselines()
    {
        List<int>? empty = null;
        foreach (var (hash, bucket) in _baselines)
        {
            bucket.RemoveAll(static entry => !entry.Control.TryGetTarget(out var control) || control.Disposed);
            if (bucket.Count == 0)
            {
                empty ??= new List<int>();
                empty.Add(hash);
            }
        }

        if (empty == null)
            return;

        foreach (var hash in empty)
            _baselines.Remove(hash);
    }

    private static Font GetFont(Label label, float scale, IResourceCache resourceCache,
        Dictionary<(string Variation, int Size, bool Display), Font> fonts)
    {
        var (variation, size, display) = GetFontStyle(label);
        var key = (Variation: variation, Size: Math.Max(6, (int) Math.Round(size * scale)), Display: display);
        if (fonts.TryGetValue(key, out var font))
            return font;

        // Resolve again on the next application, preserving resource replacement behavior.
        font = resourceCache.NotoStack(key.Variation, key.Size, key.Display);
        fonts.Add(key, font);
        return font;
    }

    private static (string Variation, float Size, bool Display) GetFontStyle(Label label)
    {
        if (label.StyleClasses.Contains("LabelHeadingBigger"))
            return ("Bold", HeadingBiggerFontSize, false);

        if (label.StyleClasses.Contains("LabelHeading"))
            return ("Bold", HeadingFontSize, false);

        if (label.StyleClasses.Contains("LabelKeyText"))
            return ("Bold", KeyFontSize, false);

        if (label.StyleClasses.Contains("LabelSmall") ||
            label.StyleClasses.Contains("LabelSubText") ||
            label.StyleClasses.Contains("WindowFooterText"))
        {
            return ("Regular", SmallFontSize, false);
        }

        return ("Regular", NormalFontSize, false);
    }

    private static Thickness Scale(Thickness value, float scale)
    {
        return new Thickness(
            value.Left * scale,
            value.Top * scale,
            value.Right * scale,
            value.Bottom * scale);
    }

    private static Vector2 Scale(Vector2 value, float scale)
    {
        return new Vector2(
            ScaleOptional(value.X, scale),
            ScaleOptional(value.Y, scale));
    }

    private static Vector2 ScaleOptional(Vector2 value, float scale)
    {
        return new Vector2(
            ScaleOptional(value.X, scale),
            ScaleOptional(value.Y, scale));
    }

    private static float ScaleOptional(float value, float scale)
    {
        return float.IsNaN(value) || float.IsInfinity(value)
            ? value
            : value * scale;
    }

    private static int? ScaleOptional(int? value, float scale)
    {
        return value is { } actual
            ? Math.Max(0, (int) Math.Round(actual * scale))
            : null;
    }

    private sealed class Baseline
    {
        public readonly WeakReference<Control> Control;
        public readonly Thickness Margin;
        public readonly Vector2 MinSize;
        public readonly Vector2 SetSize;
        public readonly Vector2 MaxSize;
        public readonly int? SeparationOverride;

        public Baseline(
            Control control,
            Thickness margin,
            Vector2 minSize,
            Vector2 setSize,
            Vector2 maxSize,
            int? separationOverride)
        {
            Control = new WeakReference<Control>(control);
            Margin = margin;
            MinSize = minSize;
            SetSize = setSize;
            MaxSize = maxSize;
            SeparationOverride = separationOverride;
        }
    }
}
