using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Line;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.AcidMine;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Insight;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._RMC14.Xenonids.DeployTraps;

public sealed partial class XenoDeployTrapsSystem : EntitySystem
{
    [Dependency] private IMapManager _map = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private XenoInsightSystem _insight = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoDeployTrapsComponent, XenoDeployTrapsActionEvent>(OnXenoDeployTrapsAction);
    }

    private void OnXenoDeployTrapsAction(Entity<XenoDeployTrapsComponent> xeno, ref XenoDeployTrapsActionEvent args)
    {
        if (args.Handled)
            return;

        // Check if target on grid
        if (_transform.GetGrid(args.Target) is not { } gridId ||
            !TryComp(gridId, out MapGridComponent? grid))
            return;

        var coords = args.Target.SnapToGrid(EntityManager, _map);

        if (!_examine.InRangeUnOccluded(xeno.Owner, coords, xeno.Comp.Range))
        {
            _popup.PopupClient(Loc.GetString("rmc-xeno-deploy-traps-see-fail"), xeno, xeno);
            return;
        }

        if (!_xenoPlasma.TryRemovePlasmaPopup((xeno.Owner, null), args.PlasmaCost))
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        _audio.PlayPredicted(xeno.Comp.DeploySound, coords, xeno);

        var popupSelf = Loc.GetString("rmc-xeno-deploy-traps-self");
        var popupOthers = Loc.GetString("rmc-xeno-deploy-traps-others", ("xeno", xeno));
        _popup.PopupPredicted(popupSelf, popupOthers, xeno, xeno);

        if (_net.IsServer)
        {
            var xenoPos = _transform.ToWorldPosition(xeno.Owner.ToCoordinates());
            var targetPos = _transform.ToWorldPosition(coords);

            var direction = (targetPos - xenoPos).Normalized();
            var ortho = new Vector2(-direction.Y, direction.X);

            // Round orthogonal world vector to nearest tile-space step
            var orthoTile = new Vector2i(
                (int) MathF.Round(ortho.X),
                (int) MathF.Round(ortho.Y)
            );

            if (orthoTile == Vector2i.Zero)
                orthoTile = new Vector2i(1, 0);

            var mapSystem = EntityManager.System<SharedMapSystem>();
            var centerTile = mapSystem.CoordinatesToTile(gridId, grid, coords);

            var radius = (int) xeno.Comp.DeployTrapsRadius;
            var empowered = xeno.Comp.Empowered;

            for (var i = -radius; i <= radius; i++)
            {
                var tileIndex = centerTile + orthoTile * i;
                var tileCoords = new EntityCoordinates(gridId, mapSystem.GridTileToLocal(gridId, grid, tileIndex).Position);
                if(!_rmcMap.HasAnchoredEntityEnumerator<DeployTrapsBlockerComponent>(tileCoords, out _))
                    DeployTraps(xeno, tileCoords, empowered);
            }

            if (empowered)
            {
                DeployTrapsEmpower(xeno);
                if (xeno.Comp.Emote is { } emote)
                    _emote.TryEmoteWithChat(xeno, emote, false, null, false, true);
                xeno.Comp.Empowered = false;
                _insight.IncrementInsight(xeno.Owner, -10);
                foreach (var action in _actions.GetActions(xeno.Owner))
                {
                    if (_actions.GetEvent(action) is XenoDeployTrapsActionEvent)
                        _actions.SetIcon(action.AsNullable(), xeno.Comp.ActionIcon);
                }
            }
        }
    }

    private void DeployTraps(Entity<XenoDeployTrapsComponent> xeno, EntityCoordinates target, bool empowered)
    {
        if (!target.IsValid(EntityManager))
            return;

        if (_net.IsServer)
        {
            if (empowered)
            {
                var traps = SpawnAtPosition(xeno.Comp.DeployEmpoweredTrapsId, target);
                _hive.SetSameHive(xeno.Owner, traps);
            }
            else
            {
                var traps = SpawnAtPosition(xeno.Comp.DeployTrapsId, target);
                _hive.SetSameHive(xeno.Owner, traps);
            }
        }
    }

    private void DeployTrapsEmpower(Entity<XenoDeployTrapsComponent> xeno)
    {
        if (!_net.IsServer)
            return;

        if (TryComp(xeno.Owner, out XenoAcidMineComponent? acidMine))
            acidMine.Empowered = true;
        _popup.PopupPredicted(Loc.GetString("rmc-xeno-deploy-traps-empower"), xeno, xeno, PopupType.Medium);
        foreach (var action in _actions.GetActions(xeno.Owner))
        {
            if (_actions.GetEvent(action) is XenoAcidMineActionEvent)
                _actions.SetIcon(action.AsNullable(), acidMine?.ActionIconEmpowered);
        }

    }
}
