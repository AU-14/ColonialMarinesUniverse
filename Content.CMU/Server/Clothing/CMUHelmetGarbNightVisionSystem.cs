using Content.Shared._RMC14.NightVision;
using Content.Shared.CMU14.Clothing;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Clothing;

/// <summary>
///     Turns a helmet's night vision goggles on for the helmet's wearer while they're pulled down, and off again when
///     they're pushed up, taken out of the helmet, or the helmet comes off.
/// </summary>
public sealed class CMUHelmetGarbNightVisionSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private SharedNightVisionSystem _nightVision = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUHelmetGarbNightVisionComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnToggled(Entity<CMUHelmetGarbNightVisionComponent> ent, ref ItemToggledEvent args)
    {
        if (TryComp(ent, out NightVisionItemComponent? nightVision))
            Refresh(ent, nightVision);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<CMUHelmetGarbNightVisionComponent, NightVisionItemComponent>();
        while (query.MoveNext(out var uid, out var garb, out var nightVision))
        {
            Refresh((uid, garb), nightVision);
        }
    }

    private void Refresh(Entity<CMUHelmetGarbNightVisionComponent> ent, NightVisionItemComponent nightVision)
    {
        var wearer = _itemToggle.IsActivated(ent.Owner) ? Wearer(ent) : null;
        if (nightVision.User == wearer)
            return;

        if (nightVision.User is { } previous)
            _nightVision.DisableNightVisionItem((ent, nightVision), previous);

        if (wearer is { } user)
            _nightVision.EnableNightVisionItem((ent, nightVision), user);
    }

    /// <summary>Whoever has the helmet these goggles are in on their head.</summary>
    private EntityUid? Wearer(Entity<CMUHelmetGarbNightVisionComponent> ent)
    {
        if (!_container.TryGetContainingContainer(ent.Owner, out var helmetStorage))
            return null;

        var helmet = helmetStorage.Owner;
        if (!_container.TryGetContainingContainer(helmet, out var slot) || slot.ID != ent.Comp.Slot)
            return null;

        return slot.Owner;
    }
}
