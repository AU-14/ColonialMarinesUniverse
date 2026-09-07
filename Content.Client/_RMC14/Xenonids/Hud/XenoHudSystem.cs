using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Xenonids.Hud;

public sealed partial class XenoHudSystem : EntitySystem
{
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<FactionIconPrototype> AllyIcon = "CMUXenoHiveAlly";

    public override void Initialize()
    {
        // CMU14: use the same hive-specific alliance check as attacks and other interactions.
        SubscribeLocalEvent<MobStateComponent, GetStatusIconsEvent>(OnGetAllyIcon);

        if (!_overlay.HasOverlay<XenoHudOverlay>())
            _overlay.AddOverlay(new XenoHudOverlay());
    }

    private void OnGetAllyIcon(Entity<MobStateComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_player.LocalEntity is not { } viewer || !HasComp<XenoComponent>(viewer) ||
            _hive.GetHive(viewer) is not { } hive || _hive.IsMember(ent.Owner, hive.Owner) ||
            !_hive.IsAllyOfHive(ent.Owner, hive.Owner))
        {
            return;
        }

        args.StatusIcons.Add(_prototypes.Index(AllyIcon));
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<XenoHudOverlay>();
    }
}
