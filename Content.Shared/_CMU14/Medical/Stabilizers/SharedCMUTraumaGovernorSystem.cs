using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Brain;
using Content.Shared._CMU14.Medical.Organs.Ears;
using Content.Shared._CMU14.Medical.Organs.Eyes;
using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared._CMU14.Medical.Organs.Kidneys;
using Content.Shared._CMU14.Medical.Organs.Liver;
using Content.Shared._CMU14.Medical.Organs.Lungs;
using Content.Shared._CMU14.Medical.Organs.Stomach;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Actions;
using Content.Shared.Body.Systems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Stabilizers;

public sealed partial class SharedCMUTraumaGovernorSystem : EntitySystem
{
    private const float StabilizedPainMultiplier = 0.35f;

    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUTraumaGovernorAttachmentComponent, EntGotInsertedIntoContainerMessage>(OnAccessoryInserted);
        SubscribeLocalEvent<CMUTraumaGovernorAttachmentComponent, EntGotRemovedFromContainerMessage>(OnAccessoryRemoved);
        SubscribeLocalEvent<CMUTraumaGovernorComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<CMUTraumaGovernorComponent, CMUTraumaGovernorActionEvent>(OnAction);
        SubscribeLocalEvent<CMUTraumaGovernorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUTraumaGovernorComponent, ComponentRemove>(OnGovernorRemoved);

        Subs.BuiEvents<CMUHumanMedicalComponent>(CMUTraumaGovernorUI.Key, subs =>
        {
            subs.Event<CMUTraumaGovernorChooseOrganBuiMsg>(OnOrganChosen);
        });
    }

    private void OnAccessoryInserted(Entity<CMUTraumaGovernorAttachmentComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (_net.IsClient)
            return;

        var holderUid = args.Container.Owner;
        if (!TryComp<UniformAccessoryHolderComponent>(holderUid, out var holder) ||
            args.Container.ID != holder.ContainerId)
        {
            return;
        }

        var governor = EnsureComp<CMUTraumaGovernorComponent>(holderUid);
        Dirty(holderUid, governor);
        GrantActionIfWorn((holderUid, governor));
    }

    private void OnAccessoryRemoved(Entity<CMUTraumaGovernorAttachmentComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (_net.IsClient)
            return;

        var holderUid = args.Container.Owner;
        if (!TryComp<UniformAccessoryHolderComponent>(holderUid, out var holder) ||
            args.Container.ID != holder.ContainerId)
        {
            return;
        }

        if (!HasTraumaGovernorAttachment((holderUid, holder), ent.Owner))
            RemCompDeferred<CMUTraumaGovernorComponent>(holderUid);
    }

    private void GrantActionIfWorn(Entity<CMUTraumaGovernorComponent> governor)
    {
        if (!TryGetWearer(governor.Owner, out var wearer))
            return;

        if (!_actionContainer.EnsureAction(governor.Owner, ref governor.Comp.Action, governor.Comp.ActionId))
            return;

        _actions.GrantActions((wearer, null), new[] { governor.Comp.Action.Value }, (governor.Owner, null));
        Dirty(governor);
    }

    private bool TryGetWearer(EntityUid armor, out EntityUid wearer)
    {
        wearer = default;
        if (!_containers.TryGetContainingContainer((armor, null, null), out var container))
            return false;
        if (!_inventory.TryGetSlot(container.Owner, container.ID, out var slot))
            return false;
        if ((slot.SlotFlags & SlotFlags.OUTERCLOTHING) == 0)
            return false;

        wearer = container.Owner;
        return true;
    }

    private bool HasTraumaGovernorAttachment(Entity<UniformAccessoryHolderComponent> holder, EntityUid ignored)
    {
        if (!_containers.TryGetContainer(holder, holder.Comp.ContainerId, out var container))
            return false;

        foreach (var contained in container.ContainedEntities)
        {
            if (contained == ignored)
                continue;
            if (HasComp<CMUTraumaGovernorAttachmentComponent>(contained))
                return true;
        }

        return false;
    }

    private void OnGetItemActions(Entity<CMUTraumaGovernorComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands || args.SlotFlags is null || (args.SlotFlags.Value & SlotFlags.OUTERCLOTHING) == 0)
            return;

        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId);
        Dirty(ent);
    }

    private void OnAction(Entity<CMUTraumaGovernorComponent> ent, ref CMUTraumaGovernorActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetWornGovernor(args.Performer, out var governor) || governor.Owner != ent.Owner)
            return;

        args.Handled = true;
        _ui.TryOpenUi(args.Performer, CMUTraumaGovernorUI.Key, args.Performer);
    }

    private void OnInteractUsing(Entity<CMUTraumaGovernorComponent> ent, ref InteractUsingEvent args)
    {
        if (!HasComp<CMUTraumaGovernorVialComponent>(args.Used))
            return;

        args.Handled = true;

        if (ent.Comp.VialLoaded)
        {
            _popup.PopupClient(Loc.GetString("cmu-trauma-governor-vial-already-loaded"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (_net.IsClient)
            return;

        ent.Comp.VialLoaded = true;
        Dirty(ent);
        Del(args.Used);

        _popup.PopupClient(Loc.GetString("cmu-trauma-governor-vial-loaded"), ent, args.User);
    }

    private void OnGovernorRemoved(Entity<CMUTraumaGovernorComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.Action is { } action)
            _actions.RemoveAction(action);
    }

    private void OnOrganChosen(Entity<CMUHumanMedicalComponent> patient, ref CMUTraumaGovernorChooseOrganBuiMsg args)
    {
        var user = args.Actor;
        if (!Enum.IsDefined(args.Target))
            return;

        if (user != patient.Owner)
            return;

        if (_net.IsClient)
            return;

        if (!TryGetWornGovernor(patient, out var governor))
        {
            _popup.PopupClient(Loc.GetString("cmu-trauma-governor-missing"), patient, user, PopupType.SmallCaution);
            return;
        }

        if (!CanUse(governor, user))
            return;

        if (!TryFindOrgan(patient, args.Target, out _, out var health))
        {
            _popup.PopupClient(Loc.GetString("cmu-trauma-governor-no-organ"), patient, user, PopupType.SmallCaution);
            return;
        }

        if (health.Stage is OrganDamageStage.Healthy or OrganDamageStage.Dead)
        {
            _popup.PopupClient(Loc.GetString("cmu-trauma-governor-ineligible"), patient, user, PopupType.SmallCaution);
            return;
        }

        var usedVial = _timing.CurTime < governor.Comp.NextUse || !governor.Comp.HasInternalCharge;
        if (usedVial)
            governor.Comp.VialLoaded = false;
        else
            governor.Comp.NextUse = _timing.CurTime + governor.Comp.Cooldown;

        Dirty(governor);

        var stabilized = EnsureComp<CMUOrganStabilizedComponent>(patient);
        stabilized.Target = args.Target;
        stabilized.ExpiresAt = _timing.CurTime + governor.Comp.Duration;
        Dirty(patient, stabilized);

        _popup.PopupClient(
            Loc.GetString("cmu-trauma-governor-applied", ("organ", Loc.GetString(GetTargetLocaleKey(args.Target)))),
            patient,
            user);
        _ui.CloseUi(patient.Owner, CMUTraumaGovernorUI.Key, user);
    }

    private bool CanUse(Entity<CMUTraumaGovernorComponent> governor, EntityUid user)
    {
        var now = _timing.CurTime;
        if (!governor.Comp.HasInternalCharge && !governor.Comp.VialLoaded)
        {
            _popup.PopupClient(Loc.GetString("cmu-trauma-governor-empty"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (governor.Comp.NextUse > now && !governor.Comp.VialLoaded)
        {
            _popup.PopupClient(Loc.GetString("cmu-trauma-governor-cooldown"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    public bool TryGetWornGovernor(EntityUid wearer, out Entity<CMUTraumaGovernorComponent> governor)
    {
        var slots = _inventory.GetSlotEnumerator(wearer, SlotFlags.OUTERCLOTHING);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } armor)
                continue;

            if (!TryComp<CMUTraumaGovernorComponent>(armor, out var comp))
                continue;

            governor = (armor, comp);
            return true;
        }

        governor = default;
        return false;
    }

    public bool IsStabilized(EntityUid body, CMUOrganStabilizerTarget target)
    {
        if (!TryComp<CMUOrganStabilizedComponent>(body, out var stabilized))
            return false;

        return stabilized.Target == target && stabilized.ExpiresAt > _timing.CurTime;
    }

    public float GetOrganPainMultiplier(EntityUid body, CMUOrganStabilizerTarget target)
    {
        return IsStabilized(body, target)
            ? StabilizedPainMultiplier
            : 1f;
    }

    public CMUTraumaGovernorReadout GetReadout(EntityUid patient)
    {
        if (!TryGetWornGovernor(patient, out var governor))
            return new CMUTraumaGovernorReadout(false, CMUTraumaGovernorState.Missing, null, 0, 0, false, false);

        var now = _timing.CurTime;
        CMUOrganStabilizerTarget? activeTarget = null;
        var activeSeconds = 0f;
        if (TryComp<CMUOrganStabilizedComponent>(patient, out var active) && active.ExpiresAt > now)
        {
            activeTarget = active.Target;
            activeSeconds = MathF.Max(0f, (float) (active.ExpiresAt - now).TotalSeconds);
        }

        var cooldownSeconds = MathF.Max(0f, (float) (governor.Comp.NextUse - now).TotalSeconds);
        var state = governor.Comp.HasInternalCharge || governor.Comp.VialLoaded
            ? cooldownSeconds > 0f && !governor.Comp.VialLoaded
                ? CMUTraumaGovernorState.CoolingDown
                : CMUTraumaGovernorState.Ready
            : CMUTraumaGovernorState.Empty;

        return new CMUTraumaGovernorReadout(
            true,
            state,
            activeTarget,
            activeSeconds,
            cooldownSeconds,
            governor.Comp.VialLoaded,
            cooldownSeconds > 0f && governor.Comp.VialLoaded);
    }

    public bool TryFindOrgan(
        EntityUid body,
        CMUOrganStabilizerTarget target,
        out EntityUid organ,
        out OrganHealthComponent health)
    {
        foreach (var candidate in _body.GetBodyOrgans(body))
        {
            if (!OrganMatchesTarget(candidate.Id, target))
                continue;

            if (!TryComp<OrganHealthComponent>(candidate.Id, out var candidateHealth))
                continue;

            organ = candidate.Id;
            health = candidateHealth;
            return true;
        }

        organ = EntityUid.Invalid;
        health = default!;
        return false;
    }

    public CMUOrganStabilizerTarget? GetTargetForOrgan(EntityUid organ)
    {
        if (HasComp<HeartComponent>(organ))
            return CMUOrganStabilizerTarget.Heart;
        if (HasComp<LungsComponent>(organ))
            return CMUOrganStabilizerTarget.Lungs;
        if (HasComp<CMUBrainComponent>(organ))
            return CMUOrganStabilizerTarget.Brain;
        if (HasComp<LiverComponent>(organ))
            return CMUOrganStabilizerTarget.Liver;
        if (HasComp<KidneysComponent>(organ))
            return CMUOrganStabilizerTarget.Kidneys;
        if (HasComp<CMUStomachComponent>(organ))
            return CMUOrganStabilizerTarget.Stomach;
        if (HasComp<EyesComponent>(organ))
            return CMUOrganStabilizerTarget.Eyes;
        if (HasComp<EarsComponent>(organ))
            return CMUOrganStabilizerTarget.Ears;

        return null;
    }

    private bool OrganMatchesTarget(EntityUid organ, CMUOrganStabilizerTarget target)
    {
        return target switch
        {
            CMUOrganStabilizerTarget.Heart => HasComp<HeartComponent>(organ),
            CMUOrganStabilizerTarget.Lungs => HasComp<LungsComponent>(organ),
            CMUOrganStabilizerTarget.Brain => HasComp<CMUBrainComponent>(organ),
            CMUOrganStabilizerTarget.Liver => HasComp<LiverComponent>(organ),
            CMUOrganStabilizerTarget.Kidneys => HasComp<KidneysComponent>(organ),
            CMUOrganStabilizerTarget.Stomach => HasComp<CMUStomachComponent>(organ),
            CMUOrganStabilizerTarget.Eyes => HasComp<EyesComponent>(organ),
            CMUOrganStabilizerTarget.Ears => HasComp<EarsComponent>(organ),
            _ => false,
        };
    }

    public static string GetTargetLocaleKey(CMUOrganStabilizerTarget target)
    {
        return target switch
        {
            CMUOrganStabilizerTarget.Heart => "cmu-trauma-governor-organ-heart",
            CMUOrganStabilizerTarget.Lungs => "cmu-trauma-governor-organ-lungs",
            CMUOrganStabilizerTarget.Brain => "cmu-trauma-governor-organ-brain",
            CMUOrganStabilizerTarget.Liver => "cmu-trauma-governor-organ-liver",
            CMUOrganStabilizerTarget.Kidneys => "cmu-trauma-governor-organ-kidneys",
            CMUOrganStabilizerTarget.Stomach => "cmu-trauma-governor-organ-stomach",
            CMUOrganStabilizerTarget.Eyes => "cmu-trauma-governor-organ-eyes",
            CMUOrganStabilizerTarget.Ears => "cmu-trauma-governor-organ-ears",
            _ => "cmu-trauma-governor-organ-unknown",
        };
    }
}
