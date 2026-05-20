using Content.Shared.AU14;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Tribals;

/// <summary>
/// Forces every tribal humanoid to the "Tribal" (Na'vi) species id and a
/// grey / dark-cyan skin tone on map-init, overriding the random profile
/// roll. Gear is left to the standard GhostRoleApplySpecial pipeline
/// (jobs + startingGear), matching the cultist / WYHT third-party flow.
/// Subscribes "after" the random humanoid system so it overwrites the
/// random species / skin pick.
/// </summary>
public sealed partial class TribalAppearanceSystem : EntitySystem
{
    public static readonly Color TribalSkin = Color.FromHex("#4F7A82");
    public static readonly ProtoId<SpeciesPrototype> TribalSpecies = "Tribal";

    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TribalComponent, MapInitEvent>(OnMapInit, after: new[] { typeof(Content.Server.Humanoid.Systems.RandomHumanoidAppearanceSystem) });
    }

    private void OnMapInit(Entity<TribalComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        _humanoid.SetSpecies(ent.Owner, TribalSpecies, sync: false, humanoid);
        _humanoid.SetSkinColor(ent.Owner, TribalSkin, sync: false, verify: false, humanoid);
        Dirty(ent.Owner, humanoid);
    }
}
