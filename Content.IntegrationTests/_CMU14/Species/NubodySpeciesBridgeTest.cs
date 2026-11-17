#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Humanoid;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.NewPlayer;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared._RMC14.Visor;
using Content.Shared._RMC14.Webbing;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Throwing;
using Robust.Shared;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Species;

[TestFixture]
[TestOf(typeof(SharedVisualBodySystem))]
public sealed class NubodySpeciesBridgeTest : GameTest
{
    private static readonly ResPath HumanPartsRsi = new("/Textures/Mobs/Species/Human/parts.rsi");

    [SidedDependency(Side.Server)] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [SidedDependency(Side.Server)] private SharedBodyPartHealthSystem _partHealth = default!;

    private SpriteSystem Sprites => Client.System<SpriteSystem>();

    [Test]
    public async Task SeveredCmuArmIsVisibleOnThrownCarrierClientside()
    {
        var map = await Pair.CreateTestMap();
        NetEntity carrierNet = default;
        EntityUid human = default;
        EntityUid gloves = default;
        EntityUid legHuman = default;
        EntityUid shoes = default;
        EntityUid headHuman = default;
        var headEquipment = new Dictionary<string, EntityUid>();

        await Server.WaitAssertion(() =>
        {
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            gloves = SEntMan.SpawnEntity("ClothingHandsGlovesCombat", map.GridCoords);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(human, gloves, "gloves"), Is.True);
            Assert.That(_medicalIndex.TryGetBodyPart(
                human,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                out var arm),
                Is.True);

            var damage = new Content.Shared.Damage.DamageSpecifier();
            damage.DamageDict["Slash"] = 1000;
            Assert.That(_partHealth.TryApplyPartDamage(human, arm, damage), Is.True);

            var carrier = SEntMan.QueryEntities<MetaDataComponent>()
                .Select(meta => meta.Owner)
                .Single(uid =>
                    SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "DetachedBody" &&
                    SEntMan.System<Content.Shared.Body.Systems.SharedBodySystem>()
                        .GetRootPartOrNull(uid)?.Entity == arm);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(carrier).EntityName, Is.EqualTo("left arm"));
                Assert.That(SEntMan.HasComponent<PhysicsComponent>(carrier), Is.True,
                    "detached body carriers need item physics or TryThrow rejects them");
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(carrier), Is.True,
                    "severed limb carrier should immediately enter the throwing state");
            });
            carrierNet = SEntMan.GetNetEntity(carrier);

            legHuman = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            shoes = SEntMan.SpawnEntity("ClothingShoesBootsCombat", map.GridCoords);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(legHuman, shoes, "shoes"), Is.True);
            Assert.That(_medicalIndex.TryGetBodyPart(
                legHuman,
                new CMUMedicalBodyPartKey(BodyPartType.Leg, BodyPartSymmetry.Left),
                out var leg),
                Is.True);
            var legDamage = new Content.Shared.Damage.DamageSpecifier();
            legDamage.DamageDict["Slash"] = 1000;
            Assert.That(_partHealth.TryApplyPartDamage(legHuman, leg, legDamage), Is.True);

            headHuman = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            foreach (var (slot, prototype) in new[]
                     {
                         ("ears", "ClothingHeadsetGrey"),
                         ("eyes", "ClothingEyesGlassesSunglasses"),
                         ("mask", "ClothingMaskGas"),
                         ("head", "ClothingHeadHelmetBasic"),
                     })
            {
                var item = SEntMan.SpawnEntity(prototype, map.GridCoords);
                Assert.That(SEntMan.System<InventorySystem>().TryEquip(headHuman, item, slot), Is.True, slot);
                headEquipment.Add(slot, item);
            }

            Assert.That(_medicalIndex.TryGetBodyPart(
                headHuman,
                new CMUMedicalBodyPartKey(BodyPartType.Head, BodyPartSymmetry.None),
                out var head),
                Is.True);
            var severHead = new BodyPartSeverAttemptEvent(headHuman, head, BodyPartType.Head, Surgical: true);
            SEntMan.EventBus.RaiseLocalEvent(head, ref severHead);
            Assert.That(severHead.Succeeded, Is.True);
        });
        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            Assert.Multiple(() =>
            {
                Assert.That(inventory.TryGetSlotEntity(human, "gloves", out _), Is.False);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(gloves), Is.True,
                    "gloves should be unequipped and flung with a severed arm");
                Assert.That(inventory.TryGetSlotEntity(legHuman, "shoes", out _), Is.False);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(shoes), Is.True,
                    "shoes should be unequipped and flung with a severed leg");
            });

            foreach (var (slot, item) in headEquipment)
            {
                Assert.That(inventory.TryGetSlotEntity(headHuman, slot, out _), Is.False, slot);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.True,
                    $"{slot} equipment should be unequipped and flung with a severed head");
            }
        });

        await Pair.RunTicksSync(29);

        await Client.WaitAssertion(() =>
        {
            var carrier = CEntMan.GetEntity(carrierNet);
            Assert.That(CEntMan.GetComponent<MetaDataComponent>(carrier).EntityName, Is.EqualTo("left arm"));
            var body = CEntMan.GetComponent<BodyComponent>(carrier);
            Assert.That(body.Organs!.ContainedEntities, Has.Count.EqualTo(2));

            var sprite = CEntMan.GetComponent<SpriteComponent>(carrier);
            AssertBodyPart(sprite, carrier, HumanoidVisualLayers.LArm, "l_arm");
            AssertBodyPart(sprite, carrier, HumanoidVisualLayers.LHand, "l_hand");
        });
    }

    private void AssertBodyPart(
        SpriteComponent sprite,
        EntityUid body,
        HumanoidVisualLayers layerKey,
        string expectedState)
    {
        Assert.That(Sprites.LayerMapTryGet((body, sprite), layerKey, out var layer, false),
            Is.True,
            $"CMMobHuman is missing its {layerKey} sprite layer");

        var state = Sprites.LayerGetRsiState((body, sprite), layer);
        Assert.Multiple(() =>
        {
            Assert.That(state.Name, Is.EqualTo(expectedState), $"CMMobHuman {layerKey} is not being drawn");
            Assert.That(sprite[layer].ActualRsi?.Path, Is.EqualTo(HumanPartsRsi),
                $"CMMobHuman {layerKey} must use the CMU human body sprites");
            Assert.That(sprite[layer].Visible, Is.True, $"CMMobHuman {layerKey} is hidden");
            Assert.That(sprite[layer].Color.A, Is.GreaterThan(0f), $"CMMobHuman {layerKey} is transparent");
        });
    }

}

#pragma warning restore RA0002
