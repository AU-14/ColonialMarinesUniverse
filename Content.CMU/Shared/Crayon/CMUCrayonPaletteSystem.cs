using Content.Shared.Containers;
using Content.Shared.Crayon;
using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Crayon;

/// <summary>
/// The crayons custom symbols are drawn with: those in the default crayon box, each in a run of tones
/// from light to dark so one crayon draws gradients of its own colour. Any other crayon counts as the
/// box crayon nearest its colour.
/// </summary>
public sealed class CMUCrayonPaletteSystem : EntitySystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public static readonly EntProtoId Box = "CrayonBox";

    /// <summary>
    /// Tones either side of a crayon's own colour, each a step further towards white or black.
    /// </summary>
    public const int Tones = 4;

    private const float Step = 0.15f;

    private List<CMUCrayonColor>? _crayons;

    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _crayons = null);
    }

    /// <summary>
    /// The box's crayons in the order it holds them, once each.
    /// </summary>
    public IReadOnlyList<CMUCrayonColor> Crayons
    {
        get
        {
            if (_crayons != null)
                return _crayons;

            _crayons = new List<CMUCrayonColor>();
            if (!_prototypes.TryIndex(Box, out var box) || !box.TryGetComponent<EntityTableContainerFillComponent>(out var fill, _factory))
                return _crayons;

            var spawns = new List<EntProtoId>();
            foreach (var table in fill.Containers.Values)
            {
                foreach (var (spawn, _) in table.ListSpawns(EntityManager, _prototypes, new EntityTableContext()))
                {
                    spawns.Add(spawn);
                }
            }

            foreach (var id in spawns)
            {
                if (!_prototypes.TryIndex(id, out var proto) ||
                    !proto.TryGetComponent<CrayonComponent>(out var crayon, _factory) ||
                    crayon.SelectableColor ||
                    _crayons.Exists(c => c.Id == id))
                {
                    continue;
                }

                _crayons.Add(new CMUCrayonColor(id, proto.Name, Quantize(crayon.Color)));
            }

            return _crayons;
        }
    }

    /// <summary>
    /// A colour lightened (tone above 0) or darkened (below 0) by whole steps, to whole bytes so it
    /// reads back the same wherever it is sent or saved.
    /// </summary>
    public static Color Shade(Color color, int tone)
    {
        var k = Math.Clamp(Math.Abs(tone), 0, Tones) * Step;
        var shaded = tone > 0
            ? new Color(color.R + (1f - color.R) * k, color.G + (1f - color.G) * k, color.B + (1f - color.B) * k)
            : new Color(color.R * (1f - k), color.G * (1f - k), color.B * (1f - k));
        return Quantize(shaded);
    }

    /// <summary>
    /// A crayon's tones from lightest to darkest, each colour once.
    /// </summary>
    public static List<(int Tone, Color Color)> Shades(Color color)
    {
        var shades = new List<(int Tone, Color Color)>();
        for (var tone = Tones; tone >= -Tones; tone--)
        {
            var shade = Shade(color, tone);
            var index = shades.FindIndex(s => Same(s.Color, shade));
            if (index < 0)
                shades.Add((tone, shade));
            // White cannot get lighter: its lighter tones are white itself, which is tone 0.
            else if (Math.Abs(tone) < Math.Abs(shades[index].Tone))
                shades[index] = (tone, shade);
        }

        return shades;
    }

    /// <summary>
    /// The box crayon a symbol colour is a tone of, and which tone.
    /// </summary>
    public bool TryGetFamily(Color color, out int family, out int tone)
    {
        var crayons = Crayons;
        for (family = 0; family < crayons.Count; family++)
        {
            foreach (var (shadeTone, shade) in Shades(crayons[family].Color))
            {
                if (!Same(shade, color))
                    continue;

                tone = shadeTone;
                return true;
            }
        }

        family = -1;
        tone = 0;
        return false;
    }

    /// <summary>
    /// The box crayon nearest a crayon's colour, which it draws as.
    /// </summary>
    public int NearestFamily(Color color)
    {
        var crayons = Crayons;
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < crayons.Count; i++)
        {
            var c = crayons[i].Color;
            var distance = (c.R - color.R) * (c.R - color.R) + (c.G - color.G) * (c.G - color.G) + (c.B - color.B) * (c.B - color.B);
            if (distance >= bestDistance)
                continue;

            best = i;
            bestDistance = distance;
        }

        return best;
    }

    public static bool Same(Color a, Color b)
    {
        return Byte(a.R) == Byte(b.R) && Byte(a.G) == Byte(b.G) && Byte(a.B) == Byte(b.B);
    }

    private static Color Quantize(Color color)
    {
        return new Color(Byte(color.R), Byte(color.G), Byte(color.B));
    }

    private static byte Byte(float channel)
    {
        return (byte) Math.Clamp(MathF.Round(channel * 255f), 0f, 255f);
    }
}

/// <summary>
/// One of the default box's crayons.
/// </summary>
public readonly record struct CMUCrayonColor(EntProtoId Id, string Name, Color Color);
