using Content.Shared._CMU14.Threats.Mobs.Xeno.Caste.Warlock;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using CMUDrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._CMU14.Threats.Mobs.Xeno.Xenonids.Warlock;

/// <summary>
/// Draws a pulsing outline on any projectile currently frozen by the psychic shield so the
/// player can spot the caught rounds at a glance. Mirrors the Ravager Empower outline pattern
/// from <see cref="Content.Client._RMC14.Aura.AuraSystem"/> — same <c>outline.swsl</c> shader
/// used via <see cref="SpriteComponent.PostShader"/>, params updated each frame in <c>Update</c>.
/// The only extension over Ravager is the two-endpoint colour lerp so the outline reads as
/// "still holding" rather than a static tint.
/// While frozen the projectile's sprite draw depth is raised above the shield sprite's
/// <c>Effects</c> layer so the caught round renders on top of the shield instead of being
/// hidden behind it; the original depth is captured on freeze and restored on unfreeze.
/// </summary>
public sealed partial class CMUXenoFrozenProjectileVisualsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // Reuse the existing RMCAuraOutline shader prototype so we behave exactly like Ravager's
    // outline path - only the colour endpoints and pulse rate are ours.
    private static readonly ProtoId<ShaderPrototype> OutlineShader = "RMCAuraOutline";

    // Purple matches the shield tint (#4C1D95); white is peak visibility.
    private static readonly Color Purple = Color.FromHex("#4C1D95");
    private static readonly Color White = Color.White;

    // Full purple-to-white-to-purple cycle every 0.8 s.
    private const float PulseHz = 1.25f;

    // Slim outline, one pixel wider than Ravager's default so it stays legible on small
    // bullet sprites while not swallowing rocket art.
    private const float OutlineWidthPx = 1f;

    // Depth used while a projectile is frozen. The shield sprite sits on Effects
    // (Default + 11); Ghosts (Default + 12) is the next tier above and puts the caught
    // projectile visibly on top of the shield without stepping into the UI overlay tier.
    private const int FrozenDrawDepth = (int)CMUDrawDepth.Ghosts;

    // Maps a currently-frozen projectile to the drawdepth it had before we bumped it, so
    // we can restore the original value when the freeze ends. Keyed by EntityUid because
    // the value has to survive across the shutdown of the networked frozen component and
    // the sprite may not exist at that point (entity deletion path).
    private readonly Dictionary<EntityUid, int> _originalDrawDepths = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUXenoFrozenProjectileComponent, ComponentStartup>(OnFrozenStartup);
        SubscribeLocalEvent<CMUXenoFrozenProjectileComponent, ComponentShutdown>(OnFrozenShutdown);
    }

    private void OnFrozenStartup(Entity<CMUXenoFrozenProjectileComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.PostShader = _prototypes.Index(OutlineShader).InstanceUnique();

        // Only capture the original depth once. If the same entity somehow re-enters this
        // path without a shutdown in between (should not happen, but guarded anyway) we
        // must not overwrite the saved value with the already-bumped depth.
        if (!_originalDrawDepths.ContainsKey(ent.Owner))
            _originalDrawDepths[ent.Owner] = sprite.DrawDepth;

        _sprite.SetDrawDepth((ent.Owner, sprite), FrozenDrawDepth);
    }

    private void OnFrozenShutdown(Entity<CMUXenoFrozenProjectileComponent> ent, ref ComponentShutdown args)
    {
        // Always release the saved depth entry so a reused EntityUid does not carry a
        // stale original from a previous life of this projectile.
        var hadOriginal = _originalDrawDepths.Remove(ent.Owner, out var originalDepth);

        if (TerminatingOrDeleted(ent))
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.PostShader = null;

        if (hadOriginal)
            _sprite.SetDrawDepth((ent.Owner, sprite), originalDepth);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Same sinusoid used for every frozen projectile so they all pulse in lockstep from
        // wall-clock; new projectiles blend in at whatever phase we happen to be at instead
        // of resetting the effect for the whole shield.
        var pulse = (MathF.Sin((float)_timing.CurTime.TotalSeconds * MathF.Tau * PulseHz) + 1f) * 0.5f;
        var color = Color.InterpolateBetween(Purple, White, pulse);

        var query = EntityQueryEnumerator<CMUXenoFrozenProjectileComponent, SpriteComponent>();
        while (query.MoveNext(out _, out _, out var sprite))
        {
            sprite.PostShader?.SetParameter("outline_color", color);
            sprite.PostShader?.SetParameter("outline_width", OutlineWidthPx);
        }
    }
}
