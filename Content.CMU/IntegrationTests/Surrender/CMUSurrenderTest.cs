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
