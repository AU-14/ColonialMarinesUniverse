using Content.Shared._RMC14.Humanoid;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid;

public sealed partial class HumanoidProfileSystem : EntitySystem
{
    [Dependency] private GrammarSystem _grammar = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, ExaminedEvent>(OnExamined);
    }

    public void ApplyProfileTo(Entity<HumanoidProfileComponent?> ent, HumanoidCharacterProfile profile)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Gender = profile.Gender;
        ent.Comp.Age = profile.Age;
        ent.Comp.Species = profile.Species;
        ent.Comp.Sex = profile.Sex;
        Dirty(ent);

        SetVoice(ent, profile.Voice);

        if (TryComp<GrammarComponent>(ent, out var grammar))
        {
            _grammar.SetGender((ent, grammar), profile.Gender);
        }
    }

    public void SetVoice(Entity<HumanoidProfileComponent?> ent, ProtoId<EmoteSoundsPrototype> voice)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Voice == voice)
            return;

        var oldVoice = ent.Comp.Voice;
        ent.Comp.Voice = voice;
        Dirty(ent);

        var voiceChanged = new VoiceChangedEvent(oldVoice, voice);
        RaiseLocalEvent(ent, ref voiceChanged);
    }

    public void SetGender(Entity<HumanoidProfileComponent?> ent, Gender gender)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Gender = gender;
        Dirty(ent);

        if (TryComp<GrammarComponent>(ent, out var grammar))
            _grammar.SetGender((ent, grammar), gender);
    }

    private void OnExamined(Entity<HumanoidProfileComponent> ent, ref ExaminedEvent args)
    {
        var identity = Identity.Entity(ent, EntityManager);
        var species = GetSpeciesRepresentation(ent.Comp.Species).ToLower();
        var age = GetAgeRepresentation(ent.Comp.Species, ent.Comp.Age);

        // RMC14 start
        if (TryComp<RMCHumanoidRepresentationOverrideComponent>(ent, out var representation))
        {
            if (representation.Species is { } speciesOverride)
                species = Loc.GetString(speciesOverride).ToLower();

            if (representation.Age is { } ageOverride)
                age = Loc.GetString(ageOverride).ToLower();
        }
        // RMC14 end

        // AU14 start
        var locale = "humanoid-appearance-component-examine";
        if (args.Examiner == args.Examined)
            locale += "-selfaware";

        args.PushText(Loc.GetString(locale, ("user", identity), ("age", age), ("species", species)), 100);
        // AU14 end
    }

    /// <summary>
    /// Takes ID of the species prototype, returns UI-friendly name of the species.
    /// </summary>
    public string GetSpeciesRepresentation(ProtoId<SpeciesPrototype> species)
    {
        if (ProtoMan.TryIndex(species, out var speciesPrototype))
            return Loc.GetString(speciesPrototype.Name);

        Log.Error("Tried to get representation of unknown species: {speciesId}");
        return Loc.GetString("humanoid-appearance-component-unknown-species");
    }

    /// <summary>
    /// Takes ID of the species prototype and an age, returns an approximate description
    /// </summary>
    public string GetAgeRepresentation(ProtoId<SpeciesPrototype> species, int age)
    {
        if (!ProtoMan.TryIndex(species, out var speciesPrototype))
        {
            Log.Error("Tried to get age representation of species that couldn't be indexed: " + species);
            return Loc.GetString("identity-age-young");
        }

        if (age < speciesPrototype.YoungAge)
        {
            return Loc.GetString("identity-age-young");
        }

        if (age < speciesPrototype.OldAge)
        {
            return Loc.GetString("identity-age-middle-aged");
        }

        return Loc.GetString("identity-age-old");
    }
}
