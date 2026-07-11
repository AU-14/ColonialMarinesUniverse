using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._CMU14.Medical.Presentation.Windows;

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

    private readonly Dictionary<Control, Baseline> _baselines = new();
    private int _applyCount;

    public void Apply(Control root, float scale, IResourceCache resourceCache)
    {
        if ((_applyCount++ & 0x1f) == 0)
            PruneDisposedBaselines();

        ApplyRecursive(root, Math.Clamp(scale, MinimumScale, 1f), resourceCache);
    }

    private void ApplyRecursive(Control control, float scale, IResourceCache resourceCache)
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
            label.FontOverride = GetFont(label, scale, resourceCache);
            label.TextMemory = label.TextMemory;
        }

        if (control is CMUScaledRichTextLabel rich)
            rich.UniformScale = scale;

        foreach (var child in control.Children)
            ApplyRecursive(child, scale, resourceCache);
    }

    private Baseline GetBaseline(Control control)
    {
        if (_baselines.TryGetValue(control, out var baseline))
            return baseline;

        baseline = new Baseline(
            control.Margin,
            control.MinSize,
            control.SetSize,
            control.MaxSize,
            control is BoxContainer box ? box.SeparationOverride : null);
        _baselines.Add(control, baseline);
        return baseline;
    }

    private void PruneDisposedBaselines()
    {
        List<Control>? disposed = null;

        foreach (var entry in _baselines)
        {
            if (!entry.Key.Disposed)
                continue;

            disposed ??= new List<Control>();
            disposed.Add(entry.Key);
        }

        if (disposed == null)
            return;

        foreach (var control in disposed)
        {
            _baselines.Remove(control);
        }
    }

    private static Font GetFont(Label label, float scale, IResourceCache resourceCache)
    {
        var (variation, size, display) = GetFontStyle(label);
        return resourceCache.NotoStack(
            variation,
            Math.Max(6, (int) Math.Round(size * scale)),
            display);
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

    private readonly struct Baseline
    {
        public readonly Thickness Margin;
        public readonly Vector2 MinSize;
        public readonly Vector2 SetSize;
        public readonly Vector2 MaxSize;
        public readonly int? SeparationOverride;

        public Baseline(
            Thickness margin,
            Vector2 minSize,
            Vector2 setSize,
            Vector2 maxSize,
            int? separationOverride)
        {
            Margin = margin;
            MinSize = minSize;
            SetSize = setSize;
            MaxSize = maxSize;
            SeparationOverride = separationOverride;
        }
    }
}
