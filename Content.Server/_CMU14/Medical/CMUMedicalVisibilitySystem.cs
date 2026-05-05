using Content.Shared._CMU14.Medical;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Eye;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;

namespace Content.Server._CMU14.Medical;

public sealed class CMUMedicalVisibilitySystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;

    private EntityQuery<BodyPartComponent> _partQuery;
    private EntityQuery<OrganComponent> _organQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private readonly HashSet<EntityUid> _queuedRefresh = new();

    private const ushort InternalLayer = (ushort) VisibilityFlags.CMUMedicalInternals;

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        _partQuery = GetEntityQuery<BodyPartComponent>();
        _organQuery = GetEntityQuery<OrganComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<CMUHumanMedicalComponent, ComponentStartup>(OnMedicalBodyStartup);
        SubscribeLocalEvent<OrganComponent, OrganAddedToBodyEvent>(OnOrganAddedToBody);
        SubscribeLocalEvent<OrganComponent, OrganRemovedFromBodyEvent>(OnOrganRemovedFromBody);
        SubscribeLocalEvent<BodyPartComponent, ComponentStartup>(OnBodyPartStartup);
        SubscribeLocalEvent<OrganComponent, ComponentStartup>(OnOrganStartup);
        SubscribeLocalEvent<BodyPartComponent, EntParentChangedMessage>(OnBodyPartParentChanged);
        SubscribeLocalEvent<OrganComponent, EntParentChangedMessage>(OnOrganParentChanged);

        _cfg.OnValueChanged(CMUMedicalCCVars.HideAttachedInternals, OnEnabledChanged, true);
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;
        RefreshAll();
    }

    private void OnMedicalBodyStartup(Entity<CMUHumanMedicalComponent> ent, ref ComponentStartup args)
    {
        RefreshSubtree(ent.Owner);
    }

    private void OnOrganAddedToBody(Entity<OrganComponent> ent, ref OrganAddedToBodyEvent args)
    {
        RefreshEntity(ent.Owner);
    }

    private void OnOrganRemovedFromBody(Entity<OrganComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        // The body system raises this before OrganComponent.Body is cleared.
        QueueRefresh(ent.Owner);
    }

    private void OnBodyPartStartup(Entity<BodyPartComponent> ent, ref ComponentStartup args)
    {
        RefreshSubtree(ent.Owner);
    }

    private void OnOrganStartup(Entity<OrganComponent> ent, ref ComponentStartup args)
    {
        RefreshEntity(ent.Owner);
    }

    private void OnBodyPartParentChanged(Entity<BodyPartComponent> ent, ref EntParentChangedMessage args)
    {
        RefreshSubtree(ent.Owner);
    }

    private void OnOrganParentChanged(Entity<OrganComponent> ent, ref EntParentChangedMessage args)
    {
        RefreshEntity(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_queuedRefresh.Count == 0)
            return;

        foreach (var uid in _queuedRefresh)
        {
            RefreshEntity(uid);
        }

        _queuedRefresh.Clear();
    }

    private void RefreshAll()
    {
        var parts = EntityQueryEnumerator<BodyPartComponent>();
        while (parts.MoveNext(out var uid, out _))
        {
            RefreshEntity(uid);
        }

        var organs = EntityQueryEnumerator<OrganComponent>();
        while (organs.MoveNext(out var uid, out _))
        {
            RefreshEntity(uid);
        }
    }

    public void RefreshSubtree(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        RefreshEntity(uid);

        if (!_xformQuery.TryGetComponent(uid, out var xform))
            return;

        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            RefreshSubtree(child);
        }
        children.Dispose();
    }

    private void QueueRefresh(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        _queuedRefresh.Add(uid);
    }

    private void RefreshEntity(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var isInternal = _partQuery.HasComp(uid) || _organQuery.HasComp(uid);
        if (!isInternal)
            return;

        var shouldHide = _enabled && IsAttachedToCMUMedicalBody(uid);
        if (!TryComp<VisibilityComponent>(uid, out var visibility))
        {
            if (!shouldHide)
                return;

            visibility = EnsureComp<VisibilityComponent>(uid);
        }

        if (shouldHide)
            _visibility.AddLayer((uid, visibility), InternalLayer);
        else
            _visibility.RemoveLayer((uid, visibility), InternalLayer);
    }

    private bool IsAttachedToCMUMedicalBody(EntityUid uid)
    {
        if (_partQuery.TryGetComponent(uid, out var part) &&
            part.Body is { } partBody &&
            HasComp<CMUHumanMedicalComponent>(partBody))
        {
            return true;
        }

        if (_organQuery.TryGetComponent(uid, out var organ) &&
            organ.Body is { } organBody &&
            HasComp<CMUHumanMedicalComponent>(organBody))
        {
            return true;
        }

        return false;
    }
}
