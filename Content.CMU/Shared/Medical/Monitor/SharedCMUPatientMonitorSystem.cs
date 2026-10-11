using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.IV;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared.CMU14.Medical.Monitor;

/// <summary>
/// Dragging a patient monitor onto a patient attaches its leads, like an IV stand. The server does the attaching
/// and keeps the readouts; this half only tells the client what can be dragged where.
/// </summary>
public abstract partial class SharedCMUPatientMonitorSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPatientMonitorComponent, CanDragEvent>(OnCanDrag);
        SubscribeLocalEvent<CMUPatientMonitorComponent, CanDropDraggedEvent>(OnCanDropDragged);
        SubscribeLocalEvent<CMUPatientMonitorComponent, DragDropDraggedEvent>(OnDragDropDragged);
        SubscribeLocalEvent<CMUPatientMonitorComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUPatientMonitorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUPatientMonitorComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<CMUPatientMonitorComponent, ItemToggleActivateAttemptEvent>(OnToggleOnAttempt);
        SubscribeLocalEvent<CMUPatientMonitorComponent, ItemToggleDeactivateAttemptEvent>(OnToggleOffAttempt);
    }

    private void OnCanDrag(Entity<CMUPatientMonitorComponent> ent, ref CanDragEvent args)
    {
        // It has to be set down first, like an IV stand.
        if (!_container.IsEntityInContainer(ent))
            args.Handled = true;
    }

    private void OnCanDropDragged(Entity<CMUPatientMonitorComponent> ent, ref CanDropDraggedEvent args)
    {
        if (!HasComp<IVDripTargetComponent>(args.Target) || !InRange(ent, args.Target, ent.Comp.Range))
            return;

        args.Handled = true;
        args.CanDrop = true;
    }

    private void OnDragDropDragged(Entity<CMUPatientMonitorComponent> ent, ref DragDropDraggedEvent args)
    {
        if (args.Handled || !HasComp<IVDripTargetComponent>(args.Target))
            return;

        args.Handled = true;

        if (ent.Comp.AttachedTo == args.Target)
            Detach(ent, args.User);
        else
            Attach(ent, args.User, args.Target);
    }

    private void OnGetVerbs(Entity<CMUPatientMonitorComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.AttachedTo == null)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-monitor-verb-disconnect"),
            Act = () => Detach(ent, user),
        });
    }

    private void OnUiOpenAttempt(Entity<CMUPatientMonitorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        // Reading the screen takes the same basic medical training as placing the leads.
        if (args.Cancelled || _skills.HasAllSkills(args.User, ent.Comp.SkillRequired))
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("cmu-monitor-ui-no-skill"), ent, args.User);
    }

    private void OnToggleOnAttempt(Entity<CMUPatientMonitorComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (args.User is not { } user || _skills.HasAllSkills(user, ent.Comp.SkillRequired))
            return;

        args.Cancelled = true;
        args.Popup = Loc.GetString("cmu-monitor-toggle-no-skill");
    }

    private void OnToggleOffAttempt(Entity<CMUPatientMonitorComponent> ent, ref ItemToggleDeactivateAttemptEvent args)
    {
        // Running flat switches it off with no user; only people are stopped.
        if (args.User is not { } user || _skills.HasAllSkills(user, ent.Comp.SkillRequired))
            return;

        args.Cancelled = true;
        args.Popup = Loc.GetString("cmu-monitor-toggle-no-skill");
    }

    private void OnExamined(Entity<CMUPatientMonitorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(ent.Comp.AttachedTo is { } patient && !TerminatingOrDeleted(patient)
            ? Loc.GetString("cmu-monitor-examine-attached", ("patient", patient))
            : Loc.GetString("cmu-monitor-examine-detached"));
    }

    public bool InRange(EntityUid monitor, EntityUid patient, float range)
    {
        if (TerminatingOrDeleted(monitor) || TerminatingOrDeleted(patient))
            return false;

        return _transform.GetMapCoordinates(monitor).InRange(_transform.GetMapCoordinates(patient), range);
    }

    protected virtual void Attach(Entity<CMUPatientMonitorComponent> ent, EntityUid user, EntityUid patient)
    {
    }

    protected virtual void Detach(Entity<CMUPatientMonitorComponent> ent, EntityUid? user)
    {
    }
}
