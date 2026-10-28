using Content.Shared.CMU14.EntityReferences;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Ghost;

/// <summary>
///     Ported from RMC-14 PR 10355: a ghost looks like the character it came from. Points at the body the ghost left;
///     each client copies that body's sprite onto the ghost the first time it sees both.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUGhostAppearanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Source;
}

public sealed partial class CMUGhostAppearanceSystem : EntitySystem
{
    [Dependency] private EntityReferenceSystem _references = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGhostAppearanceComponent, ReferencedEntityTerminatingEvent>(OnSourceTerminating);
    }

    private void OnSourceTerminating(Entity<CMUGhostAppearanceComponent> ent, ref ReferencedEntityTerminatingEvent args)
    {
        if (ent.Comp.Source != args.Entity)
            return;

        // Clients that copied the sprite keep it; future states must not reference the deleted body.
        ent.Comp.Source = null;
        Dirty(ent);
    }

    /// <summary>
    ///     Makes <paramref name="ghost"/> take on the look of <paramref name="source"/>, the body it left.
    /// </summary>
    public void SetSource(EntityUid ghost, EntityUid? source)
    {
        if (source is not { } body || TerminatingOrDeleted(body))
            return;

        var comp = EnsureComp<CMUGhostAppearanceComponent>(ghost);
        comp.Source = body;
        _references.Watch(ghost, body);
        Dirty(ghost, comp);
    }
}

/// <summary>
///     A hidden body built from a lobby observer's selected character, carried by their ghost so it has something to
///     look like. Clients never draw it; the ghost copies its sprite.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUGhostAppearanceStandInComponent : Component;
