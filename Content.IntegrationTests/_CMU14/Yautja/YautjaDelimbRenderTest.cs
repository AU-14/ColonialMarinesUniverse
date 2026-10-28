using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

// CMU14 Test: delimbing and regrowing must stay safe with the pre-coloured variant selector.
//
// The selector runs per organ visual. Delimbing removes the organ and its layer, so the renderer must simply stop drawing
// it - no exception, no fallback to a greyscale base - and a replaced organ must pick up the tone-specific state again.
[TestFixture]
public sealed class YautjaDelimbRenderTest
{
    private static EntityUid? FindPart(IEntityManager entMan, EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        var bodySystem = entMan.System<SharedBodySystem>();
        foreach (var (partUid, part) in bodySystem.GetBodyChildren(body))
        {
            if (part.PartType == type && part.Symmetry == symmetry)
                return partUid;
        }

        return null;
    }

    [Test]
    public void SelectorHandlesEveryBodyLayerForBothSexes()
    {
        Assert.Multiple(() =>
        {
            foreach (var sex in new[] { Sex.Male, Sex.Female })
            {
                Assert.That(YautjaBodyVariants.StateFor(HumanoidVisualLayers.Head, sex, "red"), Is.Not.Null,
                    $"head must resolve for {sex}");
                Assert.That(YautjaBodyVariants.StateFor(HumanoidVisualLayers.Chest, sex, "red"), Is.Not.Null,
                    $"torso must resolve for {sex}");

                foreach (var layer in new[]
                         {
                             HumanoidVisualLayers.LArm, HumanoidVisualLayers.RArm,
                             HumanoidVisualLayers.LHand, HumanoidVisualLayers.RHand,
                             HumanoidVisualLayers.LLeg, HumanoidVisualLayers.RLeg,
                             HumanoidVisualLayers.LFoot, HumanoidVisualLayers.RFoot,
                         })
                {
                    Assert.That(YautjaBodyVariants.StateFor(layer, sex, "red"), Is.EqualTo($"{Name(layer)}_red"));
                }
            }

            // Layers that are not part of the pre-coloured sheet must stay untouched.
            Assert.That(YautjaBodyVariants.StateFor(HumanoidVisualLayers.Eyes, Sex.Male, "red"), Is.Null,
                "the eyes layer must not be redirected");
            Assert.That(YautjaBodyVariants.StateFor(HumanoidVisualLayers.Hair, Sex.Male, "red"), Is.Null,
                "the hair/dreadlock layer must not be redirected");
        });

        static string Name(HumanoidVisualLayers layer) => layer switch
        {
            HumanoidVisualLayers.LArm => "l_arm",
            HumanoidVisualLayers.RArm => "r_arm",
            HumanoidVisualLayers.LHand => "l_hand",
            HumanoidVisualLayers.RHand => "r_hand",
            HumanoidVisualLayers.LLeg => "l_leg",
            HumanoidVisualLayers.RLeg => "r_leg",
            HumanoidVisualLayers.LFoot => "l_foot",
            HumanoidVisualLayers.RFoot => "r_foot",
            _ => throw new ArgumentOutOfRangeException(nameof(layer)),
        };
    }

    [Test]
    public async Task DelimbingAndRegrowingALimbStaysSafe()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid hunter = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var map = entMan.System<SharedMapSystem>().CreateMap();
            hunter = entMan.SpawnEntity("CMUMobYautja", new EntityCoordinates(map, default));
            entMan.System<Content.Server.CMU14.Yautja.YautjaProfileApplySystem>()
                .ApplyProfile(hunter, new YautjaCharacterProfile().WithSkinColor(YautjaSkinColor.Red), equipProfileGear: false);
        });

        await pair.RunTicksSync(10);

        // Sever the left arm through the real severance event, then let the body react.
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var arm = FindPart(entMan, hunter, BodyPartType.Arm, BodyPartSymmetry.Left);
            Assert.That(arm, Is.Not.Null, "the hunter must have a left arm to sever");

            var ev = new BodyPartSeverAttemptEvent(hunter, arm!.Value, BodyPartType.Arm, Surgical: true);
            entMan.EventBus.RaiseLocalEvent(arm.Value, ref ev);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(FindPart(entMan, hunter, BodyPartType.Arm, BodyPartSymmetry.Left), Is.Null,
                    "the severed arm must be gone from the body");
                Assert.That(FindPart(entMan, hunter, BodyPartType.Arm, BodyPartSymmetry.Right), Is.Not.Null,
                    "the other arm must survive");
                Assert.That(FindPart(entMan, hunter, BodyPartType.Head, BodyPartSymmetry.None), Is.Not.Null,
                    "the head must survive a delimbing");
                Assert.That(entMan.HasComponent<HumanoidProfileComponent>(hunter), Is.True,
                    "the hunter must keep its profile for the remaining parts to render");
            });
        });

        // Regrow/replace: a fresh arm organ must carry a visual organ pointed at the Yautja sheet, so the selector can
        // give it the tone-specific state as soon as it is attached.
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var torso = FindPart(entMan, hunter, BodyPartType.Torso, BodyPartSymmetry.None);
            Assert.That(torso, Is.Not.Null, "the hunter must still have a torso");

            var replacement = entMan.SpawnEntity("OrganYautjaArmLeft",
                entMan.GetComponent<TransformComponent>(torso!.Value).Coordinates);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<VisualOrganComponent>(replacement), Is.True,
                    "a replacement limb needs a visual organ");
                Assert.That(entMan.GetComponent<VisualOrganComponent>(replacement).Data.RsiPath, Does.Contain("Yautja/body.rsi"),
                    "the replacement limb must draw from the Yautja sheet so it gets the tone-specific state");
            });
        });

        await pair.CleanReturnAsync();
    }
}
