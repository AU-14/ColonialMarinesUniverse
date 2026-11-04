#nullable enable
using Content.Shared.Camera;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Minds;

[TestFixture]
public sealed partial class MindTests
{
    [Test]
    public async Task DeleteAllThenGhost()
    {
        var pair = Pair;

        // Client is connected with a valid entity & mind
        Assert.That(pair.Client.EntMan.EntityExists(pair.Client.AttachedEntity));
        Assert.That(pair.Server.EntMan.EntityExists(pair.PlayerData?.Mind));

        // Delete **everything** without deleting attached parent/child hierarchies in the same game state.
        // Camera identities are persistent services that regenerate when deleted.
        await pair.DeleteAllEntitiesLeafFirst(ent => pair.Server.EntMan.HasComponent<CameraNetworkIdentityComponent>(ent));

        Assert.That(pair.Server.EntMan.GetEntities(), Has.All.Matches<EntityUid>(
            ent => pair.Server.EntMan.HasComponent<CameraNetworkIdentityComponent>(ent)));

        var services = pair.Server.EntMan.GetEntities().Select(ent => pair.Server.EntMan.GetNetEntity(ent)).ToArray();
        Assert.That(pair.Client.EntMan.GetEntities().Select(ent => pair.Client.EntMan.GetNetEntity(ent)), Is.EquivalentTo(services));

        // Create a new map.
        MapId mapId = default;
        await pair.Server.WaitPost(() => pair.Server.System<SharedMapSystem>().CreateMap(out mapId));
        await pair.RunTicksSync(5);

        // Client is not attached to anything
        Assert.That(pair.Client.AttachedEntity, Is.Null);
        Assert.That(pair.PlayerData?.Mind, Is.Null);

        // Attempt to ghost
        var cConHost = pair.Client.ResolveDependency<IConsoleHost>();
        await pair.Client.WaitPost(() => cConHost.ExecuteCommand("ghost"));
        await pair.RunTicksSync(10);

        // Client should be attached to a ghost placed on the new map.
        Assert.That(pair.Client.EntMan.EntityExists(pair.Client.AttachedEntity));
        Assert.That(pair.Server.EntMan.EntityExists(pair.PlayerData?.Mind));
        var xform = pair.Client.Transform(pair.Client.AttachedEntity!.Value);
        Assert.That(xform.MapID, Is.EqualTo(mapId));
    }
}
