using Content.Shared.Examine;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Blood;

/// <summary>The blood type printed on an ID card, from the person it was issued or bound to.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUIdBloodTypeSystem))]
public sealed partial class CMUIdBloodTypeComponent : Component
{
    [DataField("bloodType"), AutoNetworkedField]
    public CMUBloodType Type;
}

public sealed class CMUIdBloodTypeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUIdBloodTypeComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>Prints the owner's blood type on the card. Does nothing if the owner has no blood type.</summary>
    public void Stamp(EntityUid card, EntityUid owner)
    {
        if (!TryComp<CMUBloodTypeComponent>(owner, out var bloodType))
            return;

        var printed = EnsureComp<CMUIdBloodTypeComponent>(card);
        if (printed.Type == bloodType.Type)
            return;

        printed.Type = bloodType.Type;
        Dirty(card, printed);
    }

    private void OnExamined(Entity<CMUIdBloodTypeComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-id-card-examine-blood-type", ("type", CMUBloodTypes.Name(ent.Comp.Type))));
    }
}
