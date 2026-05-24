using Content.Shared._RMC14.Stealth;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Stealth.Components;
using Content.Shared.Whitelist;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.StatusIcon;

/// <summary>
/// This handles rendering gathering and rendering icons on entities.
/// </summary>
public sealed partial class StatusIconSystem : SharedStatusIconSystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private EntityWhitelistSystem _entityWhitelist = default!;
    [Dependency] private IGameTiming _timing = default!;

    private bool _globalEnabled;
    private bool _localEnabled;

    private static readonly TimeSpan IconCacheLifetime = TimeSpan.FromSeconds(0.25);
    private const int MaxCachedIconEntities = 512;

    private readonly Dictionary<EntityUid, CachedStatusIcons> _iconCache = new();

    private sealed class CachedStatusIcons
    {
        public readonly List<StatusIconData> Icons = new();
        public TimeSpan Expires;
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        Subs.CVar(_configuration, CCVars.LocalStatusIconsEnabled, OnLocalStatusIconChanged, true);
        Subs.CVar(_configuration, CCVars.GlobalStatusIconsEnabled, OnGlobalStatusIconChanged, true);
    }

    private void OnLocalStatusIconChanged(bool obj)
    {
        _localEnabled = obj;
        UpdateOverlayVisible();
    }

    private void OnGlobalStatusIconChanged(bool obj)
    {
        _globalEnabled = obj;
        UpdateOverlayVisible();
    }

    private void UpdateOverlayVisible()
    {
        if (_globalEnabled && _localEnabled)
        {
            if (!_overlay.HasOverlay<StatusIconOverlay>())
                _overlay.AddOverlay(new StatusIconOverlay());

            return;
        }

        _overlay.RemoveOverlay<StatusIconOverlay>();
    }

    public List<StatusIconData> GetStatusIcons(EntityUid uid, MetaDataComponent? meta = null)
    {
        var list = new List<StatusIconData>();
        GetStatusIcons(uid, list, meta);
        return list;
    }

    public void GetStatusIcons(EntityUid uid, List<StatusIconData> list, MetaDataComponent? meta = null)
    {
        list.Clear();
        if (!Resolve(uid, ref meta))
            return;

        if (meta.EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        var now = _timing.RealTime;
        if (_iconCache.TryGetValue(uid, out var cached) && cached.Expires > now)
        {
            list.AddRange(cached.Icons);
            return;
        }

        if (cached is null)
        {
            if (_iconCache.Count > MaxCachedIconEntities)
                _iconCache.Clear();

            cached = new CachedStatusIcons();
            _iconCache[uid] = cached;
        }

        cached.Icons.Clear();
        cached.Expires = now + IconCacheLifetime;

        var ev = new GetStatusIconsEvent(cached.Icons);
        RaiseLocalEvent(uid, ref ev);

        if (cached.Icons.Count > 1)
            cached.Icons.Sort();

        list.AddRange(cached.Icons);
    }

    /// <summary>
    /// For overlay to check if an entity can be seen.
    /// </summary>
    public bool IsVisible(Entity<MetaDataComponent> ent, StatusIconData data)
    {
        var viewer = _playerManager.LocalSession?.AttachedEntity;

        // Always show our icons to our entity
        if (viewer == ent.Owner)
            return true;

        if (data.VisibleToGhosts && HasComp<GhostComponent>(viewer))
            return true;

        if (data.HideInContainer && (ent.Comp.Flags & MetaDataFlags.InContainer) != 0)
            return false;

        if (data.HideOnStealth && TryComp<StealthComponent>(ent, out var stealth) && stealth.Enabled)
            return false;

        if (TryComp<SpriteComponent>(ent, out var sprite) && !sprite.Visible)
            return false;

        if (data.HideOnStealth && HasComp<EntityActiveInvisibleComponent>(ent))
            return false;

        if (data.ShowTo != null && !_entityWhitelist.IsValid(data.ShowTo, viewer))
            return false;

        return true;
    }
}
