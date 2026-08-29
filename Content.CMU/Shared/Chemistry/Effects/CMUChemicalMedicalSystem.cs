using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects;

/// <summary>
/// Common typed-organ operations for generated medicines.
/// </summary>
public sealed class CMUChemicalMedicalSystem : EntitySystem
{
    [Dependency] private CMUMedicalBodyIndexSystem _index = default!;
    [Dependency] private SharedOrganHealthSystem _organs = default!;
    [Dependency] private SharedHeartSystem _heart = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganDamagedEvent>(OnOrganDamaged,
            before: new[] { typeof(SharedOrganHealthSystem) });
    }

    public bool HealOrgan<T>(EntityUid body, FixedPoint2 amount, bool restartHeart = false)
        where T : IComponent
    {
        if (amount <= FixedPoint2.Zero || !_index.TryGetOrgan<T>(body, out var organ))
            return false;
        if (!TryComp<OrganHealthComponent>(organ, out var health))
            return false;

        _organs.HealOrgan((organ, health), body, amount);
        if (restartHeart && health.Current > FixedPoint2.Zero && TryComp<HeartComponent>(organ, out var heart))
            _heart.TryRestartHeart((organ, heart));
        return true;
    }

    public bool DamageOrgan<T>(EntityUid body, FixedPoint2 amount, ProtoId<DamageTypePrototype> type,
        OrganDamageSource source = OrganDamageSource.Reagent)
        where T : IComponent
    {
        if (amount <= FixedPoint2.Zero || !_index.TryGetOrgan<T>(body, out var organ))
            return false;

        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        var ev = new OrganDamagedEvent(body, organ, damage, source);
        RaiseLocalEvent(organ, ref ev, broadcast: true);
        return true;
    }

    private void OnOrganDamaged(ref OrganDamagedEvent args)
    {
        if (!HasComp<CMUBrainComponent>(args.Organ))
            return;

        if (HasComp<ChemicalNeurocryogenicComponent>(args.Body) && args.Source != OrganDamageSource.Direct)
        {
            args.Damage *= 0f;
            return;
        }

        if (args.Source != OrganDamageSource.Reagent ||
            !TryComp<ChemicalNeuroshieldComponent>(args.Body, out var shield))
        {
            return;
        }

        args.Damage *= Math.Clamp(1f - shield.Protection, 0f, 1f);
    }
}
