using Content.Client._RMC14.Xenonids.Hive;
using Content.Client.Weapons.Melee;
using Content.Shared._RMC14.Input;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Tackle;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Client._RMC14.Weapons.Melee;

public sealed partial class RMCMeleeWeaponSystem : SharedRMCMeleeWeaponSystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MeleeWeaponSystem _melee = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private XenoHiveSystem _hive = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(CMKeyFunctions.CMXenoWideSwing,
                InputCmdHandler.FromDelegate(session =>
                {
                    if (session?.AttachedEntity != null)
                        TryPrimaryHeavyAttack();
                }, handle: false))
            .Register<RMCMeleeWeaponSystem>();
    }

    private void TryPrimaryHeavyAttack()
    {
        var mousePos = _eye.PixelToMap(_input.MouseScreenPosition);
        EntityUid grid;

        if (_map.TryFindGridAt(mousePos, out var gridUid, out _))
            grid = gridUid;
        else if (_map.TryGetMap(mousePos.MapId, out var map))
            grid = map.Value;
        else
            return;

        var coordinates = _transform.ToCoordinates(grid, mousePos);

        if (_player.LocalEntity is not { } entity)
            return;

        if (!_melee.TryGetWeapon(entity, out var weaponUid, out var weapon))
            return;

        if (weapon.WidePrimary)
            _melee.ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
    }

    /// <summary>
    /// Gets the closest alive mob that was sprite clicked.
    /// Prioritizes mobs that are not hive members.
    /// </summary>
    /// <param name="attacker"></param>
    /// <param name="clickCoords">Mouse Click Coordinates</param>
    /// <param name="clickedEntities">All Entities that are under the clickCoords</param>
    /// <param name="newTarget"></param>
    /// <returns></returns>
    public bool TryGetAlternativeXenoAttackTarget(EntityUid attacker, MapCoordinates clickCoords, IEnumerable<EntityUid> clickedEntities, [NotNullWhen(true)] out EntityUid? newTarget)
    {
        newTarget = null;

        EntityUid? closestNonHive = null;
        EntityUid? closestHive = null;
        float closestNonHiveDist = float.MaxValue;
        float closestHiveDist = float.MaxValue;

        foreach (var ent in clickedEntities)
        {
            // For the purposes of finding an alternative target, non-mobs or dead mobs are ignored
            if (!HasComp<MobStateComponent>(ent) ||
                _mobState.IsDead(ent))
            {
                continue;
            }
            
            var curDist = (_transform.GetMapCoordinates(ent).Position - clickCoords.Position).Length();
            var isSameHive = _hive.FromSameHive(attacker, ent);

            if (isSameHive && closestHiveDist > curDist)
            {
                closestHive = ent;
                closestHiveDist = curDist;
            }

            if (!isSameHive && closestNonHiveDist > curDist)
            {
                closestNonHive = ent;
                closestNonHiveDist = curDist;
            }
        }

        newTarget = closestNonHive ?? closestHive;
        return newTarget is not null;
    }
}
