using Content.Shared.Audio;
using Content.Shared.CMU14.Cables; // RUMC: the layer of a cable
using Content.Shared.Construction.Components;
using Content.Shared.Explosion;
using Content.Shared.Eye;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.SubFloor
{
    /// <summary>
    ///     Entity system backing <see cref="SubFloorHideComponent"/>.
    /// </summary>
    [UsedImplicitly]
    public abstract partial class SharedSubFloorHideSystem : EntitySystem
    {
        [Dependency] private ITileDefinitionManager _tileDefinitionManager = default!;
        [Dependency] private SharedAmbientSoundSystem _ambientSoundSystem = default!;
        [Dependency] protected SharedMapSystem Map = default!;
        [Dependency] protected SharedAppearanceSystem Appearance = default!;
        [Dependency] private SharedVisibilitySystem _visibility = default!;
        [Dependency] protected SharedPopupSystem _popup = default!;

        [Dependency] private EntityQuery<SubFloorHideComponent> _hideQuery = default!;
        [Dependency] private EntityQuery<CableLayerComponent> _layerQuery = default!; // RUMC: the layer of a cable

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
            SubscribeLocalEvent<SubFloorHideComponent, ComponentStartup>(OnSubFloorStarted);
            SubscribeLocalEvent<SubFloorHideComponent, ComponentShutdown>(OnSubFloorTerminating);
            // Like 80% sure this doesn't need to handle re-anchoring.
            SubscribeLocalEvent<SubFloorHideComponent, AnchorStateChangedEvent>(HandleAnchorChanged);
            SubscribeLocalEvent<SubFloorHideComponent, GettingInteractedWithAttemptEvent>(OnInteractionAttempt);
            SubscribeLocalEvent<SubFloorHideComponent, GettingAttackedAttemptEvent>(OnAttackAttempt);
            SubscribeLocalEvent<SubFloorHideComponent, GetExplosionResistanceEvent>(OnGetExplosionResistance);
            SubscribeLocalEvent<SubFloorHideComponent, AnchorAttemptEvent>(OnAnchorAttempt);
            SubscribeLocalEvent<SubFloorHideComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
            // RUMC: the client learns which layer a cable lies on only after the entity has started.
            SubscribeLocalEvent<CableLayerComponent, AfterAutoHandleStateEvent>(OnLayerState);
        }

        // RUMC method
        private void OnLayerState(Entity<CableLayerComponent> ent, ref AfterAutoHandleStateEvent args)
        {
            RefreshCover(ent.Owner);
        }

        /// <summary>
        ///     Works out again whether a floor tile covers this entity, for when something besides the tile changed.
        /// </summary>
        // RUMC method
        public void RefreshCover(EntityUid uid)
        {
            UpdateFloorCover(uid);
        }

        // RUMC: a cable that lies on the top layer is not covered by the tile it lies on.
        private bool IsCovered(EntityUid uid, EntityUid gridUid, MapGridComponent grid, Vector2i position)
        {
            if (_layerQuery.TryComp(uid, out var layer) && !layer.Buried)
                return false;

            return HasFloorCover(gridUid, grid, position);
        }

        private void OnAnchorAttempt(EntityUid uid, SubFloorHideComponent component, AnchorAttemptEvent args)
        {
            // No teleporting entities through floor tiles when anchoring them.
            var xform = Transform(uid);

            if (TryComp<MapGridComponent>(xform.GridUid, out var grid)
                // RUMC: IsCovered takes the layer of a cable into account; the original line is kept here:
                // RUMC: && HasFloorCover(xform.GridUid.Value, grid, Map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates)))
                && IsCovered(uid, xform.GridUid.Value, grid, Map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates)))
            {
                _popup.PopupEntity(Loc.GetString("subfloor-anchor-failure", ("entity", uid)), args.User, args.User);
                args.Cancel();
            }
        }

        private void OnUnanchorAttempt(EntityUid uid, SubFloorHideComponent component, UnanchorAttemptEvent args)
        {
            // No un-anchoring things under the floor. Only required for something like vents, which are still interactable
            // despite being partially under the floor.
            if (component.IsUnderCover)
            {
                _popup.PopupEntity(Loc.GetString("subfloor-unanchor-failure", ("entity", uid)), args.User, args.User);
                args.Cancel();
            }
        }

        private void OnGetExplosionResistance(EntityUid uid, SubFloorHideComponent component, ref GetExplosionResistanceEvent args)
        {
            if (component.BlockInteractions && component.IsUnderCover)
                args.DamageCoefficient = 0;
        }

        private void OnAttackAttempt(EntityUid uid, SubFloorHideComponent component, ref GettingAttackedAttemptEvent args)
        {
            if (component.BlockInteractions && component.IsUnderCover)
                args.Cancelled = true;
        }

        private void OnInteractionAttempt(EntityUid uid, SubFloorHideComponent component, ref GettingInteractedWithAttemptEvent args)
        {
            // Allow admins (e.g., mappers/aghosts) to twiddle with stuff under subfloors
            if (HasComp<BypassInteractionChecksComponent>(args.Uid))
                return;

            // No interactions with entities hidden under floor tiles.
            if (component.BlockInteractions && component.IsUnderCover)
                args.Cancelled = true;
        }

        private void OnSubFloorStarted(EntityUid uid, SubFloorHideComponent component, ComponentStartup _)
        {
            UpdateFloorCover(uid, component);
            UpdateAppearance(uid, component);
            EnsureComp<CollideOnAnchorComponent>(uid);
        }

        private void OnSubFloorTerminating(EntityUid uid, SubFloorHideComponent component, ComponentShutdown _)
        {
            // If component is being deleted don't need to worry about updating any component stuff because it won't matter very shortly.
            if (Comp<MetaDataComponent>(uid).EntityLifeStage >= EntityLifeStage.Terminating)
                return;

            // Regardless of whether we're on a subfloor or not, unhide.
            SetUnderCover((uid, component), false);
            UpdateAppearance(uid, component);
        }

        private void HandleAnchorChanged(EntityUid uid, SubFloorHideComponent component, ref AnchorStateChangedEvent args)
        {
            if (args.Anchored)
            {
                var xform = Transform(uid);
                UpdateFloorCover(uid, component, xform);
            }
            else if (component.IsUnderCover)
            {
                SetUnderCover((uid, component), false);
                UpdateAppearance(uid, component);
            }
        }

        private void OnTileChanged(ref TileChangedEvent args)
        {
            foreach (var change in args.Changes)
            {
                if (change.OldTile.IsEmpty)
                    continue; // Nothing is anchored here anyways.

                if (change.NewTile.IsEmpty)
                    continue; // Anything that was here will be unanchored anyways.

                UpdateTile(args.Entity, args.Entity.Comp, change.GridIndices);
            }
        }

        /// <summary>
        ///     Update whether a given entity is currently covered by a floor tile.
        /// </summary>
        private void UpdateFloorCover(EntityUid uid, SubFloorHideComponent? component = null, TransformComponent? xform = null)
        {
            if (!Resolve(uid, ref component, ref xform))
                return;

            if (xform.Anchored && TryComp<MapGridComponent>(xform.GridUid, out var grid))
                // RUMC: IsCovered takes the layer of a cable into account; the original line is kept here:
                // RUMC: SetUnderCover((uid, component), HasFloorCover(xform.GridUid.Value, grid, Map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates)));
                SetUnderCover((uid, component), IsCovered(uid, xform.GridUid.Value, grid, Map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates)));
            else
                SetUnderCover((uid, component), false);

            UpdateAppearance(uid, component);
        }

        private void SetUnderCover(Entity<SubFloorHideComponent> entity, bool value)
        {
            // If it's not undercover or it always has visible layers then normal visibility.
            _visibility.SetLayer(entity.Owner, value && entity.Comp.VisibleLayers.Count == 0 ? (ushort) VisibilityFlags.Subfloor : (ushort) VisibilityFlags.Normal);

            if (entity.Comp.IsUnderCover == value)
                return;

            entity.Comp.IsUnderCover = value;
        }

        public bool HasFloorCover(EntityUid gridUid, MapGridComponent grid, Vector2i position)
        {
            // TODO Redo this function. Currently wires on an asteroid are always "below the floor"
            var tileDef = (ContentTileDefinition) _tileDefinitionManager[Map.GetTileRef(gridUid, grid, position).Tile.TypeId];
            return !tileDef.IsSubFloor;
        }

        private void UpdateTile(EntityUid gridUid, MapGridComponent grid, Vector2i position)
        {
            foreach (var uid in Map.GetAnchoredEntities(gridUid, grid, position))
            {
                if (!_hideQuery.TryComp(uid, out var hideComp))
                    continue;

                // RUMC: whether it is covered is now worked out for each entity (a cable on the top layer is not), so the line that did it once for the tile is kept here:
                // RUMC: var covered = HasFloorCover(gridUid, grid, position);
                var covered = IsCovered(uid, gridUid, grid, position);

                if (hideComp.IsUnderCover == covered)
                    continue;

                SetUnderCover((uid, hideComp), covered);
                UpdateAppearance(uid, hideComp);
            }
        }

        public void UpdateAppearance(
            EntityUid uid,
            SubFloorHideComponent? hideComp = null,
            AppearanceComponent? appearance = null)
        {
            if (!Resolve(uid, ref hideComp, false))
                return;

            if (hideComp.BlockAmbience && hideComp.IsUnderCover)
                _ambientSoundSystem.SetAmbience(uid, false);
            else if (hideComp.BlockAmbience && !hideComp.IsUnderCover)
                _ambientSoundSystem.SetAmbience(uid, true);

            if (Resolve(uid, ref appearance, false))
            {
                Appearance.SetData(uid, SubFloorVisuals.Covered, hideComp.IsUnderCover, appearance);
            }
        }

        [Serializable, NetSerializable]
        protected sealed partial class ShowSubfloorRequestEvent : EntityEventArgs
        {
            public bool Value;
        }
    }

    [Serializable, NetSerializable]
    public enum SubFloorVisuals : byte
    {
        /// <summary>
        /// Is there a floor tile over this entity
        /// </summary>
        Covered,

        /// <summary>
        /// Is this entity revealed by a scanner or some other entity?
        /// </summary>
        ScannerRevealed,
    }

    [Serializable, NetSerializable]
    public enum SubfloorLayers : byte
    {
        FirstLayer
    }
}
