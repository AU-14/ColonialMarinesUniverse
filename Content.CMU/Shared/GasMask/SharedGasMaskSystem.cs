
using Content.Shared._RMC14.Inventory;
using Content.Shared.Examine;

namespace Content.Shared._CMU14.GasMask;

public sealed partial class SharedGasMaskSystem : EntitySystem
{
    //evil hardcoding
    private readonly float _epsilon = 0.001f;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasMaskFilterComponent, ComponentStartup>(OnComponentInit);

        SubscribeLocalEvent<GasMaskFilterComponent, ExaminedEvent>(OnExamined);
    }

    private void OnComponentInit(Entity<GasMaskFilterComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Integrity = ent.Comp.BaseIntegrity;
    }

    public bool IsFilterBroken(Entity<GasMaskFilterComponent> ent)
    {
        if (ent.Comp.Integrity == 0f || ent.Comp.Integrity <= _epsilon) return true;
        else return false;
    }

    public void DamageFilter(EntityUid uid, GasMaskFilterComponent comp, GasMaskFilterDamageComponent dam)
    {
        float newhp = comp.Integrity;
        if (!dam.Neurotoxin)
        {
            newhp -= dam.Damage;
        }
        else
        {
            newhp -= (dam.Damage * comp.NeurotoxinDamageMultiplier);
        }
        if (newhp <= _epsilon) newhp = 0f;
        comp.Integrity = newhp;
        Dirty(uid, comp);
    }

    private void OnExamined(Entity<GasMaskFilterComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(GasMaskFilterComponent)))
        {
            if (IsFilterBroken(ent))
            {
                args.PushMarkup(Loc.GetString("gas-mask-filter-broken"));
            }
            else
            {
                args.PushMarkup(Loc.GetString("gas-mask-filter-integrity-percentage", ("percent",
                    (ent.Comp.Integrity / ent.Comp.BaseIntegrity) * 100f)));
                if (ent.Comp.NeurotoxinResist == true)
                {
                    args.PushMarkup(Loc.GetString("gas-mask-filter-super"));
                }
            }
        }
    }
}
