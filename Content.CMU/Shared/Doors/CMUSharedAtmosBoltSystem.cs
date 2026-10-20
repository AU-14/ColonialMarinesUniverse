using Content.Shared.Examine;

namespace Content.Shared.CMU14.Doors;

/// <summary>
///     Shows the firelock's pressure differential warning on a door that has bolted itself against unsafe air.
/// </summary>
public sealed class CMUSharedAtmosBoltSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUAtmosBoltComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<CMUAtmosBoltComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.BoltedByAtmos)
            return;

        using (args.PushGroup(nameof(CMUAtmosBoltComponent)))
        {
            args.PushMarkup(Loc.GetString("firelock-component-examine-pressure-warning"));
        }
    }
}
