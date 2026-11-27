using Content.Shared.CCVar;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.DetailExaminable;
using Content.Shared.Preferences;

namespace Content.Server.Station.Systems;

public sealed partial class StationSpawningSystem
{
    /// <summary>Apply the winner's identity to the already equipped call-in body before mind creation.</summary>
    public void ApplyCallInIdentity(EntityUid mob, HumanoidCharacterProfile profile, string? job)
    {
        _visualBody.ApplyProfileTo(mob, profile);
        _humanoidProfile.ApplyProfileTo(mob, profile);
        _metaSystem.SetEntityName(mob, profile.Name);
        ApplyProfileBloodType(mob, profile);
        ApplyRegulationAppearance(mob, profile);

        if (_configurationManager.GetCVar(CCVars.CharacterDescription))
        {
            RemComp<CharacterDescriptionComponent>(mob);
            BakeCharacterDescription(mob, profile, job, appliedTraits: []);
        }
        if (_configurationManager.GetCVar(CCVars.FlavorText) && profile.FlavorText.Length > 0)
        {
            var detail = EnsureComp<DetailExaminableComponent>(mob);
            detail.Content = profile.FlavorText;
            Dirty(mob, detail);
        }
        if (_cardSystem.TryFindIdCard(mob, out var id))
            _cardSystem.TryChangeFullName(id.Owner, profile.Name, id.Comp);
        _identity.QueueIdentityUpdate(mob);
    }
}
