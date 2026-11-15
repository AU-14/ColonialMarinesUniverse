using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Surrender;
using Content.Shared.CMU14.Surrender;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Surrender;

[TestFixture]
public sealed class CMUSurrenderCuffingTest : GameTest
{

    [TestCase("RMCHandcuffs", false)]
    [TestCase("RMCHandcuffs", true)]
    [TestCase("RMCZipties", false)]
    [TestCase("RMCZipties", true)]
    public async Task AppliedRestraintsEndSurrenderAndPreventRestart(string prototype, bool instant)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            EntityUid? cuffs = null;
            try
            {
                var chat = Server.System<ChatSystem>();
                var surrender = Server.System<CMUSurrenderSystem>();
                var cuffing = Server.System<SharedCuffableSystem>();
                Assert.That(chat.TryEmoteWithChat(target, CMUSurrenderSystem.SurrenderEmote), Is.True);
                if (instant)
                {
                    cuffs = cuffing.TryAddCuffsInstant(target, prototype);
                    Assert.That(cuffs, Is.Not.Null);
                }
                else
                {
                    cuffs = SEntMan.SpawnEntity(prototype, map.GridCoords);
                    Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, cuffs.Value), Is.True);
                    Assert.That(cuffing.TryAddNewCuffs(target, user, cuffs.Value), Is.True);
                }

                Assert.That(SEntMan.GetComponent<CuffableComponent>(target).CuffedHandCount, Is.GreaterThan(0));
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(target), Is.False);
                Assert.That(surrender.CanSurrender(target), Is.False);
                Assert.That(chat.TryEmoteWithChat(target, CMUSurrenderSystem.SurrenderEmote), Is.False);
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(target), Is.False);
            }
            finally
            {
                SEntMan.DeleteEntity(user);
                SEntMan.DeleteEntity(target);
                if (cuffs is { } item && !SEntMan.Deleted(item))
                    SEntMan.DeleteEntity(item);
            }
        });
    }
}
