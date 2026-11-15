using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Heal;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Strain;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Xenomorphs;

public sealed class CMUXenoHealerHumanSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoComponent, XenoApplySalveActionEvent>(
            OnApplySalveToHuman,
            before: [typeof(SharedXenoHealSystem)]);
    }

    private void OnApplySalveToHuman(Entity<XenoComponent> healer, ref XenoApplySalveActionEvent args)
    {
        if (args.Handled ||
            !TryComp<XenoStrainComponent>(healer, out var strain) ||
            strain.Name != "rmc-xeno-healer-name" ||
            !HasComp<CMUHumanMedicalComponent>(args.Target) ||
            HasComp<SynthComponent>(args.Target) ||
            healer.Owner == args.Target ||
            !_interaction.InRangeUnobstructed(healer.Owner, args.Target, args.Range) ||
            _mobState.IsDead(args.Target) ||
            _flammable.IsOnFire(args.Target) ||
            !TryComp<DamageableComponent>(args.Target, out var targetDamageable))
        {
            return;
        }

        if (!_xenoPlasma.TryRemovePlasmaPopup(healer.Owner, args.StandardHealAmount * args.PlasmaCostModifier))
            return;

        var healing = _rmcDamageable.DistributeDamageCached(
            (args.Target, targetDamageable),
            BruteGroup,
            FixedPoint2.New(75));
        healing = _rmcDamageable.DistributeDamageCached(
            (args.Target, targetDamageable),
            BurnGroup,
            FixedPoint2.New(50),
            healing);

        var treatment = -healing;
        treatment.DamageDict[Poison] = FixedPoint2.New(5);
        _damageable.TryChangeDamage(
            args.Target,
            treatment,
            ignoreResistances: true,
            interruptsDoAfters: false,
            origin: healer.Owner);

        if (TryComp<DamageableComponent>(healer, out var healerDamageable))
        {
            var selfDamage = new DamageSpecifier
            {
                DamageDict = { [Blunt] = args.StandardHealAmount * args.DamageTakenModifier },
            };
            _damageable.AddDamage(healer.Owner, healerDamageable, selfDamage);
        }

        foreach (var (part, _) in _medicalIndex.GetBodyParts(args.Target))
        {
            if (TryComp<BodyPartWoundComponent>(part, out var wounds))
                _wounds.StopSurfaceBleedingOnPart(part, wounds);
        }

        args.Handled = true;

        if (_net.IsServer)
            SpawnAttachedTo(args.HealEffect, args.Target.ToCoordinates());

        _jitter.DoJitter(args.Target, TimeSpan.FromSeconds(1), true, 80, 8, true);
        _audio.PlayPredicted(args.HealSound, args.Target.ToCoordinates(), healer);
        _popup.PopupSelf(
            Loc.GetString("cmu-xeno-healer-human-heal-self", ("target", args.Target)),
            healer,
            PopupType.Medium);
        _popup.PopupSelf(
            Loc.GetString("cmu-xeno-healer-human-heal-target", ("healer", healer)),
            args.Target,
            PopupType.SmallCaution);
    }
}
