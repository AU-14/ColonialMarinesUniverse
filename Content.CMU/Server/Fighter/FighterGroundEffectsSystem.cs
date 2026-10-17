using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterGroundEffectsSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FighterAudioSystem _audio = default!;

    public override void Initialize() => SubscribeLocalEvent<FighterPayloadVisualComponent, DropshipWeaponImpactEvent>(OnImpact);

    private void OnImpact(Entity<FighterPayloadVisualComponent> payload, ref DropshipWeaponImpactEvent args)
    {
        var visual = payload.Comp.Visual;
        if (!TryComp(visual, out FighterStrikeVisualComponent? strike) || TerminatingOrDeleted(visual)) return;
        var coordinates = _transform.ToCoordinates(args.Coordinates);
        var water = false;
        if (_turf.TryGetTileRef(coordinates, out var tile))
        {
            var id = _tiles[tile.Value.Tile.TypeId].ID;
            water = id.Contains("Water", StringComparison.OrdinalIgnoreCase) || id.Contains("River", StringComparison.OrdinalIgnoreCase);
        }
        FighterEffects.AddGroundImpact(strike, args.Coordinates.Position - _transform.GetWorldPosition(visual), _timing.CurTime, water);
        EnsureComp<TimedDespawnComponent>(visual).Lifetime = FighterEffects.GroundLifetime;
        Dirty(visual, strike);
        // A GAU burst has many volleys: keep individual impact reports readable
        // without stacking dozens of long audio sources on the same tile.
        if (_timing.CurTime < payload.Comp.NextImpactSound) return;
        payload.Comp.NextImpactSound = _timing.CurTime + TimeSpan.FromSeconds(strike.Kind == FighterWeaponKind.Gau ? .3 : .15);
        _audio.PlayGround(FighterAudioSystem.Impact(strike.Kind), coordinates,
            strike.Kind == FighterWeaponKind.Gau ? 28 : 50, strike.Kind == FighterWeaponKind.Gau ? -8 : -3);
    }
}
