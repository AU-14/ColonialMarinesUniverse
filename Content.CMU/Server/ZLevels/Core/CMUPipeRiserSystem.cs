using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed class CMUPipeRiserSystem : EntitySystem
{
    private const string PryingQuality = "Prying";

    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly CMUZPairingSystem _zPairing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPipeRiserComponent, CMUZPairedEvent>(OnPaired);
        SubscribeLocalEvent<CMUPipeRiserComponent, CMUZUnpairedEvent>(OnUnpaired);
        SubscribeLocalEvent<CMUPipeRiserComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUPipeRiserComponent, ExaminedEvent>(OnExamined);
    }

    private void OnPaired(Entity<CMUPipeRiserComponent> ent, ref CMUZPairedEvent args)
    {
        if (_nodeContainer.TryGetNode<PipeNode>(ent.Owner, ent.Comp.NodeName, out var node)
            && _nodeContainer.TryGetNode<PipeNode>(args.Twin, ent.Comp.NodeName, out var twin))
        {
            node.AddAlwaysReachable(twin);
            twin.AddAlwaysReachable(node);
        }
    }

    private void OnUnpaired(Entity<CMUPipeRiserComponent> ent, ref CMUZUnpairedEvent args)
    {
        if (_nodeContainer.TryGetNode<PipeNode>(ent.Owner, ent.Comp.NodeName, out var node)
            && _nodeContainer.TryGetNode<PipeNode>(args.FormerTwin, ent.Comp.NodeName, out var twin))
        {
            node.RemoveAlwaysReachable(twin);
            twin.RemoveAlwaysReachable(node);
        }
    }

    private void OnInteractUsing(Entity<CMUPipeRiserComponent> ent, ref InteractUsingEvent args)
    {
        if (TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open)
            return;

        if (!_tool.HasQuality(args.Used, PryingQuality))
            return;

        if (!TryComp<CMUZPairedComponent>(ent, out var paired))
            return;

        args.Handled = true;

        paired.Offset = -paired.Offset;
        Dirty(ent.Owner, paired);

        var direction = Loc.GetString(paired.Offset > 0 ? "cmu-z-direction-above" : "cmu-z-direction-below");
        _popup.PopupClient(Loc.GetString("cmu-pipe-riser-dir-toggled", ("direction", direction)), ent, args.User);

        _zPairing.Unpair((ent.Owner, paired));
        _zPairing.TryPair((ent.Owner, paired));
    }

    private void OnExamined(Entity<CMUPipeRiserComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryComp<CMUZPairedComponent>(ent, out var paired))
            return;

        var above = paired.Offset > 0;
        args.PushMarkup(Loc.GetString(
            paired.Twin != null ? "cmu-pipe-riser-linked" : "cmu-pipe-riser-unlinked",
            ("direction", Loc.GetString(above ? "cmu-z-direction-above" : "cmu-z-direction-below"))));
    }
}
