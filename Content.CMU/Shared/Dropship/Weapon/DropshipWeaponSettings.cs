using System.Numerics;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Explosion.EntitySystems;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Dropship.Weapon;

[Serializable, NetSerializable]
public enum DropshipLaserPattern : byte
{
    Ring,
    Line,
}

[Serializable, NetSerializable]
public enum DropshipAirburstHeight : byte
{
    Low,
    Medium,
    High,
}

[Serializable, NetSerializable]
public sealed class DropshipWeaponSettingsMessage(bool first, DropshipLaserPattern pattern,
    DropshipAirburstHeight height) : BoundUserInterfaceMessage
{
    public readonly bool First = first;
    public readonly DropshipLaserPattern Pattern = pattern;
    public readonly DropshipAirburstHeight Height = height;
}

public abstract partial class SharedDropshipWeaponSystem
{
    private void OnWeaponSettings(Entity<DropshipTerminalWeaponsComponent> terminal,
        ref DropshipWeaponSettingsMessage args)
    {
        if (_net.IsClient || !Enum.IsDefined(args.Pattern) || !Enum.IsDefined(args.Height))
            return;

        var selection = args.First ? terminal.Comp.ScreenOne : terminal.Comp.ScreenTwo;
        if (!TryGetEntity(selection.Weapon, out var uid) || uid is not { } weapon ||
            !TryComp(weapon, out DropshipWeaponComponent? component) ||
            !_dropship.TryGetGridDropship(terminal, out var ship) ||
            !_dropship.TryGetGridDropship(weapon, out var weaponShip) || ship.Owner != weaponShip.Owner ||
            !TryGetWeaponAmmo((weapon, component), out var ammo))
        {
            _popup.PopupEntity(Loc.GetString("rmc-dropship-weapons-fire-no-weapon"), terminal, args.Actor);
            return;
        }

        if (component.Skills != null && !_skills.HasSkills(args.Actor, component.Skills))
        {
            _popup.PopupEntity(Loc.GetString("rmc-laser-designator-not-skilled"), terminal, args.Actor);
            return;
        }

        if (ammo.Comp.SelectableLaserPattern)
            component.LaserPattern = args.Pattern;
        if (ammo.Comp.SelectableAirburstHeight)
            component.AirburstHeight = args.Height;
        Dirty(weapon, component);
        RefreshWeaponsUI(terminal);
    }

    public static RMCExplosion? GetAirburstExplosion(DropshipAmmoComponent ammo, DropshipAirburstHeight height)
    {
        if (!ammo.SelectableAirburstHeight || ammo.Explosion is not { } explosion)
            return ammo.Explosion;

        var slope = height switch
        {
            DropshipAirburstHeight.Low => 12f,
            DropshipAirburstHeight.Medium => explosion.Slope,
            DropshipAirburstHeight.High => 4f,
            _ => throw new ArgumentOutOfRangeException(nameof(height)),
        };
        var max = height switch
        {
            DropshipAirburstHeight.Low => 8f,
            DropshipAirburstHeight.Medium => explosion.Max,
            DropshipAirburstHeight.High => 2f,
            _ => throw new ArgumentOutOfRangeException(nameof(height)),
        };
        return new RMCExplosion { Type = explosion.Type, Total = explosion.Total, Slope = slope, Max = max };
    }

    public static Vector2 GetLaserImpactOffset(int index, int count, float radius, Angle rotation,
        DropshipLaserPattern pattern)
    {
        if (count <= 0 || index < 0 || index >= count || radius < 0 || !Enum.IsDefined(pattern))
            throw new ArgumentOutOfRangeException(nameof(index));
        if (count == 1)
            return Vector2.Zero;
        return pattern == DropshipLaserPattern.Line
            ? rotation.RotateVec(new Vector2(-radius + 2 * radius * index / (count - 1), 0))
            : GetVolleyImpactOffset(index, count, radius, rotation);
    }

    public static float EstimateBlastRadius(RMCExplosion explosion)
    {
        // Unobstructed energy envelope only; walls and blast resistance change actual coverage.
        return 0.5f + SharedExplosionSystem.CalculateBlastRadius(explosion.Total, explosion.Slope, explosion.Max);
    }
}
