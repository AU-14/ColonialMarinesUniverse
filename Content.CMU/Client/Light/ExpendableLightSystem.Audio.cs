using System.Numerics;
using Content.Shared.Audio;
using Content.Shared.Light.Components;
using Robust.Shared.Map;

namespace Content.Client.Light.EntitySystems;

public sealed partial class ExpendableLightSystem
{
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;

    private readonly Dictionary<EntityUid, Entity<AmbientSoundComponent>> _burnAmbience = new();

    private void UpdateBurnAmbience(Entity<ExpendableLightComponent> light, ExpendableLightState state)
    {
        if (state is not (ExpendableLightState.Lit or ExpendableLightState.Fading) || light.Comp.LoopedSound is not { } sound)
        {
            RemoveBurnAmbience(light);
            return;
        }

        // Use the existing distance and concurrency budgets. A separate child keeps
        // ownership local and does not replace any ambience supplied by the prototype.
        if (!_burnAmbience.TryGetValue(light, out var emitter) || TerminatingOrDeleted(emitter))
        {
            var uid = Spawn(null, new EntityCoordinates(light, Vector2.Zero));
            emitter = (uid, AddComp<AmbientSoundComponent>(uid));
            _burnAmbience[light] = emitter;
        }

        _ambient.SetSound(emitter, sound, emitter.Comp);
        _ambient.SetRange(emitter, sound.Params.MaxDistance, emitter.Comp);
        _ambient.SetVolume(emitter, sound.Params.Volume, emitter.Comp);
    }

    private void RemoveBurnAmbience(EntityUid light)
    {
        if (_burnAmbience.Remove(light, out var emitter))
            QueueDel(emitter);
    }
}
