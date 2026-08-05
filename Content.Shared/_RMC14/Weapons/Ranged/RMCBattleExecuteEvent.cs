using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Weapons.Ranged;

[Serializable, NetSerializable]
public sealed partial class RMCBattleExecuteEvent : SimpleDoAfterEvent
{
    public DamageSpecifier BattleExecuteDamage;

    public RMCBattleExecuteEvent(DamageSpecifier battleExecuteDamage)
    {
        BattleExecuteDamage = battleExecuteDamage;
    }
}
