using System.Linq;
using Content.Client.Lobby;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.UserInterface.Systems.Ghost.Controls;

internal static class GhostPreviewHelper
{
    public static bool CanUseLiveSprite(
        IEntityManager entityManager,
        IPlayerManager playerManager,
        EntityUid target)
    {
        if (playerManager.LocalEntity is not { } local)
            return false;

        if (!entityManager.TryGetComponent(local, out TransformComponent? localXform) ||
            !entityManager.TryGetComponent(target, out TransformComponent? targetXform))
        {
            return false;
        }

        return localXform.MapID == targetXform.MapID;
    }

    public static bool TryCreateJobPreviewDummy(
        IUserInterfaceManager uiManager,
        IPrototypeManager prototypeManager,
        IEntityManager entityManager,
        EntityUid source,
        string? jobPrototype,
        string fallbackName,
        out EntityUid dummy)
    {
        dummy = EntityUid.Invalid;

        if (string.IsNullOrWhiteSpace(jobPrototype))
            return false;

        var jobId = new ProtoId<JobPrototype>(jobPrototype);
        if (!prototypeManager.TryIndex(jobId, out JobPrototype? job))
            return false;

        if (job.JobPreviewEntity == null &&
            job.JobEntity == null &&
            job.StartingGear == null &&
            job.DummyStartingGear == null)
        {
            return false;
        }

        var profile = CreateProfileFromEntity(entityManager, source, fallbackName);
        dummy = uiManager.GetUIController<LobbyUIController>().LoadProfileEntity(profile, job, true);
        return dummy.Valid;
    }

    private static HumanoidCharacterProfile CreateProfileFromEntity(
        IEntityManager entityManager,
        EntityUid source,
        string fallbackName)
    {
        var name = string.IsNullOrWhiteSpace(fallbackName)
            ? "Unknown"
            : fallbackName;

        if (!entityManager.TryGetComponent(source, out HumanoidAppearanceComponent? humanoid))
        {
            return HumanoidCharacterProfile.DefaultWithSpecies()
                .WithName(name);
        }

        return HumanoidCharacterProfile.DefaultWithSpecies(humanoid.Species)
            .WithName(name)
            .WithAge(humanoid.Age)
            .WithSex(humanoid.Sex)
            .WithGender(humanoid.Gender)
            .WithCharacterAppearance(CreateAppearance(humanoid));
    }

    private static HumanoidCharacterAppearance CreateAppearance(HumanoidAppearanceComponent humanoid)
    {
        var defaults = HumanoidCharacterAppearance.DefaultWithSpecies(humanoid.Species);
        var hair = GetFirstMarking(humanoid.MarkingSet, MarkingCategories.Hair);
        var facialHair = GetFirstMarking(humanoid.MarkingSet, MarkingCategories.FacialHair);
        var markings = humanoid.MarkingSet.Markings
            .Where(pair => pair.Key != MarkingCategories.Hair &&
                           pair.Key != MarkingCategories.FacialHair)
            .SelectMany(pair => pair.Value.Select(marking => new Marking(marking)))
            .ToList();

        return new HumanoidCharacterAppearance(
            hair?.MarkingId ?? defaults.HairStyleId,
            GetMarkingColor(hair, humanoid.CachedHairColor ?? defaults.HairColor),
            facialHair?.MarkingId ?? defaults.FacialHairStyleId,
            GetMarkingColor(facialHair, humanoid.CachedFacialHairColor ?? defaults.FacialHairColor),
            humanoid.EyeColor,
            humanoid.SkinColor,
            markings);
    }

    private static Marking? GetFirstMarking(MarkingSet markingSet, MarkingCategories category)
    {
        return markingSet.TryGetCategory(category, out var markings) && markings.Count > 0
            ? markings[0]
            : null;
    }

    private static Color GetMarkingColor(Marking? marking, Color fallback)
    {
        return marking is { MarkingColors.Count: > 0 }
            ? marking.MarkingColors[0]
            : fallback;
    }
}
