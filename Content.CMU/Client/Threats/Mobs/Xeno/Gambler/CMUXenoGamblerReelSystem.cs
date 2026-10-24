using System.Linq;
using Content.Shared.Actions;
using Content.Shared.CMU14.Threats.Mobs.Xeno.Gambler;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Threats.Mobs.Xeno.Gambler;

/// <summary> slot on gamblers reel strip</summary>
public readonly record struct CMUXenoGambleReelEntry(Texture Icon, SpriteSpecifier Frame);

public sealed partial class CMUXenoGamblerReelSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    public const string WonLayer = "cmu-gamble-won";

    private readonly Dictionary<EntityUid, (TimeSpan Start, List<CMUXenoGambleReelEntry> Strip)> _strips = new();
    private readonly List<EntityUid> _stale = new();

    public override void Initialize()
    {
        _overlays.AddOverlay(new CMUXenoGamblerReelOverlay(EntityManager, this));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<CMUXenoGamblerReelOverlay>();
    }

    /// <summary>
    /// Currently on reel, see <see cref="CMUXenoGambleSpin.GetStripIndex"/> for
    /// which slot each reel shows.
    /// </summary>
    public bool TryGetSpin(EntityUid uid,
        CMUXenoGamblerComponent gambler,
        out List<CMUXenoGambleReelEntry> strip,
        out int step)
    {
        strip = default!;
        step = 0;

        if (gambler.SpinStart is not { } start || gambler.WinnerIcons.Count == 0)
            return false;

        if (!_strips.TryGetValue(uid, out var cached) || cached.Start != start)
        {
            cached = (start, BuildStrip(gambler));
            _strips[uid] = cached;
        }

        strip = cached.Strip;

        step = CMUXenoGambleSpin.GetStep(gambler, start, _timing.CurTime, gambler.WinnerIcons.Count);
        return true;
    }

    public Texture GetFrame(SpriteSpecifier specifier, TimeSpan time)
    {
        if (specifier is not SpriteSpecifier.Rsi rsi ||
            !_resources.TryGetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / rsi.RsiPath, out var resource) ||
            !resource.RSI.TryGetState(rsi.RsiState, out var state))
        {
            return _sprite.Frame0(specifier);
        }

        var frames = state.GetFrames(RsiDirection.South);
        var delays = state.GetDelays();
        var total = delays.Sum();
        if (frames.Length <= 1 || total <= 0)
            return state.Frame0;

        var elapsed = time.TotalSeconds % total;
        for (var i = 0; i < delays.Length; i++)
        {
            elapsed -= delays[i];
            if (elapsed < 0)
                return frames[i];
        }

        return frames[^1];
    }

    public override void FrameUpdate(float frameTime)
    {
        _stale.Clear();
        foreach (var (uid, cached) in _strips)
        {
            if (!TryComp(uid, out CMUXenoGamblerComponent? spinning) || spinning.SpinStart != cached.Start)
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            _strips.Remove(uid);
        }

        if (_player.LocalEntity is not { } player ||
            !TryComp(player, out CMUXenoGamblerComponent? gambler) ||
            gambler.Reels.Count == 0 ||
            !TryGetSpin(player, gambler, out var strip, out var step))
        {
            return;
        }

        var time = _timing.CurTime;
        for (var i = 0; i < gambler.Reels.Count; i++)
        {
            var reel = gambler.Reels[i];
            var index = CMUXenoGambleSpin.GetStripIndex(gambler, i, step);
            if (index >= strip.Count || !TryComp(reel, out SpriteComponent? sprite))
                continue;

            var entry = strip[index];
            _sprite.LayerSetTexture((reel, sprite), ActionVisuals.Icon, entry.Icon);

            if (!_sprite.TryGetLayer((reel, sprite), WonLayer, out var frame, false))
                continue;

            _sprite.LayerSetTexture(frame, GetFrame(entry.Frame, time));
            _sprite.LayerSetVisible(frame, true);
        }
    }

    private List<CMUXenoGambleReelEntry> BuildStrip(CMUXenoGamblerComponent gambler)
    {
        _prototypes.TryIndex(gambler.Pool, out var pool);

        var reels = gambler.WinnerIcons.Count;
        var strip = new List<CMUXenoGambleReelEntry>(CMUXenoGambleSpin.GetStripLength(gambler, reels));
        for (var i = 0; i < CMUXenoGambleSpin.GetStripLength(gambler, reels); i++)
        {
            strip.Add(PickFiller(gambler, pool));
        }

        for (var i = 0; i < reels; i++)
        {
            var frame = i < gambler.WinnerOverlays.Count ? gambler.WinnerOverlays[i] : gambler.DefaultOverlay;
            strip[CMUXenoGambleSpin.GetWinnerIndex(gambler, i)] =
                new CMUXenoGambleReelEntry(_sprite.Frame0(gambler.WinnerIcons[i]), frame);
        }

        return strip;
    }

    /// <summary>
    /// Filler slots (win already pre determined)
    /// </summary>
    private CMUXenoGambleReelEntry PickFiller(CMUXenoGamblerComponent gambler, CMUXenoGamblePoolPrototype? pool)
    {
        var blank = new CMUXenoGambleReelEntry(_sprite.Frame0(gambler.BlankIcon), gambler.DefaultOverlay);
        if (pool == null)
            return blank;

        var total = pool.Tiers.Where(t => t.Rewards.Count > 0).Sum(t => t.Weight);
        if (total <= 0)
            return blank;

        var pick = _random.NextFloat(total);
        foreach (var tier in pool.Tiers)
        {
            if (tier.Rewards.Count == 0)
                continue;

            pick -= tier.Weight;
            if (pick >= 0)
                continue;

            var reward = _random.Pick(tier.Rewards);
            return new CMUXenoGambleReelEntry(_sprite.Frame0(reward.Icon), tier.Overlay ?? gambler.DefaultOverlay);
        }

        return blank;
    }
}
