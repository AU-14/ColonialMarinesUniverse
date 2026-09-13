using System.Linq;
using Content.Client.Body;
using Content.Client.Items.Systems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Humanoid;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Whitelist;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Humanoid;

public sealed class HiddenAppearanceSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private EntityWhitelistSystem _entityWhitelist = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemSystem _item = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private VisualBodySystem _visualBody = default!;

    private readonly Dictionary<EntityUid, Dictionary<string, (EntityUid Organ, HumanoidVisualLayers Layer)>> _localMarkingLayers = new();
    private readonly HashSet<EntityUid> _overridden = new();

    private bool _hidePlayerIdentities;
    public bool HidePlayerIdentities => _hidePlayerIdentities;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(VisualBodySystem));

        SubscribeLocalEvent<HiddenAppearanceComponent, ComponentStartup>(OnHiddenStartup);
        SubscribeLocalEvent<HiddenAppearanceComponent, AfterAutoHandleStateEvent>(OnHiddenState);
        SubscribeLocalEvent<HiddenAppearanceComponent, ComponentRemove>(OnHiddenRemove);
        SubscribeLocalEvent<HumanoidProfileComponent, AfterAutoHandleStateEvent>(OnProfileState);
        SubscribeLocalEvent<VisualBodyComponent, VisualBodySpriteRefreshEvent>(OnVisualBodySpriteRefresh);
        SubscribeLocalEvent<VisualBodyComponent, VisualBodyMarkingsVisibilityChangedEvent>(OnMarkingsVisibility);

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerChanged);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerChanged);

        Subs.CVar(_config, RMCCVars.HidePlayerIdentities, OnHidePlayerIdentitiesChanged, true);
    }

    public bool IsLocalAppearanceOverrideActive(EntityUid body)
    {
        return _overridden.Contains(body);
    }

    public int LocalMarkingLayerCount(EntityUid body)
    {
        return _localMarkingLayers.GetValueOrDefault(body)?.Count ?? 0;
    }

    private void OnHiddenStartup(Entity<HiddenAppearanceComponent> ent, ref ComponentStartup args)
    {
        Refresh(ent.Owner, true);
    }

    private void OnHiddenState(Entity<HiddenAppearanceComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Refresh(ent.Owner, true);
    }

    private void OnHiddenRemove(Entity<HiddenAppearanceComponent> ent, ref ComponentRemove args)
    {
        Restore(ent.Owner);
        UpdatePlayerMedals(ent.Owner);
    }

    private void OnProfileState(Entity<HumanoidProfileComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (HasComp<HiddenAppearanceComponent>(ent.Owner) || _overridden.Contains(ent.Owner))
            Refresh(ent.Owner);
    }

    private void OnVisualBodySpriteRefresh(Entity<VisualBodyComponent> ent, ref VisualBodySpriteRefreshEvent args)
    {
        if (HasComp<HiddenAppearanceComponent>(ent.Owner) || _overridden.Contains(ent.Owner))
            Refresh(ent.Owner);
    }

    private void OnMarkingsVisibility(
        Entity<VisualBodyComponent> ent,
        ref VisualBodyMarkingsVisibilityChangedEvent args)
    {
        if (!_overridden.Contains(ent.Owner) ||
            !_localMarkingLayers.TryGetValue(ent.Owner, out var layers))
        {
            return;
        }

        _visualBody.SetLocalMarkingVisibility(args.Organ, ent.Owner, args.Visibility, layers);
    }

    private void OnLocalPlayerChanged<T>(T args)
    {
        RefreshAll(true);
    }

    private void OnHidePlayerIdentitiesChanged(bool value)
    {
        _hidePlayerIdentities = value;
        RefreshAll(true);
    }

    private void RefreshAll(bool updateMedals = false)
    {
        var bodies = _localMarkingLayers.Keys.ToHashSet();
        var query = EntityQueryEnumerator<HiddenAppearanceComponent, VisualBodyComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            bodies.Add(uid);
        }

        foreach (var body in bodies)
        {
            Refresh(body, updateMedals);
        }
    }

    private void Refresh(EntityUid body, bool updateMedals = false)
    {
        if (!TryComp<VisualBodyComponent>(body, out var visualBody))
        {
            Restore(body);
            return;
        }

        var layers = _localMarkingLayers.GetOrNew(body);
        _overridden.Remove(body);

        if (TryGetHiddenAppearance(body, out var hidden) &&
            TryComp<HumanoidProfileComponent>(body, out var profile) &&
            hidden.Species == profile.Species)
        {
            _visualBody.ApplyLocalAppearanceOverride(
                (body, visualBody),
                hidden.Appearance,
                hidden.Sex,
                layers);
            _overridden.Add(body);
        }
        else
        {
            _visualBody.ClearLocalAppearanceOverride((body, visualBody), layers);
        }

        if (updateMedals)
            UpdatePlayerMedals(body);
    }

    private void Restore(EntityUid body)
    {
        if (_localMarkingLayers.Remove(body, out var layers))
            _visualBody.ClearLocalAppearanceOverride(body, layers);

        _overridden.Remove(body);
    }

    private bool TryGetHiddenAppearance(
        EntityUid body,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out HiddenHumanoidAppearance? appearance)
    {
        appearance = null;
        if (!_hidePlayerIdentities ||
            !TryComp<HiddenAppearanceComponent>(body, out var hidden) ||
            hidden.Appearance is not { } hiddenAppearance ||
            _player.LocalEntity is not { } player ||
            !_entityWhitelist.IsWhitelistPass(hidden.Whitelist, player))
        {
            return false;
        }

        appearance = hiddenAppearance;
        return true;
    }

    private void UpdatePlayerMedals(EntityUid player)
    {
        var slots = _inventory.GetSlotEnumerator(player);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } contained)
                _item.VisualsChanged(contained);
        }
    }
}
