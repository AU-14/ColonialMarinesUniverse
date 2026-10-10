using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Nodes;
using Content.Shared.CMU14.Power;
using Content.Shared.Examine;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Power;

public sealed class CMUAutoTransferSwitchSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUAutoTransferSwitchComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUAutoTransferSwitchComponent, PowerConsumerReceivedChanged>(OnReceivedChanged);
        SubscribeLocalEvent<CMUAutoTransferSwitchComponent, ExaminedEvent>(OnExamined);
    }

    // The output node is the only gate: Standby keeps it cut, Online closes it
    // and the discharger feeds the emergency circuit from the battery.
    private void OnStartup(Entity<CMUAutoTransferSwitchComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.State == CMUAtsState.Online)
            return;

        if (_nodeContainer.TryGetNode<CableDeviceNode>(ent.Owner, ent.Comp.OutputNode, out var node))
        {
            node.Enabled = false;
            if (node.NodeGroup != null)
                _nodeGroup.QueueReflood(node);
        }
    }

    // The input-side PowerConsumer is the mains sensor. The terminal port
    // isolates input from the tile's HV run, so the discharger can never
    // re-power the net it senses and the state machine cannot oscillate.
    private void OnReceivedChanged(Entity<CMUAutoTransferSwitchComponent> ent, ref PowerConsumerReceivedChanged args)
    {
        if (args.ReceivedPower > 0f)
        {
            ent.Comp.RevertEnd = _timing.CurTime + ent.Comp.RevertDelay;
            if (ent.Comp.State == CMUAtsState.Cranking)
                SetState(ent, CMUAtsState.Standby);
        }
        else
        {
            ent.Comp.RevertEnd = TimeSpan.Zero;
            if (ent.Comp.State != CMUAtsState.Standby)
                return;
            ent.Comp.CrankEnd = _timing.CurTime + ent.Comp.CrankDelay;
            SetState(ent, CMUAtsState.Cranking);
            _audio.PlayPvs(ent.Comp.CrankSound, ent);
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUAutoTransferSwitchComponent>();
        while (query.MoveNext(out var uid, out var ats))
        {
            switch (ats.State)
            {
                case CMUAtsState.Cranking when now >= ats.CrankEnd:
                    SetState((uid, ats), CMUAtsState.Online);
                    _audio.PlayPvs(ats.OnlineSound, uid);
                    break;
                case CMUAtsState.Online when ats.RevertEnd != TimeSpan.Zero && now >= ats.RevertEnd:
                    SetState((uid, ats), CMUAtsState.Standby);
                    break;
            }
        }
    }

    private void SetState(Entity<CMUAutoTransferSwitchComponent> ent, CMUAtsState state)
    {
        if (ent.Comp.State == state)
            return;
        ent.Comp.State = state;
        Dirty(ent);
        _appearance.SetData(ent, CMUAtsVisuals.State, state);
        if (_nodeContainer.TryGetNode<CableDeviceNode>(ent.Owner, ent.Comp.OutputNode, out var node))
        {
            node.Enabled = state == CMUAtsState.Online;
            if (node.NodeGroup != null)
                _nodeGroup.QueueReflood(node);
        }
    }

    private void OnExamined(Entity<CMUAutoTransferSwitchComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        var state = Loc.GetString($"cmu-ats-state-{ent.Comp.State.ToString().ToLowerInvariant()}");
        args.PushMarkup(Loc.GetString("cmu-ats-examine", ("state", state)));
    }
}
