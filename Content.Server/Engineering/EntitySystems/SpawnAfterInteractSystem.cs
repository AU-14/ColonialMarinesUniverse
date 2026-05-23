using System.Threading.Tasks;
using Content.Server.Engineering.Components;
using Content.Server.Stack;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.Engineering;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Stacks;
using JetBrains.Annotations;
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

        private readonly Dictionary<int, TaskCompletionSource<DoAfterStatus>> _spawnAfterInteractDoAfters = new();
        private int _nextSpawnAfterInteractDoAfterToken;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpawnAfterInteractComponent, AfterInteractEvent>(HandleAfterInteract);
            SubscribeLocalEvent<SpawnAfterInteractDoAfterEvent>(OnSpawnAfterInteractDoAfter);
        }

        private Task<DoAfterStatus> WaitSpawnAfterInteractDoAfter(DoAfterArgs doAfterArgs)
        {
            int token;
            do
            {
                token = unchecked(++_nextSpawnAfterInteractDoAfterToken);
            } while (_spawnAfterInteractDoAfters.ContainsKey(token));

            doAfterArgs.Event = new SpawnAfterInteractDoAfterEvent(token);
            doAfterArgs.Broadcast = true;

            var tcs = new TaskCompletionSource<DoAfterStatus>();
            _spawnAfterInteractDoAfters[token] = tcs;

            if (_doAfterSystem.TryStartDoAfter(doAfterArgs))
                return tcs.Task;

            _spawnAfterInteractDoAfters.Remove(token);
            return Task.FromResult(DoAfterStatus.Cancelled);
        }

        private void OnSpawnAfterInteractDoAfter(SpawnAfterInteractDoAfterEvent ev)
        {
            if (_spawnAfterInteractDoAfters.Remove(ev.Token, out var tcs))
                tcs.SetResult(ev.Cancelled ? DoAfterStatus.Cancelled : DoAfterStatus.Finished);
        }

        private async void HandleAfterInteract(EntityUid uid, SpawnAfterInteractComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach && !component.IgnoreDistance)
                return;
            if (string.IsNullOrEmpty(component.Prototype))
                return;

            var gridUid = _transform.GetGrid(args.ClickLocation);
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                return;
            if (!_maps.TryGetTileRef(gridUid.Value, grid, args.ClickLocation, out var tileRef))
                return;

            bool IsTileClear()
            {
                return tileRef.Tile.IsEmpty == false && !_turfSystem.IsTileBlocked(tileRef, CollisionGroup.MobMask);
            }

            if (!IsTileClear())
                return;

            if (component.DoAfterTime > 0)
            {
                var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.DoAfterTime, new SpawnAfterInteractDoAfterEvent(0), null)
                {
                    BreakOnMove = true,
                };
                var result = await WaitSpawnAfterInteractDoAfter(doAfterArgs);

                if (result != DoAfterStatus.Finished)
                    return;
            }

            if (component.Deleted || !IsTileClear())
                return;

            if (TryComp(uid, out StackComponent? stackComp)
                && component.RemoveOnInteract && !_stackSystem.Use(uid, 1, stackComp))
            {
                return;
            }

            Spawn(component.Prototype, args.ClickLocation.SnapToGrid(grid));

            if (component.RemoveOnInteract && stackComp == null)
                TryQueueDel(uid);
        }
    }
}
