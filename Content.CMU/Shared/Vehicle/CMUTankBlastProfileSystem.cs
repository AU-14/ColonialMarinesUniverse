using Content.Shared.Explosion;

namespace Content.Shared._RMC14.Vehicle;

/// <summary>Applies <see cref="CMUTankBlastProfileComponent"/> to explosions hitting a tank.</summary>
public sealed class CMUTankBlastProfileSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUTankBlastProfileComponent, GetExplosionResistanceEvent>(OnGetExplosionResistance);
    }

    private void OnGetExplosionResistance(Entity<CMUTankBlastProfileComponent> ent, ref GetExplosionResistanceEvent args)
    {
        ent.Comp.PendingExplosion = args.ExplosionPrototype;
        args.DamageCoefficient *= MathF.Max(GetMultiplier(ent.Comp, args.ExplosionPrototype), 0f);
    }

    public static float GetMultiplier(CMUTankBlastProfileComponent profile, string explosion)
    {
        if (profile.ExtraMultipliers.TryGetValue(explosion, out var multiplier) ||
            profile.Multipliers.TryGetValue(explosion, out multiplier))
            return multiplier;

        return profile.DefaultMultiplier;
    }
}
