using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Xenonids.Destroy;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.Buckle.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Water;

public sealed partial class RMCWaterSystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedAudioSystem _waterAudio = default!;
    [Dependency] private INetManager _net = default!;

    private static readonly ProtoId<TagPrototype> CatwalkTag = "Catwalk";
    private static readonly EntProtoId WaterImpact = "RMCWaterImpact";
    private static readonly ProtoId<SoundCollectionPrototype> LargeFootsteps = "XenoFootstepLarge";
    private static readonly SoundSpecifier ImpactSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/Water/splash.ogg",
        AudioParams.Default.WithVolume(-6));
    private static readonly SoundSpecifier ShallowWading = new SoundCollectionSpecifier("RMCWaterShallowWading");
    private static readonly SoundSpecifier Wading = new SoundCollectionSpecifier("RMCWaterWading");
    private static readonly SoundSpecifier DeepWading = new SoundCollectionSpecifier("RMCWaterDeepWading");
    private static readonly SoundSpecifier LargeWading = new SoundCollectionSpecifier("RMCWaterLargeFootsteps");

    private void InitializeSubmersion()
    {
        SubscribeLocalEvent<MobStateComponent, GetMobFootstepSoundEvent>(OnWaterFootstep);
        SubscribeLocalEvent<ThrownItemComponent, LandEvent>(OnWaterLanding);
        SubscribeLocalEvent<XenoLeapingComponent, ComponentStartup>(OnWaterLeapStart);
        SubscribeLocalEvent<XenoLeapComponent, XenoLeapStoppedEvent>(OnWaterLeapEnd);
    }

    private void OnWaterLeapStart(Entity<XenoLeapingComponent> ent, ref ComponentStartup args) => Splash(ent, ent);

    private void OnWaterLeapEnd(Entity<XenoLeapComponent> ent, ref XenoLeapStoppedEvent args)
    {
        if (!TerminatingOrDeleted(ent))
            Splash(ent, ent);
    }

    private void OnWaterLanding(Entity<ThrownItemComponent> ent, ref LandEvent args)
    {
        Splash(ent, args.User);
    }

    public void Splash(EntityUid uid, EntityUid? user = null)
    {
        if (!TryGetWaterSurface(uid, out _, out var depth, out var covered) || covered || depth < 2)
            return;

        _waterAudio.PlayPredicted(ImpactSound, uid, user);
        if (_net.IsServer)
            Spawn(WaterImpact, Transform(uid).Coordinates);
    }

    private void OnWaterFootstep(Entity<MobStateComponent> ent, ref GetMobFootstepSoundEvent args)
    {
        if (args.Handled || !TryGetWaterSurface(ent, out _, out var depth, out var covered) || covered)
            return;

        args.Handled = true;
        if (depth <= 0 || !CanSubmerge(ent) ||
            !TryComp<InputMoverComponent>(ent, out var mover) || !mover.Sprinting)
            return;

        args.Sound = TryComp<FootstepModifierComponent>(ent, out var footstep) &&
                     footstep.FootstepSoundCollection is SoundCollectionSpecifier collection && collection.Collection == LargeFootsteps
            ? LargeWading
            : depth <= 4 ? ShallowWading : depth <= 8 ? Wading : DeepWading;
    }

    /// <summary>
    /// Samples the tile under the mob's center, rather than immersing it when its collision box grazes water.
    /// Covered water is returned too, so clients can notice a catwalk being removed under a stationary mob.
    /// </summary>
    public bool TryGetWaterSurface(EntityUid user, out EntityUid? water, out float depth, out bool covered)
    {
        water = null;
        depth = 0;
        covered = false;
        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(user);
        while (anchored.MoveNext(out var uid))
        {
            if (!TryComp<RMCWaterComponent>(uid, out var component) || TerminatingOrDeleted(uid))
                continue;

            if (water != null && component.Depth <= depth)
                continue;

            water = uid;
            depth = component.Depth;
            covered = !CanCollide((uid, component), user);
        }

        // Some CMU maps deliberately leave water unanchored. Use contacts as a fallback for these entities.
        if (water == null && TryComp<FixturesComponent>(user, out var fixtures) &&
            _rmcMap.TryGetTileRefForEnt(Transform(user).Coordinates, out var userGrid, out var userTile))
        {
            var contacts = _physics.GetContacts((user, fixtures));
            while (contacts.MoveNext(out var contact))
            {
                var uid = contact.OtherEnt(user);
                if (!contact.IsTouching || !TryComp<RMCWaterComponent>(uid, out var component) ||
                    TerminatingOrDeleted(uid))
                    continue;

                if (!_rmcMap.TryGetTileRefForEnt(Transform(uid).Coordinates, out var waterGrid, out var waterTile) ||
                    userGrid.Owner != waterGrid.Owner || userTile.GridIndices != waterTile.GridIndices ||
                    water != null && component.Depth <= depth)
                    continue;

                water = uid;
                depth = component.Depth;
                covered = !CanCollide((uid, component), user);
            }
        }

        if (water != null)
            return true;

        if (!_rmcMap.TryGetTileDef(Transform(user).Coordinates, out var tile) || tile.RMCWaterDepth <= 0)
            return false;

        depth = tile.RMCWaterDepth;
        var covers = _rmcMap.GetAnchoredEntitiesEnumerator(user);
        while (covers.MoveNext(out var uid))
        {
            if (_tags.HasTag(uid, CatwalkTag))
            {
                covered = true;
                break;
            }
        }

        return true;
    }

    public bool CanSubmerge(EntityUid user)
    {
        return !_container.IsEntityInContainer(user) &&
               !(TryComp<BuckleComponent>(user, out var buckle) && buckle.Buckled) &&
               !(TryComp<ThrownItemComponent>(user, out var thrown) && !thrown.Landed) &&
               !HasComp<BeingFiremanCarriedComponent>(user) &&
               !HasComp<XenoLeapingComponent>(user) &&
               !HasComp<XenoDestroyLeapingComponent>(user) &&
               !HasComp<ActiveLeaperComponent>(user);
    }
}
