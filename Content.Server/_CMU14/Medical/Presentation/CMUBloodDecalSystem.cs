using System.Numerics;
using Content.Server.Decals;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Presentation;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Decals;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Presentation;

public sealed partial class CMUBloodDecalSystem : EntitySystem
{
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const string HumanBloodReagent = "Blood";
    private const string SynthBloodReagent = "RMCSynthBlood";
    private const string YautjaBloodReagent = "CMUYautjaBlood";
    private const string XenoBloodReagent = "FluorosulfuricAcid";
    private const float FloorDecalVolume = 8f;
    private const float ExtraDecalVolume = 80f;
    private const int MaxBloodDecalsPerTile = 6;
    private static readonly Vector2 DecalOriginOffset = new(-0.5f, -0.5f);
    private static readonly TimeSpan MinBleedDecalInterval = TimeSpan.FromSeconds(0.35);
    private static readonly TimeSpan MaxBleedDecalInterval = TimeSpan.FromSeconds(1.25);
    private static readonly TimeSpan MinSynthDamageDecalInterval = TimeSpan.FromSeconds(0.9);
    private static readonly TimeSpan MaxSynthDamageDecalInterval = TimeSpan.FromSeconds(1.8);

    private static readonly Color HumanBloodColor = Color.FromHex("#980002");
    private static readonly Color SynthBloodColor = Color.FromHex("#EEEEEE");
    private static readonly Color XenoBloodColor = Color.FromHex("#bed700");
    private static readonly Color YautjaBloodColor = Color.FromHex("#81d434");

    private static readonly ProtoId<DecalPrototype>[] HumanFloorDecals =
    [
        "CMUCM13BloodFloor1",
        "CMUCM13BloodFloor2",
        "CMUCM13BloodFloor3",
        "CMUCM13BloodFloor4",
        "CMUCM13BloodFloor5",
        "CMUCM13BloodFloor6",
        "CMUCM13BloodFloor7",
    ];

    private static readonly ProtoId<DecalPrototype>[] HumanSplatterDecals =
    [
        "CMUCM13BloodSplatter1",
        "CMUCM13BloodSplatter2",
        "CMUCM13BloodSplatter3",
        "CMUCM13BloodSplatter4",
        "CMUCM13BloodSplatter5",
    ];

    private EntityQuery<DecalGridComponent> _decalGridQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private EntityQuery<BloodstreamComponent> _bloodstreamQuery;
    private readonly Dictionary<(EntityUid GridUid, uint DecalId), BloodStainData> _bloodStains = new();
    private readonly HashSet<(EntityUid GridUid, uint DecalId)> _consumedFootprintStains = new();
    private readonly List<(EntityUid GridUid, uint DecalId)> _stainCleanupBuffer = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextBleedDecal = new();

    public override void Initialize()
    {
        base.Initialize();

        _decalGridQuery = GetEntityQuery<DecalGridComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
        _bloodstreamQuery = GetEntityQuery<BloodstreamComponent>();

        SubscribeLocalEvent<HumanBleedingTickEvent>(OnHumanBleedingTick);
        SubscribeLocalEvent<CMUBloodSpillAttemptEvent>(OnBloodSpillAttempt);
        SubscribeLocalEvent<CMUBloodPuddleAttemptEvent>(OnBloodPuddleAttempt);
        SubscribeLocalEvent<DecalGridComponent, ComponentShutdown>(OnDecalGridShutdown);
    }

    public bool TryConsumeFootprintStain(EntityUid gridUid, Vector2i tile, out string reagent, out Color color)
    {
        reagent = HumanBloodReagent;
        color = HumanBloodColor;

        if (!_decalGridQuery.TryComp(gridUid, out var decalGrid))
            return false;

        var min = new Vector2(tile.X, tile.Y);
        var bounds = new Box2(min, min + Vector2.One);
        var decals = _decals.GetDecalsIntersecting(gridUid, bounds, decalGrid);

        foreach (var (decalId, decal) in decals)
        {
            var key = (gridUid, decalId);
            if (_consumedFootprintStains.Contains(key))
                continue;

            if (_bloodStains.TryGetValue(key, out var stain))
            {
                reagent = stain.Reagent;
                color = stain.Color;
                _consumedFootprintStains.Add(key);
                return true;
            }

            if (!TryGetStainFromDecal(decal, out reagent, out color))
                continue;

            _consumedFootprintStains.Add(key);
            return true;
        }

        return false;
    }

    private void OnDecalGridShutdown(Entity<DecalGridComponent> ent, ref ComponentShutdown args)
    {
        _stainCleanupBuffer.Clear();
        foreach (var key in _bloodStains.Keys)
        {
            if (key.GridUid == ent.Owner)
                _stainCleanupBuffer.Add(key);
        }

        foreach (var key in _stainCleanupBuffer)
        {
            _bloodStains.Remove(key);
        }

        _stainCleanupBuffer.Clear();
        _consumedFootprintStains.RemoveWhere(x => x.GridUid == ent.Owner);
    }

    private void OnHumanBleedingTick(ref HumanBleedingTickEvent args)
    {
        if (args.TotalRate <= 0 ||
            args.ActiveSources <= 0 ||
            !CanSpawnBleedDecal(args.Body, args.TotalRate.Float()))
        {
            return;
        }

        var reagent = HumanBloodReagent;
        var color = HumanBloodColor;
        if (_bloodstreamQuery.TryComp(args.Body, out var bloodstream) &&
            TryGetBloodColor(bloodstream.BloodReagent.Id, out var bloodColor))
        {
            reagent = bloodstream.BloodReagent.Id;
            color = bloodColor;
        }

        TryAddBloodDecals(args.Body, reagent, color, floor: args.TotalRate.Float() >= FloorDecalVolume, count: 1);
    }

    private void OnBloodSpillAttempt(ref CMUBloodSpillAttemptEvent args)
    {
        if (args.Handled ||
            args.Solution.Volume <= 0)
        {
            return;
        }

        args.Handled = true;

        var reagent = HumanBloodReagent;
        var color = HumanBloodColor;
        TryGetBloodStain(args.Solution, out reagent, out color);

        if (!_transformQuery.TryComp(args.Body, out var xform) ||
            xform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return;
        }

        var volume = args.Solution.Volume.Float();
        var floor = args.FullSpill || volume >= FloorDecalVolume;
        var count = args.FullSpill
            ? Math.Clamp((int)MathF.Ceiling(volume / ExtraDecalVolume), 1, 4)
            : 1;

        TryAddBloodDecals(args.Body, reagent, color, floor, count);
    }

    private void OnBloodPuddleAttempt(ref CMUBloodPuddleAttemptEvent args)
    {
        if (args.Handled ||
            args.Solution.Volume <= 0 ||
            !TryGetBloodStain(args.Solution, out var reagent, out var color))
        {
            return;
        }

        args.Handled = true;

        var volume = args.Solution.Volume.Float();
        var floor = volume >= FloorDecalVolume;
        var count = Math.Clamp((int)MathF.Ceiling(volume / ExtraDecalVolume), 1, 4);
        TryAddBloodDecals(args.Tile, reagent, color, floor, count);
    }

    public void TryAddSynthDamageBlood(EntityUid body, DamageSpecifier damage)
    {
        var positive = GetPositiveDamage(damage);
        if (positive <= 0f ||
            !CanSpawnBleedDecal(
                body,
                MathF.Max(positive / 10f, 1f),
                MinSynthDamageDecalInterval,
                MaxSynthDamageDecalInterval))
        {
            return;
        }

        var floor = positive >= FloorDecalVolume;
        TryAddBloodDecals(body, SynthBloodReagent, SynthBloodColor, floor, count: 1);
    }

    private bool CanSpawnBleedDecal(EntityUid body, float bleedRate)
    {
        return CanSpawnBleedDecal(
            body,
            bleedRate,
            MinBleedDecalInterval,
            MaxBleedDecalInterval);
    }

    private bool CanSpawnBleedDecal(
        EntityUid body,
        float bleedRate,
        TimeSpan minInterval,
        TimeSpan maxInterval)
    {
        var now = _timing.CurTime;
        if (_nextBleedDecal.TryGetValue(body, out var next) &&
            now < next)
        {
            return false;
        }

        var intervalSeconds = Math.Clamp(
            1.25f / MathF.Max(bleedRate, 1f),
            (float) minInterval.TotalSeconds,
            (float) maxInterval.TotalSeconds);
        _nextBleedDecal[body] = now + TimeSpan.FromSeconds(intervalSeconds);
        return true;
    }

    private bool TryAddBloodDecals(EntityUid body, string reagent, Color color, bool floor, int count)
    {
        if (!_transformQuery.TryComp(body, out var xform) ||
            xform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return false;
        }

        var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        return TryAddBloodDecals(gridUid, grid, tile, reagent, color, floor, count);
    }

    private bool TryAddBloodDecals(TileRef tileRef, string reagent, Color color, bool floor, int count)
    {
        if (!_gridQuery.TryComp(tileRef.GridUid, out var grid))
            return false;

        return TryAddBloodDecals(
            tileRef.GridUid,
            grid,
            tileRef.GridIndices,
            reagent,
            color,
            floor,
            count);
    }

    private bool TryAddBloodDecals(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        string reagent,
        Color color,
        bool floor,
        int count)
    {
        if (_decalGridQuery.TryComp(gridUid, out var decalGrid) &&
            CountBloodDecalsInTile(gridUid, tile, decalGrid) >= MaxBloodDecalsPerTile)
        {
            return true;
        }

        var decals = GetDecals(floor);
        if (decals.Length == 0)
            return false;

        for (var i = 0; i < count; i++)
        {
            var coords = _map.GridTileToLocal(gridUid, grid, tile)
                .Offset(DecalOriginOffset)
                .Offset(_random.NextVector2(-0.22f, 0.22f));

            if (!_decals.TryAddDecal(
                _random.Pick(decals),
                coords,
                out var decalId,
                color,
                _random.NextAngle(),
                cleanable: true))
            {
                continue;
            }

            _bloodStains[(gridUid, decalId)] = new BloodStainData(reagent, color);
        }

        return true;
    }

    private static ProtoId<DecalPrototype>[] GetDecals(bool floor)
    {
        return floor ? HumanFloorDecals : HumanSplatterDecals;
    }

    private static bool TryGetBloodStain(Solution solution, out string reagent, out Color color)
    {
        reagent = HumanBloodReagent;
        color = HumanBloodColor;

        foreach (var content in solution.Contents)
        {
            switch (content.Reagent.Prototype)
            {
                case HumanBloodReagent:
                    reagent = HumanBloodReagent;
                    color = HumanBloodColor;
                    return true;
                case XenoBloodReagent:
                    reagent = XenoBloodReagent;
                    color = XenoBloodColor;
                    return true;
                case SynthBloodReagent:
                    reagent = SynthBloodReagent;
                    color = SynthBloodColor;
                    return true;
                case YautjaBloodReagent:
                    reagent = YautjaBloodReagent;
                    color = YautjaBloodColor;
                    return true;
            }
        }

        return false;
    }

    private static bool TryGetBloodColor(string reagent, out Color color)
    {
        color = reagent switch
        {
            HumanBloodReagent => HumanBloodColor,
            XenoBloodReagent => XenoBloodColor,
            SynthBloodReagent => SynthBloodColor,
            YautjaBloodReagent => YautjaBloodColor,
            _ => HumanBloodColor,
        };

        return reagent is HumanBloodReagent or XenoBloodReagent or SynthBloodReagent or YautjaBloodReagent;
    }

    private static float GetPositiveDamage(DamageSpecifier damage)
    {
        var total = 0f;
        foreach (var value in damage.DamageDict.Values)
        {
            if (value > 0)
                total += value.Float();
        }

        return total;
    }

    private int CountBloodDecalsInTile(EntityUid gridUid, Vector2i tile, DecalGridComponent decalGrid)
    {
        var min = new Vector2(tile.X, tile.Y);
        var bounds = new Box2(min, min + Vector2.One);
        var decals = _decals.GetDecalsIntersecting(gridUid, bounds, decalGrid);
        var count = 0;

        foreach (var (_, decal) in decals)
        {
            if (!IsBloodDecal(decal.Id))
                continue;

            count++;
        }

        return count;
    }

    private static bool IsBloodDecal(string id)
    {
        return id.StartsWith("RMCDecalBlood", StringComparison.Ordinal) ||
               id.StartsWith("CMUCM13Blood", StringComparison.Ordinal) ||
               id.StartsWith("CMUCM13XenoBlood", StringComparison.Ordinal);
    }

    private static bool TryGetStainFromDecal(Decal decal, out string reagent, out Color color)
    {
        reagent = HumanBloodReagent;
        color = HumanBloodColor;

        if (decal.Id.StartsWith("RMCDecalBloodXenonid", StringComparison.Ordinal))
        {
            reagent = XenoBloodReagent;
            color = XenoBloodColor;
            return true;
        }

        if (decal.Id.StartsWith("CMUCM13XenoBlood", StringComparison.Ordinal))
        {
            reagent = XenoBloodReagent;
            color = decal.Color ?? XenoBloodColor;
            return true;
        }

        if (!IsBloodDecal(decal.Id))
            return false;

        if (decal.Id.StartsWith("CMUCM13Blood", StringComparison.Ordinal) &&
            (decal.Color is not { } cm13Tint || IsCloseColor(cm13Tint, Color.White)))
        {
            reagent = HumanBloodReagent;
            color = HumanBloodColor;
            return true;
        }

        if (decal.Id.StartsWith("RMCDecalBlood", StringComparison.Ordinal) &&
            (decal.Color is not { } rmcTint || IsCloseColor(rmcTint, Color.White)))
        {
            reagent = HumanBloodReagent;
            color = HumanBloodColor;
            return true;
        }

        if (decal.Color is not { } tint)
            return true;

        color = tint;
        if (IsCloseColor(tint, SynthBloodColor))
        {
            reagent = SynthBloodReagent;
            return true;
        }

        if (IsCloseColor(tint, YautjaBloodColor))
        {
            reagent = YautjaBloodReagent;
            return true;
        }

        if (IsCloseColor(tint, XenoBloodColor))
        {
            reagent = XenoBloodReagent;
            return true;
        }

        return true;
    }

    private static bool IsCloseColor(Color a, Color b)
    {
        const float tolerance = 0.08f;
        return MathF.Abs(a.R - b.R) <= tolerance &&
               MathF.Abs(a.G - b.G) <= tolerance &&
               MathF.Abs(a.B - b.B) <= tolerance;
    }

    private readonly record struct BloodStainData(string Reagent, Color Color);
}
