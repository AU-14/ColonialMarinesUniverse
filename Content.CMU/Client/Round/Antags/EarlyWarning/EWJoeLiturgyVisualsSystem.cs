using Content.Client.Body;
using Content.Shared.Body;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// ARCH-05 (node N12): the Joe's eyes step towards full orange with its Liturgy stacks, so the people it fights
/// can read the meter. Client tint only, driven by <see cref="EWJoeLiturgyGlowComponent"/>, which the server adds
/// only while the meter is up: the eye layer's colour is overridden while it exists, and given back from the eye
/// organ's own data (the RMC intents colour the server set) when it goes.
/// </summary>
/// <remarks>
/// RMCIntentsEyeColor sets the eye colour on the server through the body's organ profiles; every change it makes
/// reaches the client as an organ state, which re-applies the layer and raises <see cref="VisualBodySpriteRefreshEvent"/>.
/// This system re-tints after that refresh instead of competing with it, and touches nothing at zero stacks.
/// </remarks>
public sealed class EWJoeLiturgyVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly HashSet<EntityUid> _pending = new();
    private readonly HashSet<EntityUid> _tinted = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeLiturgyGlowComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EWJoeLiturgyGlowComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<EWJoeLiturgyGlowComponent, VisualBodySpriteRefreshEvent>(OnBodyRefresh);
        SubscribeLocalEvent<EWJoeLiturgyGlowComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<EWJoeLiturgyGlowComponent> ent, ref ComponentStartup args)
    {
        _pending.Add(ent);
    }

    private void OnState(Entity<EWJoeLiturgyGlowComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _pending.Add(ent);
    }

    private void OnBodyRefresh(Entity<EWJoeLiturgyGlowComponent> ent, ref VisualBodySpriteRefreshEvent args)
    {
        _pending.Add(ent);
    }

    private void OnShutdown(Entity<EWJoeLiturgyGlowComponent> ent, ref ComponentShutdown args)
    {
        _pending.Remove(ent);
        if (_tinted.Remove(ent) && !TerminatingOrDeleted(ent))
            RestoreEyes(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_pending.Count == 0)
            return;

        foreach (var uid in _pending)
        {
            if (!TryComp(uid, out EWJoeLiturgyGlowComponent? liturgy) || TerminatingOrDeleted(uid))
            {
                _tinted.Remove(uid);
                continue;
            }

            if ((liturgy.Stacks > 0 || liturgy.Exalted) && liturgy.EyeColors.Count > 0)
            {
                var index = Math.Clamp(liturgy.Stacks, 1, liturgy.EyeColors.Count) - 1;
                if (liturgy.Exalted)
                    index = liturgy.EyeColors.Count - 1;

                if (SetEyes(uid, liturgy.EyeColors[index]))
                    _tinted.Add(uid);
            }
            else if (_tinted.Remove(uid))
            {
                RestoreEyes(uid);
            }
        }

        _pending.Clear();
    }

    private bool SetEyes(EntityUid uid, Color color)
    {
        if (!TryComp(uid, out SpriteComponent? sprite) ||
            !_sprite.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.Eyes, out var layer, false))
        {
            return false;
        }

        _sprite.LayerSetColor((uid, sprite), layer, color);
        return true;
    }

    /// <summary>Gives the eye layer back the colour its organ carries.</summary>
    private void RestoreEyes(EntityUid uid)
    {
        var color = Color.White;
        var query = EntityQueryEnumerator<VisualOrganComponent, OrganComponent>();
        while (query.MoveNext(out _, out var visual, out var organ))
        {
            // read into locals: the organ's fields are the visual body system's to act on
            Enum organLayer = visual.Layer;
            if (organ.Body != uid || !organLayer.Equals(HumanoidVisualLayers.Eyes))
                continue;

            var data = visual.Data;
            color = data.Color ?? Color.White;
            break;
        }

        SetEyes(uid, color);
    }
}
