using Content.Shared.Humanoid;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Speech;

public sealed class CMUHumanSpeechSoundsSystem : EntitySystem
{
    private static readonly ProtoId<SpeechSoundsPrototype> Male = "CMUHumanMale";
    private static readonly ProtoId<SpeechSoundsPrototype> Female = "CMUHumanFemale";

    public override void Initialize()
    {
        SubscribeLocalEvent<SpeechComponent, VoiceChangedEvent>(OnVoiceChanged);
    }

    private void OnVoiceChanged(Entity<SpeechComponent> ent, ref VoiceChangedEvent args)
    {
        if (ent.Comp.SpeechSounds != Male && ent.Comp.SpeechSounds != Female)
            return;

        var sounds = TryComp<HumanoidProfileComponent>(ent, out var profile) && profile.Sex == Sex.Female
            ? Female
            : Male;

        if (ent.Comp.SpeechSounds == sounds)
            return;

        ent.Comp.SpeechSounds = sounds;
        Dirty(ent);
    }
}
