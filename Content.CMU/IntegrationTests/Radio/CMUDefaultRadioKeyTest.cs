using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Radio;

// :h is the headset default key. UNISC spawns with its net keyed in, so :h must reach it,
// and a speaker without a headset must stay off it
[TestFixture]
public sealed class CMUDefaultRadioKeyTest
{
    private static readonly ProtoId<JobPrototype> Rifleman = "AU14JobUNISCRifleman";

    [Test]
    public async Task DefaultKeyFollowsTheWornHeadset()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var chat = server.System<SharedChatSystem>();
            var inventory = server.System<InventorySystem>();

            var unisc = server.System<StationSpawningSystem>().SpawnPlayerMob(map.GridCoords, Rifleman, new HumanoidCharacterProfile(), station: null);
            Assert.That(inventory.TryGetSlotEntity(unisc, "ears", out var headset), Is.True);
            Assert.That(entities.GetComponent<EncryptionKeyHolderComponent>(headset.Value).Channels, Does.Contain("UNISOF"));
            Assert.That(chat.TryProcessRadioMessage(unisc, ":h radio check", out _, out var channel), Is.True);
            Assert.That(channel?.ID, Is.EqualTo("UNISOF"));

            var bare = entities.SpawnAtPosition("CMMobHuman", map.GridCoords);
            Assert.That(chat.TryProcessRadioMessage(bare, ":h radio check", out _, out channel), Is.True);
            Assert.That(channel?.ID, Is.Not.EqualTo("UNISOF"));
        });
        await pair.CleanReturnAsync();
    }
}
