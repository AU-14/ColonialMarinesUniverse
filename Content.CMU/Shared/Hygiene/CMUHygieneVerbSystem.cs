using Content.Shared.Verbs;
using Content.Shared._RMC14.Chemistry;

namespace Content.Shared.CMU14.Hygiene;

public sealed partial class CMUHygieneVerbSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<RMCSinkWaterComponent, GetVerbsEvent<AlternativeVerb>>(OnSinkVerbs);
        SubscribeLocalEvent<CMUShowerControlsComponent, GetVerbsEvent<AlternativeVerb>>(OnShowerVerbs);
    }

    private void OnSinkVerbs(Entity<RMCSinkWaterComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-sink-wash"),
            Priority = 20,
            EventTarget = ent.Owner,
            ExecutionEventArgs = new CMUSinkWashRequestEvent(args.User),
        });
    }

    private void OnShowerVerbs(Entity<CMUShowerControlsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Running ? "cmu-shower-switch-off" : "cmu-shower-switch-on"),
            Priority = 20,
            EventTarget = ent.Owner,
            ExecutionEventArgs = new CMUShowerToggleRequestEvent(args.User),
        });
    }
}
