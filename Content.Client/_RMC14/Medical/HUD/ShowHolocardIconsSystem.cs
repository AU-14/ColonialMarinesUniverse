using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Client.Overlays;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Medical.HUD;

public sealed class ShowHolocardIconsSystem : EquipmentHudSystem<HolocardScannerComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<HealthIconPrototype> Urgent = "UrgentHolocardIcon";
    private static readonly ProtoId<HealthIconPrototype> Emergency = "EmergencyHolocardIcon";
    private static readonly ProtoId<HealthIconPrototype> Xeno = "XenoHolocardIcon";
    private static readonly ProtoId<HealthIconPrototype> Permadead = "PermaHolocardIcon";
    private static readonly ProtoId<HealthIconPrototype> Stable = "StableHolocardIcon";
    private static readonly ProtoId<HealthIconPrototype> Trauma = "TraumaHolocardIcon";
    private static readonly ProtoId<HealthIconPrototype> OrganFailure = "OrganFailureHolocardIcon";

    private readonly Dictionary<HolocardStatus, StatusIconData> _iconByStatus = new();
    private bool _iconsCached;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HolocardStateComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void EnsureIconsCached()
    {
        if (_iconsCached)
            return;
        _iconsCached = true;

        Cache(HolocardStatus.Urgent, Urgent);
        Cache(HolocardStatus.Emergency, Emergency);
        Cache(HolocardStatus.Xeno, Xeno);
        Cache(HolocardStatus.Permadead, Permadead);
        Cache(HolocardStatus.Stable, Stable);
        Cache(HolocardStatus.Trauma, Trauma);
        Cache(HolocardStatus.OrganFailure, OrganFailure);

        void Cache(HolocardStatus status, ProtoId<HealthIconPrototype> id)
        {
            if (_prototypes.TryIndex(id, out var proto))
                _iconByStatus[status] = proto;
        }
    }

    private void OnGetStatusIconsEvent(Entity<HolocardStateComponent> entity, ref GetStatusIconsEvent args)
    {
        if (!IsActive)
            return;

        EnsureIconsCached();

        if (_iconByStatus.TryGetValue(entity.Comp.HolocardStatus, out var icon))
            args.StatusIcons.Add(icon);
    }

    public IReadOnlyList<StatusIconData> GetIcons(Entity<HolocardStateComponent> entity)
    {
        EnsureIconsCached();

        if (_iconByStatus.TryGetValue(entity.Comp.HolocardStatus, out var icon))
            return new[] { icon };

        return Array.Empty<StatusIconData>();
    }

    public bool TryGetHolocardData(HolocardStatus holocardStatus, out HolocardData data)
    {
        data = new HolocardData();
        switch (holocardStatus)
        {
            case HolocardStatus.None:
                data.HolocardIcon = null;
                data.Description = Loc.GetString("hc-none-description");
                break;
            case HolocardStatus.Urgent:
                data.HolocardIcon = Urgent;
                data.Description = Loc.GetString("hc-urgent-description");
                break;
            case HolocardStatus.Emergency:
                data.HolocardIcon = Emergency;
                data.Description = Loc.GetString("hc-emergency-description");
                break;
            case HolocardStatus.Xeno:
                data.HolocardIcon = Xeno;
                data.Description = Loc.GetString("hc-xeno-description");
                break;
            case HolocardStatus.Permadead:
                data.HolocardIcon = Permadead;
                data.Description = Loc.GetString("hc-permadead-description");
                break;
            case HolocardStatus.Stable:
                data.HolocardIcon = Stable;
                data.Description = Loc.GetString("cmu-medical-holocard-stable-desc");
                break;
            case HolocardStatus.Trauma:
                data.HolocardIcon = Trauma;
                data.Description = Loc.GetString("cmu-medical-holocard-trauma-desc");
                break;
            case HolocardStatus.OrganFailure:
                data.HolocardIcon = OrganFailure;
                data.Description = Loc.GetString("cmu-medical-holocard-organ-failure-desc");
                break;
            default:
                data = default;
                return false;
        }

        return true;
    }

    public bool TryGetHolocardName(HolocardStatus holocardStatus, [NotNullWhen(true)] out string? holocardName)
    {
        holocardName = null;
        switch (holocardStatus)
        {
            case HolocardStatus.None:
                holocardName = Loc.GetString("hc-none-name");
                break;
            case HolocardStatus.Urgent:
                holocardName = Loc.GetString("hc-urgent-name");
                break;
            case HolocardStatus.Emergency:
                holocardName = Loc.GetString("hc-emergency-name");
                break;
            case HolocardStatus.Xeno:
                holocardName = Loc.GetString("hc-xeno-name");
                break;
            case HolocardStatus.Permadead:
                holocardName = Loc.GetString("hc-permadead-name");
                break;
            case HolocardStatus.Stable:
                holocardName = Loc.GetString("cmu-medical-holocard-stable");
                break;
            case HolocardStatus.Trauma:
                holocardName = Loc.GetString("cmu-medical-holocard-trauma");
                break;
            case HolocardStatus.OrganFailure:
                holocardName = Loc.GetString("cmu-medical-holocard-organ-failure");
                break;
            default:
                return false;
        }
        return true;
    }

    public bool TryGetHolocardColor(HolocardStatus holocardStatus, [NotNullWhen(true)] out Color? holocardColor)
    {
        holocardColor = null;
        switch (holocardStatus)
        {
            case HolocardStatus.Urgent:
                holocardColor = Color.Chocolate;
                break;
            case HolocardStatus.Emergency:
                holocardColor = Color.DarkRed;
                break;
            case HolocardStatus.Xeno:
                holocardColor = Color.Purple;
                break;
            case HolocardStatus.Permadead:
                holocardColor = Color.Black;
                break;
            case HolocardStatus.Stable:
                holocardColor = Color.LightGreen;
                break;
            case HolocardStatus.Trauma:
                holocardColor = Color.Orange;
                break;
            case HolocardStatus.OrganFailure:
                holocardColor = Color.Red;
                break;
            default:
                return false;
        }
        return true;
    }

    public bool TryGetHolocardColor(Entity<HolocardStateComponent> entity, [NotNullWhen(true)] out Color? holocardColor)
    {
        holocardColor = null;
        if (TryGetHolocardColor(entity.Comp.HolocardStatus, out var holoColor))
        {
            holocardColor = holoColor;
            return true;
        }
        return false;
    }

    public bool TryGetDescription(Entity<HolocardStateComponent> entity, [NotNullWhen(true)] out string? description)
    {
        description = null;
        if (TryGetHolocardData(entity.Comp.HolocardStatus, out var holocardData))
        {
            description = holocardData.Description;
            return true;
        }
        return false;
    }
}
