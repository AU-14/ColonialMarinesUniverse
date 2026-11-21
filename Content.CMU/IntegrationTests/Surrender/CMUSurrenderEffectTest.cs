using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Surrender;
using Content.Shared.Chat;
using Content.Shared.CMU14.Surrender;
using Content.Shared.Cuffs;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.CMU14.Surrender;

[TestFixture]
public sealed class CMUSurrenderEffectTest : GameTest
{

    [Test]
    public async Task UndroppableItemPreventsSurrenderAndAnimation()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var item = SEntMan.SpawnEntity("Pen", map.GridCoords);
            try
            {
                var hands = Server.System<SharedHandsSystem>();
                Assert.That(hands.TryPickupAnyHand(human, item), Is.True);
                SEntMan.AddComponent<UnremoveableComponent>(item);
                Server.System<ChatSystem>().TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote);
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(human), Is.False);
                Assert.That(Effects(human), Is.Empty);
                Assert.That(hands.EnumerateHeld(human), Does.Contain(item));
            }
            finally
            {
                SEntMan.DeleteEntity(human);
                if (SEntMan.EntityExists(item)) SEntMan.DeleteEntity(item);
            }
        });
    }

    private List<EntityUid> Effects(EntityUid parent)
    {
        var effects = new List<EntityUid>();
        var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meta, out var transform))
        {
            if (meta.EntityPrototype?.ID == "CMUEffectSurrenderHands" && transform.ParentUid == parent && !SEntMan.IsQueuedForDeletion(uid))
                effects.Add(uid);
        }
        return effects;
    }
}
