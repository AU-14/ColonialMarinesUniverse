using Content.Shared._RMC14.Standing;
using Content.Shared.Movement.Systems;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controls;
using Content.Shared.Movement.Components;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Inventory;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Surrender;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Body;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Surrender;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.Speech;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Surrender;

[TestFixture]
[TestOf(typeof(CMUSurrenderSystem))]
public sealed class CMUSurrenderTest : GameTest
{
    [TestCase("Chair")]
    [TestCase("Bed")]
    public async Task BucklingPreservesSurrender(string prototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var strap = SEntMan.SpawnEntity(prototype, map.GridCoords);
            try
            {
                var chat = Server.System<ChatSystem>();
                Assert.That(chat.TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);
                Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(human, human, strap), Is.True);
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(human), Is.True);
                Assert.That(chat.TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(human), Is.False);
            }
            finally
            {
                SEntMan.DeleteEntity(human);
                SEntMan.DeleteEntity(strap);
            }
        });
    }

    [Test]
    public async Task SurrenderShadersLoadWithTheNativeDisplacementParameters()
    {
        await Client.WaitAssertion(() =>
        {
            foreach (var id in new[] { "CMUSurrenderDraw", "CMUSurrenderDrawUnshaded" })
            {
                var shader = CProtoMan.Index<ShaderPrototype>(id).InstanceUnique();
                using (shader)
                {
                    shader.SetParameter("displacementSize", 127f);
                    shader.SetParameter("displacementUV", new System.Numerics.Vector4(0f, 0f, 1f, 1f));
                }
            }
        });
    }

    [TestCase("CMBeltMarine", "belt", true)]
    [TestCase("RMCBackpackRTOBagPhone", "back", true)]
    [TestCase("RMCWeaponRifleM54C", "back", false)]
    public async Task GearFillStaysBehindOriginalArtAndCleansUp(string prototype, string slot, bool shouldFill)
    {
        var map = await Pair.CreateTestMap();
        var originalAttached = ServerSession!.AttachedEntity;
        EntityUid source = default;
        EntityUid gear = default;
        EntityUid uniform = default;
        var originals = new Dictionary<string, (SpriteComponent.Layer Layer, ShaderInstance? Shader)>();
        try
        {
            await Server.WaitAssertion(() =>
            {
                source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                uniform = SEntMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords);
                gear = SEntMan.SpawnEntity(prototype, map.GridCoords);
                var inventory = Server.System<InventorySystem>();
                Assert.That(inventory.TryEquip(source, uniform, "jumpsuit", force: true), Is.True);
                Assert.That(inventory.TryEquip(source, gear, slot, force: true), Is.True);
                Server.PlayerMan.SetAttachedEntity(ServerSession, source);
            });
            await Pair.RunUntilSynced();
            await Pair.RunTicksSync(2);
            var clientSource = Pair.ToClientUid(source);
            await Client.WaitAssertion(() =>
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(clientSource);
                var keys = CEntMan.GetComponent<InventorySlotsComponent>(clientSource).VisualLayerKeys[slot];
                Assert.That(keys, Is.Not.Empty);
                foreach (var key in keys)
                {
                    Assert.That(Client.System<SpriteSystem>().TryGetLayer((clientSource, sprite), key,
                        out var layer, false), Is.True);
                    originals.Add(key, (layer!, layer!.Shader));
                }
            });
            await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(
                source, CMUSurrenderSystem.SurrenderEmote), Is.True));
            await Pair.RunUntilSynced();
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(clientSource);
                var sprites = Client.System<SpriteSystem>();
                foreach (var (key, original) in originals)
                {
                    Assert.That(sprites.LayerMapTryGet((clientSource, sprite), key, out var index, false), Is.True);
                    Assert.That(sprites.TryGetLayer((clientSource, sprite), index, out var layer, false), Is.True);
                    Assert.That(layer, Is.SameAs(original.Layer), "The original gear must remain the render source.");
                    Assert.That(layer!.Shader, Is.SameAs(original.Shader), "The original gear shader must remain untouched.");
                    var found = sprites.LayerMapTryGet((clientSource, sprite), $"cmu-surrender-fill-{key}",
                        out var fill, false);
                    Assert.That(found, Is.EqualTo(shouldFill), "Only worn storage gear needs the hand-cutout fill.");
                    if (shouldFill)
                        Assert.That(fill, Is.LessThan(index), "The fill must sit behind the original buckle and pouch details.");
                }
            });

            await Server.WaitAssertion(() => Assert.That(Server.System<InventorySystem>().TryUnequip(
                source, slot, force: true), Is.True));
            await Pair.RunUntilSynced();
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(clientSource);
                foreach (var key in originals.Keys)
                {
                    Assert.That(Client.System<SpriteSystem>().LayerMapTryGet((clientSource, sprite),
                        $"cmu-surrender-fill-{key}", out _, false), Is.False,
                        "Removing equipment must remove its fill instead of leaving a floating copy.");
                }
            });
            await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(
                source, CMUSurrenderSystem.SurrenderEmote), Is.True));
            await Pair.RunUntilSynced();
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, originalAttached));
            foreach (var entity in new[] { source, gear, uniform })
            {
                if (SEntMan.EntityExists(entity))
                    await Pair.DeleteEntityTreeLeafFirst(entity);
            }
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task StartingSurrenderDoesNotSendOverheadText(bool canSpeak)
    {
        var map = await Pair.CreateTestMap();
        var originalAttached = ServerSession!.AttachedEntity;
        EntityUid source = default;
        NetEntity netSource = default;
        var messages = new List<ChatMessage>();
        var controller = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>();
        Action<ChatMessage> capture = message => messages.Add(message);
        await Client.WaitPost(() => controller.MessageAdded += capture);
        try
        {
            await Server.WaitPost(() =>
            {
                source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                netSource = SEntMan.GetNetEntity(source);
                Server.System<SpeechSystem>().SetSpeech(source, canSpeak);
                Server.PlayerMan.SetAttachedEntity(ServerSession, source);
            });
            await Pair.RunUntilSynced();
            await Client.WaitPost(() => messages.Clear());
            await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(
                source, CMUSurrenderSystem.SurrenderEmote), Is.True));
            await Pair.RunUntilSynced();
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
            {
                var overhead = messages.Where(message => message.SenderEntity == netSource
                    && !message.HidePopup
                    && message.Channel is ChatChannel.Local or ChatChannel.Emotes).ToArray();
                Assert.That(overhead, Is.Empty,
                    "The hand animation replaces both spoken pleas and raise-hands text, including for mute characters.");
                var actions = messages.Where(message => message.SenderEntity == netSource
                    && message.Channel == ChatChannel.Emotes && !message.HideChat).ToArray();
                Assert.That(actions, Has.Length.EqualTo(1), "Successful surrender must appear once in the chat box.");
                Assert.That(actions[0].HidePopup, Is.True, "The action must not add an overhead text bubble.");
            });
        }
        finally
        {
            await Client.WaitPost(() => controller.MessageAdded -= capture);
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, originalAttached));
            if (SEntMan.EntityExists(source))
                await Pair.DeleteEntityTreeLeafFirst(source);
        }
    }

    [TestCase("CMUUndershirtOlive", HumanoidVisualLayers.UndergarmentTop)]
    [TestCase("CMUUndershirtBlackLong", HumanoidVisualLayers.UndergarmentTop)]
    [TestCase("CMUUndershirtOliveSleeveless", HumanoidVisualLayers.UndergarmentTop)]
    [TestCase("RMCUndershirtStandard", HumanoidVisualLayers.UndergarmentTop)]
    [TestCase("CMUUnderwearBoxersBrown", HumanoidVisualLayers.UndergarmentBottom)]
    [TestCase("CMUUnderwearBriefsBlack", HumanoidVisualLayers.UndergarmentBottom)]
    [TestCase("RMCUnderwearBoxers", HumanoidVisualLayers.UndergarmentBottom)]
    public async Task UndergarmentsFollowPoseAndAppearanceChanges(string garment,
        HumanoidVisualLayers bodyPart)
    {
        var map = await Pair.CreateTestMap();
        EntityUid source = default;
        var originalAttached = ServerSession!.AttachedEntity;
        ProtoId<OrganCategoryPrototype> category = default;
        string markingKey = default!;
        await Server.WaitPost(() =>
        {
            source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession, source);
            var torso = Server.System<BodySystem>().EnumerateOrgans<VisualOrganMarkingsComponent>(source)
                .Single(organ =>
                {
                    var markings = organ.Comp2.MarkingData;
                    return markings.Layers.Contains(bodyPart);
                });
            category = SEntMan.GetComponent<OrganComponent>(torso.Owner).Category!.Value;
            Server.System<Content.Server.Body.VisualBodySystem>().ApplyMarkings(source, new()
            {
                [category] = new() { [bodyPart] = [new Marking(garment, 1)] },
            });
            var texture = (SpriteSpecifier.Rsi) SProtoMan.Index<MarkingPrototype>(garment).Sprites.Single();
            markingKey = $"{garment}-{texture.RsiState}";
        });
        await Pair.RunUntilSynced();
        await Pair.RunTicksSync(2);
        var clientSource = Pair.ToClientUid(source);
        var originalLayers = 0;
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientSource);
            Assert.That(Client.System<SpriteSystem>().LayerMapTryGet(
                (clientSource, sprite), markingKey, out _, false), Is.True);
            originalLayers = sprite.AllLayers.Count();
        });

        await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(
            source, CMUSurrenderSystem.SurrenderEmote), Is.True));
        await Pair.RunUntilSynced();
        await Pair.RunTicksSync(2);
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientSource);
            var sprites = Client.System<SpriteSystem>();
            Assert.That(sprites.LayerMapTryGet((clientSource, sprite),
                $"cmu-surrender-fill-{markingKey}", out var fill, false), Is.True,
                "Default undergarments need the hand-notch fill even without inventory clothing.");
            Assert.That(sprites.LayerMapTryGet((clientSource, sprite), markingKey, out var original, false), Is.True);
            Assert.That(fill, Is.GreaterThan(original),
                "The small waist repair must cover opaque hand-outline pixels, not just transparent holes.");
            Assert.That(sprites.LayerMapTryGet((clientSource, sprite),
                $"cmu-surrender-front-{markingKey}", out _, false),
                Is.EqualTo(bodyPart == HumanoidVisualLayers.UndergarmentTop),
                "Only the top has sleeves to raise; underwear stays at the waist.");
        });

        var replacement = bodyPart == HumanoidVisualLayers.UndergarmentTop
            ? "CMUUndershirtWhite" : "CMUUnderwearBoxersWhite";
        string replacementKey = default!;
        await Server.WaitPost(() =>
        {
            Server.System<Content.Server.Body.VisualBodySystem>().ApplyMarkings(source, new()
            {
                [category] = new() { [bodyPart] = [new Marking(replacement, 1)] },
            });
            var texture = (SpriteSpecifier.Rsi) SProtoMan.Index<MarkingPrototype>(replacement).Sprites.Single();
            replacementKey = $"{replacement}-{texture.RsiState}";
        });
        await Pair.RunUntilSynced();
        await Pair.RunTicksSync(2);
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientSource);
            var sprites = Client.System<SpriteSystem>();
            Assert.That(sprites.LayerMapTryGet((clientSource, sprite),
                $"cmu-surrender-front-{markingKey}", out _, false), Is.False,
                "Appearance refreshes must remove the previous sleeve cutout.");
            Assert.That(sprites.LayerMapTryGet((clientSource, sprite),
                $"cmu-surrender-fill-{markingKey}", out _, false), Is.False,
                "Appearance refreshes must remove the previous undergarment fill.");
            Assert.That(sprites.LayerMapTryGet((clientSource, sprite),
                $"cmu-surrender-fill-{replacementKey}", out _, false), Is.True);
        });

        await Server.WaitAssertion(() => Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(
            source, CMUSurrenderSystem.SurrenderEmote), Is.True));
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientSource);
            var sprites = Client.System<SpriteSystem>();
            Assert.That(sprite.AllLayers.Count(), Is.EqualTo(originalLayers),
                "Lowering hands must restore ordinary undergarments without duplicate layers.");
            Assert.That(sprites.LayerMapTryGet((clientSource, sprite),
                $"cmu-surrender-front-{replacementKey}", out _, false), Is.False);
            Assert.That(sprites.LayerMapTryGet((clientSource, sprite),
                $"cmu-surrender-fill-{replacementKey}", out _, false), Is.False);
            Assert.That(sprites.LayerMapTryGet((clientSource, sprite),
                replacementKey, out var index, false), Is.True);
            Assert.That(sprites.TryGetLayer((clientSource, sprite), index, out var layer, false), Is.True);
            Assert.That(layer!.Shader, Is.Null, "The undergarment must regain its normal rendering shader.");
        });
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, originalAttached));
        await Pair.DeleteEntityTreeLeafFirst(source);
    }

    [Test]
    public async Task WheelEmoteTogglesNetworkedPoseAndRestoresLayers()
    {
        var map = await Pair.CreateTestMap();
        EntityUid source = default;
        var originalAttached = ServerSession!.AttachedEntity;
        await Server.WaitPost(() =>
        {
            source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession, source);
        });
        await Pair.RunUntilSynced();
        var clientSource = Pair.ToClientUid(source);
        var originalLayers = 0;
        await Client.WaitAssertion(() =>
            originalLayers = CEntMan.GetComponent<SpriteComponent>(clientSource).AllLayers.Count());

        await Server.WaitAssertion(() =>
        {
            var emote = Server.ResolveDependency<IPrototypeManager>().Index<EmotePrototype>(CMUSurrenderSystem.SurrenderEmote);
            Assert.That(emote.Category, Is.EqualTo(EmoteCategory.Hands));
            Assert.That(emote.ChatTriggers, Is.Not.Empty, "The wheel omits emotes without a trigger.");
            Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(source, emote), Is.True);
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.True);
        });
        await Pair.RunUntilSynced();
        await Pair.RunTicksSync(2);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.HasComponent<CMUSurrenderComponent>(clientSource), Is.True);
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientSource);
            Assert.That(Client.System<SpriteSystem>().LayerMapTryGet(
                    (clientSource, sprite), "cmu-surrender-RArm-displacement", out _, false), Is.True,
                "Observers need a real pose, not just an emote message.");
            Assert.That(sprite.AllLayers.Count(), Is.GreaterThan(originalLayers));
            Assert.That(Client.System<SpriteSystem>().LayerMapTryGet(
                    (clientSource, sprite), "cmu-surrender-front-RArm", out _, false), Is.True,
                "The near arm needs a foreground layer in side views.");
            Assert.That(Client.System<SpriteSystem>().LayerMapTryGet(
                    (clientSource, sprite), "cmu-surrender-Chest-displacement", out _, false), Is.True,
                "The raised shoulders must cover the torso's standing arm seams.");
        });

        await Server.WaitAssertion(() =>
        {
            var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(source);
            Assert.That(speed.CurrentSprintSpeed, Is.EqualTo(speed.BaseWalkSpeed * 0.5f).Within(0.001f));
        });

        EntityUid uniform = default;
        await Server.WaitAssertion(() =>
        {
            uniform = SEntMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords);
            Assert.That(Server.System<InventorySystem>().TryEquip(source, uniform, "jumpsuit", force: true), Is.True);
        });
        await Pair.RunUntilSynced();
        await Pair.RunTicksSync(2);
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientSource);
            Assert.That(Client.System<SpriteSystem>().LayerMapTryGet(
                    (clientSource, sprite), "cmu-surrender-jumpsuit-0-displacement", out _, false), Is.True,
                "Newly equipped sleeves must join the raised-arm pose.");
        });
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<InventorySystem>().TryUnequip(source, "jumpsuit", force: true), Is.True));
        await Pair.RunUntilSynced();
        await Pair.RunTicksSync(2);
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientSource);
            foreach (var layer in sprite.AllLayers.OfType<SpriteComponent.Layer>())
            {
                if (layer.CopyToShaderParameters is { LayerKey: string key } && key.StartsWith("cmu-surrender-"))
                    Assert.That(Client.System<SpriteSystem>().LayerMapTryGet((clientSource, sprite), key, out _, false), Is.True,
                        "Removing clothing must not leave a map referencing a deleted sleeve layer.");
            }
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.False);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientSource);
            Assert.That(sprite.AllLayers.Count(), Is.EqualTo(originalLayers), "Ending the pose must not leave duplicate render layers.");
            Assert.That(Client.System<SpriteSystem>().LayerMapTryGet((clientSource, sprite), "cmu-surrender-RArm", out _, false), Is.False);
            Assert.That(Client.System<SpriteSystem>().LayerMapTryGet((clientSource, sprite), "cmu-surrender-Chest", out _, false), Is.False);
            Assert.That(Client.System<SpriteSystem>().LayerMapTryGet((clientSource, sprite), "cmu-surrender-front-RArm", out _, false), Is.False);
        });
        await Server.WaitAssertion(() =>
        {
            var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(source);
            Assert.That(speed.CurrentSprintSpeed, Is.GreaterThan(speed.BaseWalkSpeed), "Lowering hands must restore running.");
        });
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, originalAttached));
        await Pair.DeleteEntityTreeLeafFirst(source);
        await Pair.DeleteEntityTreeLeafFirst(uniform);
    }

    [Test]
    public async Task SurrenderDropsBothHandsAndBlocksPickupUntilToggledOff()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var item = SEntMan.SpawnEntity("Pen", map.GridCoords);
            var hands = Server.System<SharedHandsSystem>();
            var surrender = Server.System<CMUSurrenderSystem>();
            var chat = Server.System<ChatSystem>();
            Assert.That(hands.TryPickupAnyHand(source, item), Is.True);
            var other = SEntMan.SpawnEntity("Pen", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(source, other), Is.True);
            Assert.That(surrender.CanSurrender(source), Is.True);
            Assert.That(chat.TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.True);
            var component = SEntMan.GetComponent<HandsComponent>(source);
            Assert.That(hands.CountFreeHands((source, component)), Is.EqualTo(component.Count));
            Assert.That(hands.TryPickupAnyHand(source, item), Is.False);
            Assert.That(hands.TryPickupAnyHand(source, item, checkActionBlocker: false), Is.False);
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.True);
            Assert.That(hands.CountFreeHands((source, component)), Is.EqualTo(component.Count));
            Assert.That(chat.TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.False);
            Assert.That(hands.TryPickupAnyHand(source, item), Is.True);
            SEntMan.DeleteEntity(item);
            SEntMan.DeleteEntity(other);
            SEntMan.DeleteEntity(source);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task SurrenderWorksBeforeAndAfterLyingDown(bool resting, bool surrenderFirst)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var chat = Server.System<ChatSystem>();
            if (surrenderFirst)
                Assert.That(chat.TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Assert.That(Server.System<StandingStateSystem>().Down(source, dropHeldItems: false, downedBy: source), Is.True);
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.EqualTo(surrenderFirst));
            if (resting)
                Server.System<RMCStandingSystem>().SetRest(source, true);
            if (!surrenderFirst)
                Assert.That(chat.TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.True);
            Assert.That(Server.System<CMUSurrenderSystem>().CanSurrender(source), Is.True);
            Assert.That(chat.TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.False);
            SEntMan.DeleteEntity(source);
        });
    }

    [TestCase(MobState.Critical)]
    [TestCase(MobState.Dead)]
    public async Task IncapacitationEndsSurrender(MobState state)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Server.System<MobStateSystem>().ChangeMobState(source, state);
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.False);
            Assert.That(Server.System<CMUSurrenderSystem>().CanSurrender(source), Is.False);
            Server.System<MobStateSystem>().ChangeMobState(source, MobState.Alive);
            Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.True);
            SEntMan.DeleteEntity(source);
        });
    }

    [Test]
    public async Task LosingAHandKeepsSurrenderAndAllowsRestart()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var chat = Server.System<ChatSystem>();
            Assert.That(chat.TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            var hands = SEntMan.GetComponent<HandsComponent>(source);
            Server.System<SharedHandsSystem>().RemoveHand((source, hands), hands.SortedHands.First());
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.True);
            Assert.That(Server.System<CMUSurrenderSystem>().CanSurrender(source), Is.True);
            Assert.That(chat.TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Assert.That(chat.TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.True);
            SEntMan.DeleteEntity(source);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task SurrenderHalvesExistingMovementAndRestoresIt(bool armor, bool resting)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            EntityUid? suit = null;
            if (armor)
            {
                suit = SEntMan.SpawnEntity("CMArmorM3Heavy", map.GridCoords);
                Assert.That(Server.System<InventorySystem>().TryEquip(source, suit.Value, "outerClothing", force: true), Is.True);
            }
            if (resting)
            {
                Server.System<StandingStateSystem>().Down(source, dropHeldItems: false, downedBy: source);
                Server.System<RMCStandingSystem>().SetRest(source, true);
            }
            var movement = Server.System<MovementSpeedModifierSystem>();
            movement.RefreshMovementSpeedModifiers(source);
            var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(source);
            var walk = speed.CurrentWalkSpeed;
            var sprint = speed.CurrentSprintSpeed;
            var chat = Server.System<ChatSystem>();
            Assert.That(chat.TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Assert.That(speed.CurrentWalkSpeed, Is.EqualTo(walk * 0.5f).Within(0.001f));
            Assert.That(speed.CurrentSprintSpeed,
                Is.EqualTo(MathF.Min(sprint, resting ? walk : speed.BaseWalkSpeed) * 0.5f).Within(0.001f));
            Assert.That(chat.TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            Assert.That(speed.CurrentWalkSpeed, Is.EqualTo(walk).Within(0.001f));
            Assert.That(speed.CurrentSprintSpeed, Is.EqualTo(sprint).Within(0.001f));
            SEntMan.DeleteEntity(source);
            if (suit is { } item)
                SEntMan.DeleteEntity(item);
        });
    }

    [TestCase("CMArmorM3Medium")]
    [TestCase("CMArmorM3Heavy")]
    [TestCase("RMCArmorM3PVE")]
    public async Task RaisedArmorUsesItsOwnTextureAboveTheUniform(string prototype)
    {
        var map = await Pair.CreateTestMap();
        var attached = ServerSession!.AttachedEntity;
        EntityUid source = default;
        EntityUid uniform = default;
        EntityUid armor = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                uniform = SEntMan.SpawnEntity("ClothingUniformJumpsuitColorRed", map.GridCoords);
                armor = SEntMan.SpawnEntity(prototype, map.GridCoords);
                Assert.That(Server.System<InventorySystem>().TryEquip(source, uniform, "jumpsuit", force: true), Is.True);
                Server.PlayerMan.SetAttachedEntity(ServerSession, source);
                Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
            });
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() => Assert.That(Server.System<InventorySystem>().TryEquip(
                source, armor, "outerClothing", force: true), Is.True));
            await Pair.RunUntilSynced();
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                var uid = Pair.ToClientUid(source);
                var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                var sprites = Client.System<SpriteSystem>();
                Assert.That(sprites.LayerMapTryGet((uid, sprite), "cmu-surrender-front-jumpsuit-0", out var shirt, false), Is.True);
                Assert.That(sprites.LayerMapTryGet((uid, sprite), "jumpsuit-0", out var uniformLayer, false), Is.True);
                Assert.That(sprites.LayerMapTryGet((uid, sprite), "cmu-surrender-fill-jumpsuit-0", out var waistRepair, false), Is.True);
                Assert.That(waistRepair, Is.GreaterThan(uniformLayer),
                    "The uniform's old hand outline must be covered at the waistband.");
                var keys = CEntMan.GetComponent<InventorySlotsComponent>(uid).VisualLayerKeys["outerClothing"];
                var checkedArmor = false;
                foreach (var key in keys)
                {
                    if (!sprites.TryGetLayer((uid, sprite), key, out var original, false)
                        || original!.CopyToShaderParameters != null)
                        continue;
                    checkedArmor = true;
                    Assert.That(sprites.LayerMapTryGet((uid, sprite), key, out var armorLayer, false), Is.True);
                    Assert.That(waistRepair, Is.LessThan(armorLayer), "Waist repairs must stay underneath worn armor.");
                    Assert.That(sprites.LayerMapTryGet((uid, sprite), $"cmu-surrender-front-{key}", out var index, false), Is.True);
                    Assert.That(index, Is.GreaterThan(shirt), "The raised armor must cover the underlying sleeve.");
                    var raised = (SpriteComponent.Layer) sprite[index];
                    Assert.That(raised.State, Is.EqualTo(original.State));
                    Assert.That(raised.RSI, Is.SameAs(original.RSI), "Raised shoulder pixels must come from the armor's own texture.");
                    Assert.That(sprites.TryGetLayer((uid, sprite), $"cmu-surrender-cmu-surrender-front-{key}-displacement",
                        out var mapLayer, false), Is.True);
                    Assert.That(mapLayer!.State.ToString(), Is.EqualTo("foreground_armor"));
                }
                Assert.That(checkedArmor, Is.True);
            });
            await Server.WaitAssertion(() => Assert.That(Server.System<InventorySystem>().TryUnequip(source, "outerClothing", force: true), Is.True));
            await Pair.RunUntilSynced();
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                var uid = Pair.ToClientUid(source);
                var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                Assert.That(Client.System<SpriteSystem>().LayerMapTryGet((uid, sprite), "cmu-surrender-front-outerClothing-0", out _, false), Is.False);
                Assert.That(Client.System<SpriteSystem>().LayerMapTryGet((uid, sprite), "cmu-surrender-front-jumpsuit-0", out _, false), Is.True);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, attached));
            foreach (var entity in new[] { source, uniform, armor })
            {
                if (SEntMan.EntityExists(entity))
                    await Pair.DeleteEntityTreeLeafFirst(entity);
            }
        }
    }

    [TestCase(HumanoidVisualLayers.LArm)]
    [TestCase(HumanoidVisualLayers.RArm)]
    [TestCase(HumanoidVisualLayers.LHand)]
    [TestCase(HumanoidVisualLayers.RHand)]
    public async Task DetachedLimbsDoNotCancelSurrenderOrGainRenderCopies(HumanoidVisualLayers limb)
    {
        var map = await Pair.CreateTestMap();
        var attached = ServerSession!.AttachedEntity;
        EntityUid source = default;
        EntityUid detached = default;
        EntityUid uniform = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                source = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                uniform = SEntMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords);
                Assert.That(Server.System<InventorySystem>().TryEquip(source, uniform, "jumpsuit", force: true), Is.True);
                Server.PlayerMan.SetAttachedEntity(ServerSession, source);
                Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
                var part = Server.System<BodySystem>().EnumerateOrgans<VisualOrganComponent>(source)
                    .Single(organ => Equals(organ.Comp2.Layer, limb));
                var result = Server.System<DetachableOrganSystem>().Detach(part.Owner);
                Assert.That(result, Is.Not.Null);
                detached = result!.Value;
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.True);
                Assert.That(Server.System<CMUSurrenderSystem>().CanSurrender(source), Is.True);
            });
            await Pair.RunUntilSynced();
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                var uid = Pair.ToClientUid(source);
                var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                var sprites = Client.System<SpriteSystem>();
                Assert.That(sprites.LayerMapTryGet((uid, sprite), $"cmu-surrender-front-{limb}", out _, false), Is.False,
                    "The detached limb must not leave a raised skin copy.");
                Assert.That(sprites.TryGetLayer((uid, sprite), "cmu-surrender-jumpsuit-0-displacement", out var mapLayer, false), Is.True);
                var state = limb switch
                {
                    HumanoidVisualLayers.LArm => "clothing_right",
                    HumanoidVisualLayers.RArm => "clothing_left",
                    _ => "clothing",
                };
                Assert.That(mapLayer!.State.ToString(), Is.EqualTo(state),
                    "Clothing must only raise sleeves belonging to attached arms.");
                if (limb is HumanoidVisualLayers.LArm or HumanoidVisualLayers.RArm)
                {
                    var remaining = limb == HumanoidVisualLayers.LArm ? HumanoidVisualLayers.RArm : HumanoidVisualLayers.LArm;
                    Assert.That(sprites.TryGetLayer((uid, sprite), $"cmu-surrender-far-{remaining}", out var far, false), Is.True);
                    Assert.That(far!.DirOffset, Is.EqualTo(SpriteComponent.DirectionOffset.Flip));
                    Assert.That(far.Scale.X, Is.LessThan(0), "The remaining arm's opposite frame must face the viewer.");
                    Assert.That(sprites.TryGetLayer((uid, sprite),
                        $"cmu-surrender-cmu-surrender-far-{remaining}-displacement", out var data, false), Is.True);
                    Assert.That(data!.DirOffset, Is.EqualTo(SpriteComponent.DirectionOffset.Flip),
                        "The fallback arm and its source lookup must use the same direction.");
                }
            });
            await Server.WaitAssertion(() =>
            {
                var chat = Server.System<ChatSystem>();
                Assert.That(chat.TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.False);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var uid = Pair.ToClientUid(source);
                var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                foreach (var layer in sprite.AllLayers.OfType<SpriteComponent.Layer>())
                {
                    if (layer.CopyToShaderParameters?.LayerKey is string key)
                        Assert.That(key.StartsWith("cmu-surrender-"), Is.False,
                            "Lowering the remaining arm must remove every normal and opposite-side pose map.");
                }
                Assert.That(Client.System<SpriteSystem>().LayerMapTryGet((uid, sprite),
                    "cmu-surrender-far-jumpsuit-0", out _, false), Is.False);
            });
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(source, CMUSurrenderSystem.SurrenderEmote), Is.True);
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(source), Is.True);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, attached));
            foreach (var entity in new[] { source, detached, uniform })
            {
                if (SEntMan.EntityExists(entity))
                    await Pair.DeleteEntityTreeLeafFirst(entity);
            }
        }
    }

    [Test]
    public async Task HighResolutionSurrenderIconFitsTheWheel()
    {
        await Client.WaitAssertion(() =>
        {
            var prototype = CProtoMan.Index<EmotePrototype>("CMUSurrender");
            using var menu = new SimpleRadialMenu();
            menu.SetButtons([new RadialMenuActionOption<EmotePrototype>(_ => { }, prototype)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(prototype.Icon),
            }]);
            var icons = Descendants(menu).OfType<TextureRect>().Where(icon => icon.Texture != null).ToArray();
            Assert.That(icons, Has.Length.EqualTo(1));
            var icon = icons[0];
            Assert.That(icon.Texture!.Width, Is.GreaterThan(32), "Keep the supplied high-resolution artwork.");
            Assert.That(icon.Texture.Width * icon.TextureScale.X, Is.EqualTo(64f).Within(0.01f));
            Assert.That(icon.Texture.Height * icon.TextureScale.Y, Is.EqualTo(64f).Within(0.01f),
                "The original file must not enlarge the emote wheel button.");
        });
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
