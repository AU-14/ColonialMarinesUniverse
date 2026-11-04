using Content.Client.CMU14.DroneOperator;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private CMUCombatDroneTurretSystem _combatDroneTurret = default!;
}
