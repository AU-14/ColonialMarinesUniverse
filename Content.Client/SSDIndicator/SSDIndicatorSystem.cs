using System.Collections.Generic;
using Content.Shared.CCVar;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client.SSDIndicator;

/// <summary>
///     Handles displaying SSD indicator as status icon
/// </summary>
public sealed class SSDIndicatorSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private bool _showSsdIndicator;

    // Avoid an IPrototypeManager.Index lookup for every visible SSD entity every frame.
    private readonly Dictionary<ProtoId<SsdIconPrototype>, SsdIconPrototype> _iconCache = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SSDIndicatorComponent, GetStatusIconsEvent>(OnGetStatusIcon);
        _cfg.OnValueChanged(CCVars.ICShowSSDIndicator, v => _showSsdIndicator = v, true);
    }

    private void OnGetStatusIcon(EntityUid uid, SSDIndicatorComponent component, ref GetStatusIconsEvent args)
    {
        // Cheapest checks first so non-SSD entities (and the entire codepath when the cvar is off)
        // bail before touching the component registry.
        if (!_showSsdIndicator || !component.IsSSD)
            return;

        if (_mobState.IsDead(uid) ||
            HasComp<ActiveNPCComponent>(uid) ||
            !TryComp<MindContainerComponent>(uid, out var mindContainer) ||
            !mindContainer.ShowExamineInfo)
        {
            return;
        }

        if (!_iconCache.TryGetValue(component.Icon, out var icon))
        {
            icon = _prototype.Index(component.Icon);
            _iconCache[component.Icon] = icon;
        }

        args.StatusIcons.Add(icon);
    }
}
