// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._RMC14.Medical.CPR;
using Content.Shared.CMU14.Medical.Autocompressor;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Medical.Autocompressor;

public sealed partial class CMUAutocompressorVisualsSystem
{
    private const string TrainingLayer = "cmu-cpr-training-compressor";
    private static readonly ResPath TrainingSprite = new("CMU14/Objects/Medical/autocompressor.rsi");
    private readonly HashSet<EntityUid> _trainingDummies = new();

    private void InitializeTraining()
    {
        SubscribeLocalEvent<CMUAutocompressorPatientComponent, ComponentStartup>(OnTrainingPatientStartup);
        SubscribeLocalEvent<CMUAutocompressorPatientComponent, ComponentRemove>(OnTrainingPatientRemoved);
        SubscribeLocalEvent<CMUAutocompressorPatientComponent, AfterAutoHandleStateEvent>(OnTrainingPatientState);
    }

    private void OnTrainingPatientStartup(Entity<CMUAutocompressorPatientComponent> ent, ref ComponentStartup args)
        => QueueTrainingDummy(ent.Owner);

    private void OnTrainingPatientState(Entity<CMUAutocompressorPatientComponent> ent, ref AfterAutoHandleStateEvent args)
        => QueueTrainingDummy(ent.Owner);

    private void OnTrainingPatientRemoved(Entity<CMUAutocompressorPatientComponent> ent, ref ComponentRemove args)
    {
        _trainingDummies.Remove(ent.Owner);
        if (HasComp<CPRDummyComponent>(ent) && HasComp<SpriteComponent>(ent))
            _sprite.RemoveLayer(ent.Owner, TrainingLayer, logMissing: false);
    }

    private void QueueTrainingDummy(EntityUid dummy)
    {
        if (!TerminatingOrDeleted(dummy) && HasComp<CPRDummyComponent>(dummy))
            _trainingDummies.Add(dummy);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var dummy in _trainingDummies)
            UpdateTrainingDummy(dummy);
        _trainingDummies.Clear();
    }

    private void UpdateTrainingDummy(EntityUid dummy)
    {
        if (TerminatingOrDeleted(dummy) || !TryComp<SpriteComponent>(dummy, out var sprite))
            return;
        Entity<SpriteComponent?> body = (dummy, sprite);
        if (!TryComp<CMUAutocompressorPatientComponent>(dummy, out var patient)
            || patient.Device == null)
        {
            _sprite.RemoveLayer(body, TrainingLayer, logMissing: false);
            return;
        }

        var state = patient.Running ? "training-running" : "training-idle";
        if (_sprite.LayerMapTryGet(body, TrainingLayer, out var layer, false))
            _sprite.LayerSetRsiState(body, layer, state);
        else
        {
            layer = _sprite.AddLayer(body, new SpriteSpecifier.Rsi(TrainingSprite, state));
            _sprite.LayerMapSet(body, TrainingLayer, layer);
        }
    }
}
