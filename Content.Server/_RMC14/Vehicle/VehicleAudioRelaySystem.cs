using System;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Vehicle;

public sealed partial class VehicleAudioRelaySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float MuffleVolumeDb = -8f;
    private const float MaxFalloffDb = -20f;

    // CMU14: Z-level audio owns AudioComponent's move subscription. Queue its sources
    // until the audio API has finished setting global/private audience restrictions.
    private readonly HashSet<EntityUid> _pending = new();
    private readonly List<EntityUid> _buffer = new();

    public void QueueSound(EntityUid uid) => _pending.Add(uid);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _buffer.Clear();
        _buffer.AddRange(_pending);
        _pending.Clear();
        foreach (var uid in _buffer)
        {
            if (!TerminatingOrDeleted(uid) && TryComp(uid, out AudioComponent? audio))
                RelaySound((uid, audio));
        }
    }

    // CMU14: share recipient selection with sounds admitted before allocation.
    private void RelaySound(Entity<AudioComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.Global || comp.IncludedEntities != null || comp.Params.Loop || comp.FileName.Length == 0)
            return;

        RelayCMUTransientSound(new ResolvedPathSpecifier(comp.FileName), comp.Params, ent, comp.ExcludedEntity);
    }

    // CMU14: preserve muffling and occupant selection for pre-admitted sounds.
    private void RelayToOccupants(VehicleInteriorComponent interior, ResolvedSoundSpecifier specifier,
        AudioParams sourceParams, float distance, float maxDistance, EntityUid? excluded)
    {
        var falloff = Math.Clamp(distance / maxDistance, 0f, 1f) * MaxFalloffDb;
        var audioParams = sourceParams.WithVolume(sourceParams.Volume + falloff + MuffleVolumeDb);

        foreach (var occupant in interior.Passengers)
        {
            RelayToOccupant(occupant, specifier, audioParams, excluded);
        }

        foreach (var occupant in interior.Xenos)
        {
            RelayToOccupant(occupant, specifier, audioParams, excluded);
        }
    }

    // CMU14: rejected relays must not allocate or release a different sound's budget.
    private void RelayToOccupant(EntityUid occupant, ResolvedSoundSpecifier specifier, AudioParams audioParams, EntityUid? excluded)
    {
        if (occupant == excluded || TerminatingOrDeleted(occupant) ||
            !TryComp(occupant, out ActorComponent? actor) ||
            IsListeningOutside(occupant))
        {
            return;
        }

        var filter = _cmuBudget.LimitAudience(specifier, Filter.SinglePlayer(actor.PlayerSession), audioParams);
        if (filter.Count == 0)
            return;
        if (_audio.PlayGlobal(specifier, actor.PlayerSession, audioParams) is { } sound)
            _cmuBudget.Track(sound.Component, filter);
    }

    private bool IsListeningOutside(EntityUid occupant)
    {
        return TryComp(occupant, out VehicleViewToggleComponent? toggle) && toggle.IsOutside;
    }
}
