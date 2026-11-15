using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Yautja;
using Content.Client.CMU14.Yautja;
using Content.Client.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Content.Server.Database;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaCharacterProfileTest
{

    public void NewSkinPresetRoundTripsThroughAppearance(
        string enumName,
        byte red,
        byte green,
        byte blue,
        string displayName)
    {
        var skinColor = Enum.Parse<YautjaSkinColor>(enumName);
        var expected = new Color(red, green, blue);
        var profile = YautjaCharacterProfile.Default.WithSkinColor(skinColor);
        var quills = YautjaCharacterProfile.GetQuillMarkings(profile.Appearance).Single(marking => marking.MarkingId == profile.QuillMarkingId);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaCharacterProfile.SkinColorOrder, Does.Contain(skinColor));
            Assert.That(YautjaCharacterProfile.GetSkinColorDisplayName(skinColor), Is.EqualTo(displayName));
            Assert.That(YautjaCharacterProfile.GetSkinColorColor(skinColor), Is.EqualTo(expected));
            Assert.That(profile.SkinColor, Is.EqualTo(skinColor));
            Assert.That(profile.Appearance.SkinColor, Is.EqualTo(expected));
            Assert.That(YautjaCharacterProfile.GetQuillColor(profile.Appearance), Is.EqualTo(expected));
            Assert.That(quills.MarkingColors.Single(), Is.EqualTo(expected));
        });
    }

    [Test]
    public void ProfileSanitizerClearsUnauthorizedSetsAndPreservesAuthorizedSet()
    {
        var profile = YautjaCharacterProfile.Default
            .WithLegacy(YautjaLegacySet.Dragon)
            .WithUnique(YautjaUniqueSet.Anubys);

        var sanitized = profile.SanitizeForCapabilities(
            new YautjaProfileCapabilities(YautjaRank.Blooded, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(sanitized.Legacy, Is.EqualTo(YautjaLegacySet.None));
            Assert.That(sanitized.Unique, Is.EqualTo(YautjaUniqueSet.None));
        });

        // Dragon is a clan leader set, Anubys a legacy whitelist one
        var leaderLegacy = YautjaCharacterProfile.Default
            .WithLegacy(YautjaLegacySet.Dragon)
            .SanitizeForCapabilities(new YautjaProfileCapabilities(YautjaRank.Leader, true, false));
        var whitelistedUnique = YautjaCharacterProfile.Default
            .WithUnique(YautjaUniqueSet.Anubys)
            .SanitizeForCapabilities(new YautjaProfileCapabilities(YautjaRank.Blooded, false, true));

        Assert.Multiple(() =>
        {
            Assert.That(leaderLegacy.Legacy, Is.EqualTo(YautjaLegacySet.Dragon));
            Assert.That(whitelistedUnique.Unique, Is.EqualTo(YautjaUniqueSet.Anubys));
        });
    }

    [Test]
    public async Task AppliedProfileUsesEffectiveSelectedStatusForEntityRank()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var profileApply = entMan.System<YautjaProfileApplySystem>();
            var capabilities = new YautjaProfileCapabilities(
                YautjaRank.Ancient,
                true,
                false,
                canUseCouncilStatus: true,
                canUseLeaderStatus: true);
            var normal = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var council = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);

            try
            {
                profileApply.ApplyProfile(
                    normal,
                    YautjaCharacterProfile.Default.WithStatus(YautjaProfileStatus.Normal),
                    authoritativeCapabilities: capabilities);
                profileApply.ApplyProfile(
                    council,
                    YautjaCharacterProfile.Default.WithStatus(YautjaProfileStatus.Council),
                    authoritativeCapabilities: capabilities);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        entMan.GetComponent<YautjaComponent>(normal).ClanRank,
                        Is.EqualTo(YautjaRank.Blooded));
                    Assert.That(
                        entMan.GetComponent<YautjaComponent>(council).ClanRank,
                        Is.EqualTo(YautjaRank.Ancient));
                });
            }
            finally
            {
                entMan.DeleteEntity(normal);
                entMan.DeleteEntity(council);
            }
        });

        await pair.CleanReturnAsync();
    }

    // CM-SS13 has six predator skins: code/__DEFINES/pred.dm:11 PRED_SKIN_COLOR = tan, green, purple, blue, red,
    // black. The colours themselves are not constants - /datum/pred_picker samples the centre pixel of each
    // "[colour]_torso_pred_m" state in the species icobase (icons/mob/humans/species/r_predator.dmi). These are
    // those samples, so the picker has to offer the reference skins first, in reference order, with reference colour.

}
