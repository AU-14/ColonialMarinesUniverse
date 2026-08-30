using Content.Server.Humanoid;
using Content.Server.Humanoid.Systems;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using TribalComponent = Content.Shared.CMU14.Threats.Mobs.Tribal.TribalComponent;

namespace Content.Server.CMU14.Threats.Mobs.Tribal;

/// <summary>
///     Forces every tribal humanoid to the "Tribal" (Na'vi) species id and a
///     gray / dark-cyan skin tone on map-init, overriding the random profile
///     roll. Gear is left to the standard GhostRoleApplySpecial pipeline
///     (jobs + startingGear), matching the cultist / WYHT third-party flow.
///     Subscribes "after" the random humanoid system so it overwrites the
///     random species / skin pick.
/// </summary>
public sealed partial class TribalAppearanceSystem : EntitySystem
{
    [Dependency] private HumanoidOrganAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private SharedHideableHumanoidLayersSystem _hideableLayers = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    public static readonly Color TribalSkin = Color.FromHex("#4F7A82");
    public static readonly ProtoId<SpeciesPrototype> TribalSpecies = "Tribal";

    public override void Initialize()
    {
        SubscribeLocalEvent<TribalComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TribalComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TribalComponent, MapInitEvent>(OnMapInit,
            after: [typeof(RandomHumanoidAppearanceSystem)]);
    }

    private void OnStartup(Entity<TribalComponent> ent, ref ComponentStartup args)
    {
        // Prototype components start before RandomHumanoidAppearance's MapInit handler. Dynamic
        // additions to an already initialized entity need the invariant immediately, though.
        if (LifeStage(ent.Owner) >= EntityLifeStage.MapInitialized)
            ApplyTribalAppearance(ent.Owner);
    }

    private void OnShutdown(Entity<TribalComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        if (HasComp<HideableHumanoidLayersComponent>(ent.Owner))
        {
            _hideableLayers.SetPermanentLayerOcclusion(ent.Owner, HumanoidVisualLayers.UndergarmentTop, hidden: false);
            _hideableLayers.SetPermanentLayerOcclusion(ent.Owner, HumanoidVisualLayers.UndergarmentBottom, hidden: false);
        }

        if (!TryComp(ent, out HumanoidProfileComponent? humanoid) ||
            !_humanoidAppearance.TryGetAppearance(ent.Owner, out var skinColor, out var eyeColor, out var markings))
        {
            return;
        }

        // Removing the marker restores a valid visible appearance for the current species.
        var appearance = HumanoidCharacterAppearance.EnsureValid(
            new HumanoidCharacterAppearance(eyeColor, skinColor, markings),
            humanoid.Species,
            humanoid.Sex);
        var profile = CreateProfile(humanoid, humanoid.Species, appearance);
        _visualBody.ApplyProfileTo(ent.Owner, profile);
    }

    private void OnMapInit(Entity<TribalComponent> ent, ref MapInitEvent args)
    {
        ApplyTribalAppearance(ent.Owner);
    }

    private void ApplyTribalAppearance(EntityUid uid)
    {
        if (!TryComp(uid, out HumanoidProfileComponent? humanoid) ||
            !_humanoidAppearance.TryGetAppearance(uid, out _, out var eyeColor, out var markings))
        {
            return;
        }

        foreach (var organMarkings in markings.Values)
        {
            // Empty lists are intentional: omitted layers mean "leave unchanged" to VisualBody.
            organMarkings[HumanoidVisualLayers.UndergarmentTop] = [];
            organMarkings[HumanoidVisualLayers.UndergarmentBottom] = [];
        }

        var appearance = new HumanoidCharacterAppearance(eyeColor, TribalSkin, markings);
        var profile = CreateProfile(humanoid, TribalSpecies, appearance);

        // Do not call EnsureValid here: the shared Human organ group requires underwear and
        // validation would restore the two layers we deliberately cleared above.
        _visualBody.ApplyProfileTo(uid, profile);
        _humanoidProfile.ApplyProfileTo(uid, profile);

        // This remains authoritative if a later validator or profile application restores Human's
        // required underwear markings. ComponentShutdown clears it for live marker removal; a
        // polymorph/revert discards the temporary entity and its occlusion state together.
        if (HasComp<HideableHumanoidLayersComponent>(uid))
        {
            _hideableLayers.SetPermanentLayerOcclusion(uid, HumanoidVisualLayers.UndergarmentTop, hidden: true);
            _hideableLayers.SetPermanentLayerOcclusion(uid, HumanoidVisualLayers.UndergarmentBottom, hidden: true);
        }
    }

    private static HumanoidCharacterProfile CreateProfile(
        HumanoidProfileComponent humanoid,
        ProtoId<SpeciesPrototype> species,
        HumanoidCharacterAppearance appearance)
    {
        return HumanoidCharacterProfile.DefaultWithSpecies(species)
            .WithAge(humanoid.Age)
            .WithSex(humanoid.Sex)
            .WithGender(humanoid.Gender)
            .WithVoice(humanoid.Voice)
            .WithCharacterAppearance(appearance);
    }
}
