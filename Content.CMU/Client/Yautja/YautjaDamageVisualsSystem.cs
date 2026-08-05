using Content.Client.Damage;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Damage.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.Yautja;

public sealed partial class YautjaDamageVisualsSystem : EntitySystem
{
    [Dependency] private DamageVisualsSystem _damageVisuals = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private const string YautjaBloodColor = "#2cf274";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaComponent, ComponentStartup>(OnYautjaStartup);
        SubscribeLocalEvent<DamageVisualsComponent, ComponentStartup>(OnDamageVisualsStartup);
    }

    private void OnYautjaStartup(Entity<YautjaComponent> ent, ref ComponentStartup args)
    {
        ApplyYautjaBloodColor(ent.Owner);
    }

    private void OnDamageVisualsStartup(Entity<DamageVisualsComponent> ent, ref ComponentStartup args)
    {
        if (!HasComp<YautjaComponent>(ent.Owner))
            return;

        ApplyYautjaBloodColor(ent.Owner);
    }

    private void ApplyYautjaBloodColor(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) ||
            !TryComp<DamageVisualsComponent>(uid, out var damageVisuals))
        {
            return;
        }

        _damageVisuals.ChangeDamageGroupColor(sprite, damageVisuals, BruteGroup, YautjaBloodColor);
    }
}
