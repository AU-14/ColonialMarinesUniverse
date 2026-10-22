using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;

namespace Content.Shared.CMU14.Chemistry.Fuel;

/// <summary>
/// Flamer tanks ship with <see cref="NoMixingReagentsComponent"/>, which forbids any blend. CMU sizes fires
/// from the whole tank solution (<see cref="CMUFuelProfileSystem"/>), so the restriction is lifted here instead
/// of editing every RMC tank prototype. The component is networked, so removing it on the server is enough.
/// </summary>
public sealed class CMUFlamerTankMixingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCFlamerTankComponent, MapInitEvent>(OnTankMapInit);
    }

    private void OnTankMapInit(Entity<RMCFlamerTankComponent> ent, ref MapInitEvent args)
    {
        RemComp<NoMixingReagentsComponent>(ent);
    }
}
