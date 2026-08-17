using System.Linq;
using Content.Shared._CMU14.Ops.Sfx;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.OrbitalCannon;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Ops.Sfx;

public sealed partial class EvacuationSequenceSystem : EntitySystem
{
    [Dependency] private OrbitalCannonSystem _orbitalCannon = default!;
    [Dependency] private ScriptedSoundSystem _scriptedSound = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private static readonly ProtoId<ScriptedSoundSequencePrototype> SelfDestructSequence = "SelfDestructSequence";
    private static readonly ProtoId<ScriptedSoundSequencePrototype> SelfDestructEngineSequence = "SelfDestructEngineSequence";
    private static readonly EntProtoId SelfDestructWarhead = "CMUSelfDestructWarheadExplosion";

    public override void Initialize()
    {
        SubscribeLocalEvent<EvacuationEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<EvacuationDisabledEvent>(OnDisabled);
        SubscribeLocalEvent<ShipSelfDestructEvent>(OnSelfDestruct);
        Subs.CVar(_cfg, CCVars.EnableEvacSfx, OnCVarChanged);
    }

    private void OnEnabled(ref EvacuationEnabledEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.EnableEvacSfx)) return;

        if (!_scriptedSound.TryGetActiveSequence(SelfDestructSequence, ev.Map, out _))
            _scriptedSound.StartSequence(SelfDestructSequence, ev.Map);

        if (!_scriptedSound.TryGetActiveSequence(SelfDestructEngineSequence, ev.Map, out _))
            _scriptedSound.StartSequence(SelfDestructEngineSequence, ev.Map);
    }

    private void OnDisabled(ref EvacuationDisabledEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.EnableEvacSfx)) return;

        if (_scriptedSound.TryGetActiveSequence(SelfDestructSequence, ev.Map, out var seq))
            _scriptedSound.StopSequence(seq);

        if (_scriptedSound.TryGetActiveSequence(SelfDestructEngineSequence, ev.Map, out var engineSeq))
            _scriptedSound.StopSequence(engineSeq);
    }

    private void OnSelfDestruct(ref ShipSelfDestructEvent ev)
        => _orbitalCannon.SpawnExplosion(SelfDestructWarhead, Transform(ev.Map).Coordinates);

    private void OnCVarChanged(bool enabled)
    {
        if (enabled) return;
        foreach (var (uid, comp) in _scriptedSound.GetActiveSequences().ToList())
        {
            if (comp.SequenceId == SelfDestructSequence || comp.SequenceId == SelfDestructEngineSequence)
                _scriptedSound.StopSequence(uid);
        }
    }
}
