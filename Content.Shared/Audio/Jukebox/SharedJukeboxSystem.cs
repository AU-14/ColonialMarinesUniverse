using Robust.Shared.Audio.Systems;
using Content.Shared.CMU14.EntityReferences;

namespace Content.Shared.Audio.Jukebox;

public abstract partial class SharedJukeboxSystem : EntitySystem
{
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected EntityReferenceSystem References = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JukeboxComponent, ReferencedEntityTerminatingEvent>(OnAudioTerminating);
    }

    private void OnAudioTerminating(Entity<JukeboxComponent> ent, ref ReferencedEntityTerminatingEvent args)
    {
        if (ent.Comp.AudioStream != args.Entity)
            return;

        ent.Comp.AudioStream = null;
        Dirty(ent);
    }

    /// <summary>
    /// Returns whether or not the given jukebox is currently playing a song.
    /// </summary>
    public bool IsPlaying(Entity<JukeboxComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return false;

        return entity.Comp.AudioStream is { } audio && Audio.IsPlaying(audio);
    }
}
