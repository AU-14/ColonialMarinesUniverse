using Content.Server.CMU14.Maps;
using Robust.Shared.Map;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    private void CMUSpawnTileDestroyDrops(TileRef tileRef)
    {
        EntityManager.System<CMUTileDestructionSystem>().SpawnDestroyDrops(tileRef);
    }
}
