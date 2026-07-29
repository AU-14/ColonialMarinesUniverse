using Content.Server._CMU14.CharacterDescription;
using Content.Server.EUI;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._CMU14.CharacterDescription;
using Content.Shared.Eui;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.CharacterDescription.UI;

[UsedImplicitly]
public sealed class DetailedExamineEui : BaseEui
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly RMCReagentSystem _reagentSystem;
    private readonly NetEntity _target;

    public DetailedExamineEui(NetEntity target)
    {
        _target = target;
        IoCManager.InjectDependencies(this);
        _reagentSystem = _entManager.System<RMCReagentSystem>();
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        var state = new DetailedExamineEuiState { Target = _target };

        var uid = _entManager.GetEntity(_target);
        if (!_entManager.EntityExists(uid))
            return state;

        state.Name = _entManager.GetComponent<MetaDataComponent>(uid).EntityName;

        if (_entManager.TryGetComponent(uid, out CharacterDescriptionComponent? desc))
        {
            state.FullDescription = desc.FullDescription;
            state.Height = desc.Height;
            state.Weight = desc.Weight;
            state.Age = desc.Age;
            state.Build = Loc.GetString($"build-type-{desc.Build.ToString().ToLowerInvariant()}");

            if (!desc.HideMetaInformation)
            {
                state.MedicalRecord = MedicalRecordBuilder.Build(_entManager, _reagentSystem, uid, desc);
                state.CriminalRecord = desc.CriminalRecord;
                state.GeneralRecord = desc.GeneralRecord;

                if (desc.Origin is { } origin && _prototypeManager.TryIndex(origin, out var originProto))
                    state.Origin = Loc.GetString(originProto.Name);

                if (desc.Allegiance is { } allegiance && _prototypeManager.TryIndex(allegiance, out var allegianceProto))
                    state.Allegiance = Loc.GetString(allegianceProto.Name);
            }
        }

        if (_entManager.TryGetComponent(uid, out HumanoidAppearanceComponent? humanoid))
        {
            state.SkinToneName = NamedColorHelper.NearestColorName(humanoid.SkinColor);
            state.EyeColorName = NamedColorHelper.NearestColorName(humanoid.EyeColor);

            if (humanoid.MarkingSet.TryGetCategory(MarkingCategories.Hair, out var hairMarkings) &&
                hairMarkings.Count > 0 && hairMarkings[0].MarkingColors.Count > 0)
            {
                state.HairColorName = NamedColorHelper.NearestColorName(hairMarkings[0].MarkingColors[0]);
            }
        }

        return state;
    }
}
