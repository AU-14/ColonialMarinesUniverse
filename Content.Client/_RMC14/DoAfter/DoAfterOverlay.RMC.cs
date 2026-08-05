using Content.Client._RMC14.NightVision;
using Content.Client.Examine;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.DoAfter;

public sealed partial class DoAfterOverlay
{
    private IOverlayManager _rmcOverlay = default!;
    private ExamineSystem _rmcExamine = default!;
    private EntityQuery<EntityActiveInvisibleComponent> _rmcInvisibleQuery;
    private EntityQuery<XenoComponent> _rmcXenoQuery;

    private void InitializeRMC(IOverlayManager overlay)
    {
        _rmcOverlay = overlay;
        _rmcExamine = _entManager.System<ExamineSystem>();
        _rmcInvisibleQuery = _entManager.GetEntityQuery<EntityActiveInvisibleComponent>();
        _rmcXenoQuery = _entManager.GetEntityQuery<XenoComponent>();
        ZIndex = 1;
    }

    private OverlaySpace GetRMCOverlaySpace()
    {
        return _rmcOverlay.HasOverlay<NightVisionOverlay>()
            ? OverlaySpace.WorldSpace
            : OverlaySpace.WorldSpaceBelowFOV;
    }

    private bool TryGetRMCDoAfterMaxAlpha(
        EntityUid uid,
        EntityUid? localEnt,
        SpriteComponent sprite,
        bool forceVisible,
        ref float maxAlpha)
    {
        if (forceVisible)
            return true;

        if (!sprite.Visible && uid != localEnt)
            return false;

        if (localEnt is { } local &&
            _rmcXenoQuery.HasComponent(local) &&
            !_rmcXenoQuery.HasComponent(uid) &&
            !_rmcExamine.InRangeUnOccluded(uid, local))
        {
            return false;
        }

        maxAlpha = Math.Min(maxAlpha, sprite.Color.A);

        if (_rmcInvisibleQuery.TryComp(uid, out var invisible))
            maxAlpha = Math.Min(maxAlpha, invisible.Opacity);

        return true;
    }
}
