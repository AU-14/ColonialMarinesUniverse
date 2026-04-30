using System.Collections.Generic;
using Content.Shared._RMC14.Connection;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;

namespace Content.Client._RMC14.Medical.HUD;

public sealed class CMHealthIconsSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly RMCUnrevivableSystem _unrevivable = default!;

    private static readonly ProtoId<HealthIconPrototype> BaseDeadIcon = "CMHealthIconDead";

    private readonly Dictionary<ProtoId<HealthIconPrototype>, StatusIconData> _indexedIcons = new();

    public StatusIconData GetDeadIcon()
    {
        return ResolveIcon(BaseDeadIcon)!;
    }

    public IReadOnlyList<StatusIconData> GetIcons(Entity<DamageableComponent> damageable)
    {
        if (TryGetIcon(damageable, out var statusIcon))
            return new[] { statusIcon };

        return Array.Empty<StatusIconData>();
    }

    public bool TryGetIcon(Entity<DamageableComponent> damageable, [NotNullWhen(true)] out StatusIconData? statusIcon)
    {
        statusIcon = null;

        if (!TryComp<RMCHealthIconsComponent>(damageable, out var iconsComp))
            return false;

        var icon = RMCHealthIconTypes.Healthy;

        if (_mobState.IsDead(damageable))
        {
            if (_unrevivable.IsUnrevivable(damageable))
            {
                icon = RMCHealthIconTypes.Dead;
            }
            else if (TryComp<MindCheckComponent>(damageable, out var mind) && !mind.ActiveMindOrGhost)
            {
                icon = RMCHealthIconTypes.DeadDNR;
            }
            else
            {
                var stage = _unrevivable.GetUnrevivableStage(damageable.Owner, 4);
                if (stage <= 1)
                    icon = RMCHealthIconTypes.DeadDefib;
                else if (stage == 2)
                    icon = RMCHealthIconTypes.DeadClose;
                else if (stage == 3)
                    icon = RMCHealthIconTypes.DeadAlmost;
            }
        }

        if (!iconsComp.Icons.TryGetValue(icon, out var iconToUse))
            return false;

        statusIcon = ResolveIcon(iconToUse);
        return statusIcon is not null;
    }

    private StatusIconData? ResolveIcon(ProtoId<HealthIconPrototype> id)
    {
        if (_indexedIcons.TryGetValue(id, out var cached))
            return cached;

        if (!_prototype.TryIndex(id, out var proto))
            return null;

        _indexedIcons[id] = proto;
        return proto;
    }
}
