using Content.Shared._RMC14.BlurredVision;
using Content.Shared._RMC14.NightVision;
using Content.Shared.Examine;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Clothing.NightVision;

/// <summary>
///     Runs <see cref="CMUNightVisionFlareBlindComponent"/>: a lit flare near someone seeing through the goggles
///     blinds them for a few seconds.
/// </summary>
public sealed class CMUNightVisionFlareBlindSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.25);
    private const string BlindKey = "Blinded";
    private static readonly ProtoId<TagPrototype> FlareTag = "RMCFlare";

    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<MapCoordinates> _litFlares = new();
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        // Blinding is the server's call; clients see the status effect it adds.
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        // Only flares count, not glowsticks. They are few, so gather the lit ones once, wherever they are:
        // on the floor, thrown or held.
        _litFlares.Clear();
        var flares = EntityQueryEnumerator<ExpendableLightComponent, TransformComponent>();
        while (flares.MoveNext(out var flare, out var light, out var xform))
        {
            if (light.Activated && xform.MapID != MapId.Nullspace && _tags.HasTag(flare, FlareTag))
                _litFlares.Add(_transform.GetMapCoordinates(flare, xform));
        }

        if (_litFlares.Count == 0)
            return;

        var goggles = EntityQueryEnumerator<CMUNightVisionFlareBlindComponent, NightVisionItemComponent>();
        while (goggles.MoveNext(out var uid, out var comp, out var nightVision))
        {
            if (nightVision.User is not { } wearer || now < comp.NextBlind)
                continue;

            if (!FlareNearby(wearer, comp.Range))
                continue;

            Blind((uid, comp), wearer);
        }
    }

    /// <summary>Whites the goggles out, blinding whoever is looking through them.</summary>
    public void Blind(Entity<CMUNightVisionFlareBlindComponent> ent, EntityUid wearer)
    {
        ent.Comp.NextBlind = _timing.CurTime + ent.Comp.BlindDuration + ent.Comp.Cooldown;
        _statusEffects.TryAddStatusEffect<RMCBlindedComponent>(wearer, BlindKey, ent.Comp.BlindDuration, true);
        _popup.PopupEntity(Loc.GetString("cmu-nvg-flare-blind", ("item", ent.Owner)), wearer, wearer, PopupType.MediumCaution);
    }

    private bool FlareNearby(EntityUid wearer, float range)
    {
        var position = _transform.GetMapCoordinates(wearer);
        foreach (var flare in _litFlares)
        {
            if (flare.MapId == position.MapId &&
                (flare.Position - position.Position).Length() <= range &&
                _examine.InRangeUnOccluded(position, flare, range, null))
            {
                return true;
            }
        }

        return false;
    }
}
