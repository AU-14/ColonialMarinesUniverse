using Content.Server.Humanoid;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.ColonyBounty;

/// <summary>
/// Builds witness-style physical descriptions of colonists for wanted records, using the same
/// named colors as the detailed-examine window so marshals can compare them 1:1. Fields are
/// deliberately fuzzy: some are swapped for another colonist's, because witness statements
/// are never a dossier.
/// </summary>
public sealed partial class SuspectDescriptionSystem : EntitySystem
{
    [Dependency] private readonly HumanoidOrganAppearanceSystem _appearance = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private const float MisrememberChance = 0.33f;

    public string Describe(EntityUid subject, EntityUid? witness)
    {
        var fields = new List<(EntityUid Source, Func<EntityUid, string?> Part)>
        {
            (subject, SexPart),
            (subject, AgePart),
            (subject, BuildPart),
            (subject, HairPart),
            (subject, EyePart),
            (subject, SkinPart),
        };

        if (witness is { } w)
        {
            var swapped = false;
            for (var i = 0; i < fields.Count; i++)
            {
                if (!_random.Prob(MisrememberChance))
                    continue;

                fields[i] = (w, fields[i].Part);
                swapped = true;
            }

            // A witness that remembers everything perfectly is no witness at all
            if (!swapped)
            {
                var i = _random.Next(fields.Count);
                fields[i] = (w, fields[i].Part);
            }
        }

        var parts = new List<string>();
        foreach (var (source, part) in fields)
        {
            if (part(source) is { } text)
                parts.Add(text);
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// A random profile-bearing colonist to misremember details from, ideally from the same station.
    /// </summary>
    public EntityUid? RandomWitness(EntityUid subject, EntityUid? station)
    {
        var pool = new List<EntityUid>();
        var enumerator = EntityManager.AllEntityQueryEnumerator<HumanoidProfileComponent>();
        while (enumerator.MoveNext(out var colonist, out _))
        {
            if (colonist == subject)
                continue;

            if (station != null && _station.GetOwningStation(colonist) != station)
                continue;

            pool.Add(colonist);
        }

        return pool.Count == 0 ? null : _random.Pick(pool);
    }

    private string? SexPart(EntityUid uid)
        => CompOrNull<HumanoidProfileComponent>(uid)?.Sex switch
        {
            Sex.Male => Loc.GetString("suspect-description-male"),
            Sex.Female => Loc.GetString("suspect-description-female"),
            _ => null,
        };

    private string? AgePart(EntityUid uid)
        => TryComp<CharacterDescriptionComponent>(uid, out var desc)
            ? Loc.GetString(desc.Age switch
            {
                < 30 => "suspect-description-young",
                < 50 => "suspect-description-middle-aged",
                _ => "suspect-description-elder",
            })
            : null;

    private string? BuildPart(EntityUid uid)
        => TryComp<CharacterDescriptionComponent>(uid, out var desc)
            ? Loc.GetString($"build-type-{desc.Build.ToString().ToLowerInvariant()}") + " "
              + Loc.GetString("suspect-description-build")
            : null;

    private string? HairPart(EntityUid uid)
    {
        if (!_appearance.TryGetMarkings(uid, HumanoidVisualLayers.Hair, out _, out _, out var markings)
            || markings.Count == 0
            || markings[0].MarkingColors.Count == 0)
            return null;

        return Loc.GetString("suspect-description-hair",
            ("color", NamedColorHelper.NearestColorName(markings[0].MarkingColors[0])));
    }

    private string? EyePart(EntityUid uid)
        => _appearance.TryGetColors(uid, out _, out var eye)
            ? Loc.GetString("suspect-description-eyes",
                ("color", NamedColorHelper.NearestColorName(eye)))
            : null;

    private string? SkinPart(EntityUid uid)
        => _appearance.TryGetColors(uid, out var skin, out _)
            ? Loc.GetString("suspect-description-skin",
                ("color", NamedColorHelper.NearestColorName(skin)))
            : null;
}
