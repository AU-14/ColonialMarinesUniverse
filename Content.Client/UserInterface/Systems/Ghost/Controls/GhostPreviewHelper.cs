using System.Linq;
using Content.Client.Lobby;
using Content.Shared.Body;
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
    private static readonly ProtoId<OrganCategoryPrototype> Head = "Head";
    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";

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

        var visualBody = entityManager.System<SharedVisualBodySystem>();
        if (!entityManager.TryGetComponent(source, out HumanoidProfileComponent? humanoid) ||
            !visualBody.TryGatherMarkingsData(source, null, out var organProfiles, out _, out var organMarkings) ||
            organProfiles.Count == 0)
        {
            return HumanoidCharacterProfile.DefaultWithSpecies()
                .WithName(name);
        }

        var organProfile = SelectOrganProfile(organProfiles);
        var appearance = new HumanoidCharacterAppearance(
            organProfile.EyeColor,
            organProfile.SkinColor,
            organMarkings.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToDictionary(
                    inner => inner.Key,
                    inner => inner.Value.Select(marking => new Marking(marking.MarkingId, marking.MarkingColors)
                    {
                        Forced = marking.Forced,
                    }).ToList())));

        return HumanoidCharacterProfile.DefaultWithSpecies(humanoid.Species)
            .WithName(name)
            .WithAge(humanoid.Age)
            .WithSex(humanoid.Sex)
            .WithGender(humanoid.Gender)
            .WithVoice(humanoid.Voice)
            .WithCharacterAppearance(appearance);
    }

    internal static OrganProfileData SelectOrganProfile(
        IReadOnlyDictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData> organProfiles)
    {
        return organProfiles.TryGetValue(Head, out var head)
            ? head
            : organProfiles.TryGetValue(Torso, out var torso)
                ? torso
                : organProfiles.OrderBy(pair => pair.Key.Id, StringComparer.Ordinal).First().Value;
    }
}
