using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Species;

[TestFixture]
[TestOf(typeof(SharedVisualBodySystem))]
public sealed class RoboticLimbVisualOrganTest : GameTest
{
    private const string PartsPrototypeRsi = "CMU14/Mobs/DroneAndroid/parts.rsi";
    private static readonly ResPath PartsRsi = new("/Textures/CMU14/Mobs/DroneAndroid/parts.rsi");

    [SidedDependency(Side.Server)] private SharedBodySystem _body = default!;
    [SidedDependency(Side.Client)] private SpriteSystem _sprites = default!;

    [Test]
    public async Task RoboticHandVisualFollowsOrganInsertionAndRemoval()
    {
        var map = await Pair.CreateTestMap();
        EntityUid body = default;
        EntityUid arm = default;
        EntityUid hand = default;
        NetEntity bodyNet = default;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnEntity("CMUDroneAndroid", map.GridCoords);
            bodyNet = SEntMan.GetNetEntity(body);
            arm = FindOrgan(body, "ArmLeft");
            hand = FindOrgan(body, "HandLeft");

            var visual = SEntMan.GetComponent<VisualOrganComponent>(hand);
            Assert.Multiple(() =>
            {
                Assert.That(visual.Layer, Is.EqualTo(HumanoidVisualLayers.LHand));
                Assert.That(visual.Data.RsiPath, Is.EqualTo(PartsPrototypeRsi));
                Assert.That(visual.Data.State, Is.EqualTo("l_hand"));
                Assert.That(SEntMan.HasComponent<CMURoboticLimbComponent>(hand), Is.True);
                Assert.That(SEntMan.ComponentFactory.TryGetRegistration("CMURoboticLimbOverlay", out _), Is.False,
                    "the legacy CustomBaseLayers tracker must not coexist with VisualOrgan ownership");
            });
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() => AssertHandLayer(bodyNet, "l_hand"));

        await Server.WaitAssertion(() =>
        {
            Assert.That(_body.RemoveOrgan(hand), Is.True);
            Assert.That(SEntMan.GetComponent<OrganComponent>(hand).Body, Is.Null);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() => AssertHandLayer(bodyNet, null));

        await Server.WaitAssertion(() =>
        {
            Assert.That(_body.AttachPart(arm, "left_hand", hand), Is.True);
            Assert.That(SEntMan.GetComponent<OrganComponent>(hand).Body, Is.EqualTo(body));
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() => AssertHandLayer(bodyNet, "l_hand"));

        // GameTest cleanup owns the replicated body hierarchy. Manually recursively deleting it here
        // exercises RobustToolbox parent/child deletion rather than this visual lifecycle contract.
    }

    private EntityUid FindOrgan(EntityUid body, string category)
    {
        var component = SEntMan.GetComponent<BodyComponent>(body);
        Assert.That(component.Organs, Is.Not.Null);

        foreach (var organ in component.Organs!.ContainedEntities)
        {
            if (SEntMan.GetComponent<OrganComponent>(organ).Category?.Id == category)
                return organ;
        }

        Assert.Fail($"{body} is missing organ category {category}");
        return default;
    }

    private void AssertHandLayer(NetEntity bodyNet, string? expectedState)
    {
        var body = CEntMan.GetEntity(bodyNet);
        var sprite = CEntMan.GetComponent<SpriteComponent>(body);
        Assert.That(_sprites.LayerMapTryGet((body, sprite), HumanoidVisualLayers.LHand, out var layer, false), Is.True);

        var state = _sprites.LayerGetRsiState((body, sprite), layer);
        if (expectedState is null)
        {
            Assert.That(state, Is.EqualTo(RSI.StateId.Invalid),
                "removing the VisualOrgan must clear its owned layer");
            return;
        }

        Assert.Multiple(() =>
        {
            Assert.That(state.Name, Is.EqualTo(expectedState));
            Assert.That(sprite[layer].ActualRsi?.Path, Is.EqualTo(PartsRsi));
        });
    }
}
