using System.Numerics;
using System.Linq;
using Content.Server.Decals;
using Content.Shared.CMU14.SceneAnimation;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.SceneAnimation;

/// <summary>
/// Converts short lines of text into the base game's stencil decals in front of the tool user.
/// </summary>
public sealed class CMUGroundTextDecalSystem : EntitySystem
{
    private const int MaxCharacters = 200;
    private const int MaxLines = 8;
    private const int MaxLineLength = 48;

    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<CMUGroundTextDecalToolComponent>(CMUGroundTextDecalUiKey.Key, subs =>
        {
            subs.Event<CMUGroundTextDecalMessage>(OnPlaceText);
        });
    }

    private void OnPlaceText(Entity<CMUGroundTextDecalToolComponent> ent, ref CMUGroundTextDecalMessage args)
    {
        if (!_hands.IsHolding(args.Actor, ent.Owner) || _timing.CurTime < ent.Comp.NextUseAt)
            return;

        var actorXform = Transform(args.Actor);
        if (actorXform.GridUid is not { } grid || actorXform.MapID == MapId.Nullspace)
            return;

        var lines = args.Text
            .Replace("\r", string.Empty)
            .Split('\n')
            .Take(MaxLines)
            .Select(line => line[..Math.Min(line.Length, MaxLineLength)])
            .ToArray();

        if (lines.Length == 0 || lines.Sum(line => line.Count(c => !char.IsWhiteSpace(c))) == 0)
            return;

        var letterSpacing = Math.Clamp(args.LetterSpacing, 0.35f, 2f);
        var lineSpacing = Math.Clamp(args.LineSpacing, 0.5f, 3f);
        var wonkiness = Math.Clamp(args.Wonkiness, 0f, 0.75f);
        var world = _transform.GetMapCoordinates(actorXform);
        var facing = _transform.GetWorldRotation(actorXform);
        var forward = facing.RotateVec(Vector2.UnitY);
        var right = facing.RotateVec(Vector2.UnitX);
        var origin = world.Position + forward * 1.5f;
        var written = 0;

        for (var lineIndex = 0; lineIndex < lines.Length && written < MaxCharacters; lineIndex++)
        {
            var line = lines[lineIndex];
            var lineWidth = Math.Max(0, line.Length - 1) * letterSpacing;
            var lineOrigin = origin - right * lineWidth / 2f - forward * lineIndex * lineSpacing;

            for (var column = 0; column < line.Length && written < MaxCharacters; column++)
            {
                if (!TryGetGlyph(line[column], out var decal))
                    continue;

                var offset = right * column * letterSpacing;
                if (wonkiness > 0f)
                {
                    offset += right * _random.NextFloat(-0.25f, 0.25f) * wonkiness;
                    offset += forward * _random.NextFloat(-0.25f, 0.25f) * wonkiness;
                }

                var rotation = facing + Angle.FromDegrees(_random.NextFloat(-35f, 35f) * wonkiness);
                var mapCoordinates = new MapCoordinates(lineOrigin + offset, world.MapId);
                var coordinates = _transform.ToCoordinates(grid, mapCoordinates);
                if (_decals.TryAddDecal(decal, coordinates, out _, args.Color, rotation, cleanable: true))
                    written++;
            }
        }

        if (written > 0)
            ent.Comp.NextUseAt = _timing.CurTime + ent.Comp.Cooldown;
    }

    private static bool TryGetGlyph(char character, out string decal)
    {
        character = char.ToUpperInvariant(character);
        if (character is >= 'A' and <= 'Z')
        {
            decal = $"StencilLetter{character}";
            return true;
        }

        if (character is >= '0' and <= '9')
        {
            decal = $"StencilNumber{character}";
            return true;
        }

        decal = character switch
        {
            '&' => "StencilSymbolAmpersand",
            '*' => "StencilSymbolAsterix",
            '-' => "StencilSymbolDash",
            '=' => "StencilSymbolEquals",
            '!' => "StencilSymbolExclaim",
            '#' => "StencilSymbolHash",
            '$' => "StencilSymbolSpeso",
            '@' => "StencilSymbolMultiocular",
            '+' => "StencilSymbolPlus",
            '?' => "StencilSymbolQuestion",
            _ => string.Empty,
        };
        return decal.Length > 0;
    }
}
