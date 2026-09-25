using System.Numerics;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Optional setup assistance; release still uses every normal weapon restriction.</summary>
public static class FighterRunAssist
{
    public static bool CanQueue(FighterAircraftComponent aircraft, FighterSeatComponent seat,
        FighterWeaponStatus? weapon, FighterTarget? target) =>
        seat.Pilot && !aircraft.ForcedRetreat && aircraft.Phase != FighterPhase.Return &&
        weapon is { Kind: FighterWeaponKind.Gau or FighterWeaponKind.Rockets, PerShot: > 0 } &&
        weapon.Rounds >= weapon.PerShot && target is { Laser: false, CanStrike: true };

    public static bool TryPlan(FighterAircraftComponent aircraft, FighterWeaponsComponent weapons, FighterTarget target)
    {
        if (aircraft.Flying || aircraft.ForcedRetreat || target.Laser || !target.CanStrike ||
            !FighterFlight.Finite(target.Position) || !aircraft.Battlefield.Contains(target.Position)) return false;
        var direction = target.Position - aircraft.Position;
        direction = direction.LengthSquared() > .01f ? Vector2.Normalize(direction) : FighterFlight.Forward(aircraft.Heading);
        var entry = target.Position - direction * Math.Min(weapons.RunRange + 35, Reach(-direction));
        var exit = target.Position + direction * Math.Min(weapons.NearbyRange + 45, Reach(direction));
        if (!FighterFlight.TryPlan(aircraft, entry, exit)) return false;
        // Leave slower/lower settings alone. The prepared pass can still be edited before launch.
        aircraft.TargetHeight = Math.Min(aircraft.TargetHeight, 350);
        aircraft.TargetSpeed = Math.Clamp(Math.Min(aircraft.TargetSpeed, 12), aircraft.MinimumSpeed, aircraft.MaximumSpeed);
        return true;

        float Reach(Vector2 ray)
        {
            var offset = target.Position - aircraft.Home;
            var dot = Vector2.Dot(offset, ray);
            var discriminant = dot * dot + aircraft.AirspaceRadius * aircraft.AirspaceRadius - offset.LengthSquared();
            return discriminant <= 0 ? 0 : Math.Max(0, -dot + MathF.Sqrt(discriminant) - 1);
        }
    }
}
