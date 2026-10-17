using Content.Shared.CMU14.Fighter;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Spawners;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private TurfSystem _vtolTurf = default!;

    private void BeginVtolEffects(Entity<FighterGroundComponent> ground, Entity<FighterAircraftComponent> aircraft, bool landing)
    {
        FinishVtolEffects(ground, FighterVtolOutcome.Aborted);
        // Attach to the pad's grid, never to the hull that leaves the map on takeoff.
        var uid = SpawnAttachedTo("CMUFighterVtolWash", Transform(ground).Coordinates);
        // Nozzle sockets use north-forward; the vehicle chassis uses south-forward.
        _transform.SetWorldRotation(uid, _transform.GetWorldRotation(ground) + new Angle(Math.PI));
        var visual = Comp<FighterVtolVisualComponent>(uid);
        visual.Landing = landing;
        visual.StartedAt = ground.Comp.StartedAt;
        visual.EndsAt = ground.Comp.EndsAt;
        if (_vtolTurf.TryGetTileRef(Transform(ground).Coordinates, out var tile))
        {
            var id = _tiles[tile.Value.Tile.TypeId].ID;
            if (id.Contains("Water", StringComparison.OrdinalIgnoreCase) || id.Contains("River", StringComparison.OrdinalIgnoreCase))
                visual.Surface = FighterVtolSurface.Water;
            else if (id.Contains("Snow", StringComparison.OrdinalIgnoreCase) || id.Contains("Ice", StringComparison.OrdinalIgnoreCase))
                visual.Surface = FighterVtolSurface.Snow;
            else if (id.Contains("Metal", StringComparison.OrdinalIgnoreCase) || id.Contains("Steel", StringComparison.OrdinalIgnoreCase) ||
                     id.Contains("Vehicle", StringComparison.OrdinalIgnoreCase) || id.Contains("Plating", StringComparison.OrdinalIgnoreCase))
                visual.Surface = FighterVtolSurface.Deck;
        }
        Comp<TimedDespawnComponent>(uid).Lifetime = (float) (visual.EndsAt - visual.StartedAt).TotalSeconds + FighterVtolVisualComponent.ResidueSeconds;
        Dirty(uid, visual);
        ground.Comp.VtolVisual = uid;
        var sound = new SoundPathSpecifier(landing ? "/Audio/CMU14/Fighter/vtol-descent.wav" : "/Audio/CMU14/Fighter/vtol-takeoff.wav");
        ground.Comp.TransitionAudio = _fighterAudio.PlayGround(sound, Transform(uid).Coordinates, 55, -2);
        // Both crew now sit in the ground hull during these transitions and hear
        // the same exterior source. A second source on the empty cockpit map is redundant.
    }

    private void FinishVtolEffects(Entity<FighterGroundComponent> ground, FighterVtolOutcome outcome)
    {
        ground.Comp.TransitionAudio = _audio.Stop(ground.Comp.TransitionAudio);
        ground.Comp.CockpitTransitionAudio = _audio.Stop(ground.Comp.CockpitTransitionAudio);
        if (ground.Comp.VtolVisual is not { } uid || !TryComp(uid, out FighterVtolVisualComponent? visual) || TerminatingOrDeleted(uid)) return;
        ground.Comp.VtolVisual = null;
        if (visual.Outcome != FighterVtolOutcome.Active) return;
        visual.Outcome = outcome;
        visual.FinishedAt = _timing.CurTime;
        Comp<TimedDespawnComponent>(uid).Lifetime = FighterVtolVisualComponent.ResidueSeconds;
        Dirty(uid, visual);
        if (outcome != FighterVtolOutcome.Touchdown) return;
        ground.Comp.TouchdownAt = _timing.CurTime;
        Dirty(ground);
        var sound = new SoundPathSpecifier("/Audio/CMU14/Fighter/vtol-touchdown.wav");
        _fighterAudio.PlayGround(sound, Transform(uid).Coordinates, 35, -2);
    }
}
