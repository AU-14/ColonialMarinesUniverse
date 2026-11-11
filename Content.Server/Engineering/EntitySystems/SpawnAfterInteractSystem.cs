using Content.Server.Engineering.Components;
using Content.Server.Stack;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.CMU14.Engineering;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Engineering.EntitySystems
{
    [UsedImplicitly]
    public sealed partial class SpawnAfterInteractSystem : EntitySystem
    {
        [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private StackSystem _stackSystem = default!;
        [Dependency] private TurfSystem _turfSystem = default!;
        [Dependency] private SharedTransformSystem _transform = default!;
        [Dependency] private SharedMapSystem _maps = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpawnAfterInteractComponent, AfterInteractEvent>(HandleAfterInteract);
            SubscribeLocalEvent<SpawnAfterInteractComponent, CMUSpawnAfterInteractDoAfterEvent>(OnSpawnDoAfter); // CMU14
        }

        // CMU14: Complete delayed placement through a do-after event so its lifetime follows the item.
        private void HandleAfterInteract(EntityUid uid, SpawnAfterInteractComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach && !component.IgnoreDistance)
                return;
            if (string.IsNullOrEmpty(component.Prototype))
                return;

            if (!TryGetClearTile(args.ClickLocation, out _, out _))
                return;

            if (component.DoAfterTime > 0)
            {
                var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.DoAfterTime,
                    new CMUSpawnAfterInteractDoAfterEvent(GetNetCoordinates(args.ClickLocation)), uid)
                {
                    BreakOnMove = true,
                };
                _doAfterSystem.TryStartDoAfter(doAfterArgs);
                return;
            }

            CompletePlacement(uid, component, args.ClickLocation);
        }

        // CMU14
        private void OnSpawnDoAfter(Entity<SpawnAfterInteractComponent> ent, ref CMUSpawnAfterInteractDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled)
                return;

            CompletePlacement(ent, ent.Comp, GetCoordinates(args.Coordinates));
            args.Handled = true;
        }

        // CMU14: Recheck occupancy on completion; another placement may have finished during the delay.
        private void CompletePlacement(EntityUid uid, SpawnAfterInteractComponent component, EntityCoordinates coordinates)
        {
            if (component.Deleted || !TryGetClearTile(coordinates, out _, out var grid))
                return;

            if (TryComp<StackComponent>(uid, out var stackComp)
                && component.RemoveOnInteract && !_stackSystem.TryUse((uid, stackComp), 1))
            {
                return;
            }

            Spawn(component.Prototype, coordinates.SnapToGrid(grid));

            if (component.RemoveOnInteract && stackComp == null)
                TryQueueDel(uid);
        }

        // CMU14: WallLayer covers the inflatables themselves, which otherwise stack on one tile.
        private bool TryGetClearTile(EntityCoordinates coordinates, out EntityUid gridUid, out MapGridComponent grid)
        {
            gridUid = default;
            grid = default!;
            if (_transform.GetGrid(coordinates) is not { } uid || !TryComp(uid, out MapGridComponent? component) ||
                !_maps.TryGetTileRef(uid, component, coordinates, out var tile))
                return false;

            gridUid = uid;
            grid = component;
            return !tile.Tile.IsEmpty && !_turfSystem.IsTileBlocked(tile, CollisionGroup.MobMask | CollisionGroup.WallLayer);
        }
    }
}
