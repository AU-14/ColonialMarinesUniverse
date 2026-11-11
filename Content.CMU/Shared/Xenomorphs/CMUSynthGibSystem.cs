using System.Linq;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Chat.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Xenomorphs;

public sealed class CMUSynthGibSystem : EntitySystem
{
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private INetManager _net = default!;

    private static readonly ProtoId<EmotePrototype> Scream = "Scream";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SynthComponent, XenoGibTargetAttemptEvent>(OnSynthGibAttempt);
    }

    private void OnSynthGibAttempt(Entity<SynthComponent> ent, ref XenoGibTargetAttemptEvent args)
    {
        if (_net.IsClient
            || args.Handled
            || !HasComp<CMUHumanMedicalComponent>(ent.Owner)
            || !TryComp<MobStateComponent>(ent.Owner, out var mobState)
            || mobState.CurrentState == MobState.Dead)
            return;

        EntityUid? torso = null;
        BodyPartHealthComponent? torsoHealth = null;
        foreach (var (part, _) in _body.GetBodyChildrenOfType(ent.Owner, BodyPartType.Torso))
        {
            if (!TryComp<BodyPartHealthComponent>(part, out torsoHealth))
                continue;

            torso = part;
            break;
        }

        if (torso is not { } torsoPart || torsoHealth is null)
            return;

        foreach (var (part, anatomy) in _body.GetBodyChildrenOfType(ent.Owner, BodyPartType.Leg).ToList())
        {
            var sever = new BodyPartSeverAttemptEvent(ent.Owner, part, anatomy.PartType);
            RaiseLocalEvent(part, ref sever, broadcast: true);
            if (!sever.Succeeded)
                return;
        }

        _partHealth.SetCurrent((torsoPart, torsoHealth), FixedPoint2.Zero);

        args.Handled = true;
        _emote.TryEmoteWithChat(ent.Owner, Scream, ignoreActionBlocker: true, forceEmote: true);
    }
}
