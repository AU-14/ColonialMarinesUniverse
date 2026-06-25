using Content.Server.Humanoid.Systems;
using Content.Shared.AU14;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using TribalComponent = Content.Shared._CMU14.Threats.Mobs.Tribal.TribalComponent;

namespace Content.Server._CMU14.Threats.Mobs.Tribal;

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
    public static readonly Color TribalSkin = Color.FromHex("#4F7A82");
    public static readonly ProtoId<SpeciesPrototype> TribalSpecies = "Tribal";

    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TribalComponent, MapInitEvent>(OnMapInit,
            after: [typeof(RandomHumanoidAppearanceSystem)]);
    }

    private void OnMapInit(Entity<TribalComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out HumanoidAppearanceComponent? humanoid))
            return;

        _humanoid.SetSpecies(ent.Owner, TribalAppearanceSystem.TribalSpecies, false, humanoid);
        _humanoid.SetSkinColor(ent.Owner, TribalAppearanceSystem.TribalSkin, false, false, humanoid);
        Dirty(ent.Owner, humanoid);
    }
}