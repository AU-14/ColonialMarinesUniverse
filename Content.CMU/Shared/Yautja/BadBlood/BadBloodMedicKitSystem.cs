using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ECS-702 / ECS-712 (D17), the predicted half of the Medic:
/// <list type="bullet">
/// <item>the field kit: a Medic using a <see cref="BadBloodMedicKitComponent"/> splint or tourniquet starts the CMU
/// item's own do-after at <see cref="BadBloodFieldMendComponent.KitDelayMultiplier"/> of its time, so the CMU system
/// still finishes the treatment. Anyone else falls through to the CMU handler at the normal time;</item>
/// <item>second wind: faster and unable to attack while it lasts (expiry is the server's, BadBloodMedicSystem).</item>
/// </list>
/// </summary>
public sealed class BadBloodMedicKitSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedCMUSplintItemSystem _splints = default!;
    [Dependency] private SharedCMUTourniquetSystem _tourniquets = default!;

    public override void Initialize()
    {
        // the CMU item handlers claim the interaction, so a Medic's quicker do-after has to start first
        SubscribeLocalEvent<BadBloodMedicKitComponent, AfterInteractEvent>(OnKitAfterInteract,
            before: [typeof(SharedCMUSplintItemSystem), typeof(SharedCMUTourniquetSystem)]);

        SubscribeLocalEvent<BadBloodSecondWindComponent, ComponentStartup>(OnSecondWindStartup);
        SubscribeLocalEvent<BadBloodSecondWindComponent, ComponentShutdown>(OnSecondWindShutdown);
        SubscribeLocalEvent<BadBloodSecondWindComponent, RefreshMovementSpeedModifiersEvent>(OnSecondWindRefreshSpeed);
        SubscribeLocalEvent<BadBloodSecondWindComponent, AttackAttemptEvent>(OnSecondWindAttackAttempt);
    }

    private void OnKitAfterInteract(Entity<BadBloodMedicKitComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { } target ||
            !TryComp(args.User, out BadBloodFieldMendComponent? medic) ||
            !HasComp<CMUHumanMedicalComponent>(target) ||
            HasComp<SynthComponent>(target))
        {
            return;
        }

        var multiplier = Math.Clamp(medic.KitDelayMultiplier, 0f, 1f);
        if (TryComp(ent, out CMUSplintItemComponent? splint))
        {
            if (!_splints.TryFindFracturedPart(target, out var part, args.User))
                return;

            var splintEv = new CMUSplintApplyDoAfterEvent { PreSelectedPart = GetNetEntity(part) };
            var splintDoAfter = new DoAfterArgs(EntityManager, args.User, splint.ApplyDelay * multiplier,
                splintEv, ent.Owner, target: target, used: ent.Owner)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                BlockDuplicate = true,
                NeedHand = true,
                BreakOnHandChange = true,
                BreakOnDropItem = true,
            };
            _doAfter.TryStartDoAfter(splintDoAfter);
            args.Handled = true;
            return;
        }

        if (!TryComp(ent, out CMUTourniquetItemComponent? tourniquet))
            return;

        // only the plain limb case. a stump, a limb already wearing one, or nothing to clamp is left to the CMU
        // handler, which knows those cases
        if (!_tourniquets.TryFindTourniquetTargetPart(args.User, target, out var limb, out var alreadyOn) || alreadyOn)
            return;

        var tourniquetEv = new CMUTourniquetApplyDoAfterEvent { PreSelectedPart = GetNetEntity(limb) };
        var tourniquetDoAfter = new DoAfterArgs(EntityManager, args.User, tourniquet.ApplyDelay * multiplier,
            tourniquetEv, ent.Owner, target: target, used: ent.Owner)
        {
            BlockDuplicate = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        };

        if (_doAfter.TryStartDoAfter(tourniquetDoAfter))
            _popup.PopupBroadcast(Loc.GetString("cmu-medical-tourniquet-applying"), target, args.User);

        args.Handled = true;
    }

    private void OnSecondWindStartup(Entity<BadBloodSecondWindComponent> ent, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnSecondWindShutdown(Entity<BadBloodSecondWindComponent> ent, ref ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnSecondWindRefreshSpeed(Entity<BadBloodSecondWindComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // shutdown refreshes too, and a stopping component no longer counts
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }

    private void OnSecondWindAttackAttempt(Entity<BadBloodSecondWindComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancel();
    }
}
