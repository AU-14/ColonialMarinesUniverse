using Content.Shared._AU14.Insurgency.Sapper;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client._AU14.Insurgency.Sapper;

/// <summary>
///     Decides, per local viewer, whether a planted sapper trap is drawn. Reveal is now entirely a
///     per-viewer, client-side decision: an enemy who walks close to a trap sees it, but that does
///     NOT reveal it to anyone else (the old server-global Revealed flag showed it to everyone at
///     once). The cell that laid it always sees its own field.
///
///     - Not planted (carried in hand / in the kit): fully drawn.
///     - Planted and the local player is CLF: fully drawn, at any range.
///     - Planted, enemy viewer within the trap's RevealRadius: fully drawn (for that viewer only).
///     - Planted, enemy viewer further away: drawn only faintly, so a sharp eye can just make it out.
/// </summary>
public sealed class SapperTrapVisualsSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var local = _player.LocalEntity;
        var localFriendly = local != null && HasComp<CLFMemberComponent>(local);
        var localCoords = local != null ? Transform(local.Value).Coordinates : default;

        var query = EntityQueryEnumerator<SapperTrapComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var trap, out var sprite, out var xform))
        {
            // Per-viewer proximity reveal: only THIS client's distance matters.
            var nearLocal = local != null &&
                            _transform.InRange(localCoords, xform.Coordinates, trap.RevealRadius);

            var shown = !trap.Deployed || localFriendly || nearLocal;
            var alpha = shown ? 1f : trap.HiddenAlpha;
            if (System.Math.Abs(sprite.Color.A - alpha) > 0.01f)
                sprite.Color = sprite.Color.WithAlpha(alpha);
        }
    }
}
