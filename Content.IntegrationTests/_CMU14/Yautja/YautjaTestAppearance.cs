using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;

namespace Content.IntegrationTests.CMU14.Yautja;

internal static class YautjaTestAppearance
{
    public static void Apply(IEntityManager entities, EntityUid body, HumanoidCharacterProfile profile)
    {
        entities.System<HumanoidProfileSystem>().ApplyProfileTo(body, profile);
        entities.System<SharedVisualBodySystem>().ApplyProfileTo(body, profile);
    }

    public static Color SkinColor(IEntityManager entities, EntityUid body) => Head(entities, body).SkinColor;
    public static Color EyeColor(IEntityManager entities, EntityUid body) => Head(entities, body).EyeColor;

    private static OrganProfileData Head(IEntityManager entities, EntityUid body)
    {
        Assert.That(entities.System<SharedVisualBodySystem>().TryGatherMarkingsData(
            body, null, out var profiles, out _, out _), Is.True);
        Assert.That(profiles.ContainsKey("Head"), Is.True);
        return profiles["Head"];
    }

    public static List<Marking> Hair(IEntityManager entities, EntityUid body)
    {
        Assert.That(entities.System<SharedVisualBodySystem>().TryGatherMarkingsData(
            body, [HumanoidVisualLayers.Hair], out _, out _, out var markings), Is.True);
        return markings["Head"][HumanoidVisualLayers.Hair];
    }
}
