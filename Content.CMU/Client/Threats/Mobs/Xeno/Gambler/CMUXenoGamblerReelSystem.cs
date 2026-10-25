using System.Linq;
using System.Numerics;
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

public sealed class CMUXenoGambleReelStrip(List<CMUXenoGambleReelEntry> entries)
{
    public readonly List<CMUXenoGambleReelEntry> Entries = entries;

    public CMUXenoGambleReelEntry Get(int k)
    {
        return Entries[Math.Clamp(k + 1, 0, Entries.Count - 1)];
    }
}

public sealed partial class CMUXenoGamblerReelSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    public const string FrameLayer = "cmu-gamble-frame";
    public const string FlashLayer = "cmu-gamble-flash";

    private readonly Dictionary<EntityUid, (TimeSpan Start, List<CMUXenoGambleReelStrip> Reels)> _strips = new();
    private readonly List<EntityUid> _stale = new();

    public override void Initialize()
    {
        _overlays.AddOverlay(new CMUXenoGamblerReelOverlay(EntityManager, this));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<CMUXenoGamblerReelOverlay>();
    }

    /// <summary>Gets spinning gamblers reel columns see <see cref="CMUXenoGambleSpin"/> for how they roll</summary>
    public bool TryGetSpin(EntityUid uid, CMUXenoGamblerComponent gambler, out List<CMUXenoGambleReelStrip> reels)
    {
        reels = default!;
        if (gambler.SpinStart is not { } start || gambler.SpinRolls.Count == 0)
            return false;

        if (!_strips.TryGetValue(uid, out var cached) || cached.Start != start)
        {
            cached = (start, BuildReels(gambler));
            _strips[uid] = cached;
        }

        reels = cached.Reels;
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
            gambler.SpinStart is not { } start ||
            !TryGetSpin(player, gambler, out var strips))
        {
            return;
        }

        var time = _timing.CurTime;
        for (var i = 0; i < gambler.Reels.Count; i++)
        {
            var reel = gambler.Reels[i];
            if (i >= strips.Count || !TryComp(reel, out SpriteComponent? sprite))
                continue;

            // Hotbar shows whatever is on the winning line
            var position = CMUXenoGambleSpin.GetReelPosition(gambler, start, time, i);
            var entry = strips[i].Get((int) Math.Round(position));
            var pop = GetPop(gambler, i, time);
            var scale = new Vector2(1 + gambler.ReelPopScale * pop);

            if (_sprite.TryGetLayer((reel, sprite), ActionVisuals.Icon, out var icon, false))
            {
                _sprite.LayerSetTexture(icon, entry.Icon);
                _sprite.LayerSetScale(icon, scale);
            }

            if (_sprite.TryGetLayer((reel, sprite), FrameLayer, out var frame, false))
            {
                _sprite.LayerSetTexture(frame, GetFrame(entry.Frame, time));
                _sprite.LayerSetScale(frame, scale);
                _sprite.LayerSetVisible(frame, true);
            }

            if (_sprite.TryGetLayer((reel, sprite), FlashLayer, out var flash, false))
            {
                _sprite.LayerSetScale(flash, scale);
                _sprite.LayerSetColor(flash, GetPopColor(gambler, i, time).WithAlpha(pop * 0.85f));
                _sprite.LayerSetVisible(flash, pop > 0);
            }
        }
    }

    /// <summary> pool tier & reward reel lands on</summary>
    private bool TryGetWinner(CMUXenoGamblerComponent gambler,
        int reel,
        out CMUXenoGambleTier tier,
        out CMUXenoGambleReward reward)
    {
        tier = default!;
        reward = default!;
        return reel < gambler.SpinRolls.Count &&
               _prototypes.TryIndex(gambler.Pool, out var pool) &&
               gambler.SpinRolls[reel].TryGet(pool, out tier, out reward);
    }

    public Color GetPopColor(CMUXenoGamblerComponent gambler, int reel, TimeSpan time)
    {
        if (!TryGetWinner(gambler, reel, out var tier, out _))
            return Color.White;

        if (!tier.StopPopRainbow)
            return tier.StopPopColor;

        var hue = (float) (time.TotalSeconds * 2 % 1);
        return Color.FromHsv(new Vector4(hue, 0.7f, 1f, 1f));
    }

    public float GetPop(CMUXenoGamblerComponent gambler, int reel, TimeSpan time)
    {
        if (gambler.SpinStart is not { } start ||
            gambler.ReelPopDuration <= TimeSpan.Zero ||
            !TryGetWinner(gambler, reel, out var tier, out _))
        {
            return 0;
        }

        var since = time - CMUXenoGambleSpin.GetStopTime(gambler, start, reel);
        if (since < TimeSpan.Zero || since >= gambler.ReelPopDuration)
            return 0;

        var remaining = 1 - (float) (since / gambler.ReelPopDuration);
        return tier.StopPop * remaining * remaining;
    }

    private List<CMUXenoGambleReelStrip> BuildReels(CMUXenoGamblerComponent gambler)
    {
        _prototypes.TryIndex(gambler.Pool, out var pool);

        var reels = new List<CMUXenoGambleReelStrip>();
        for (var i = 0; i < gambler.SpinRolls.Count; i++)
        {
            // Icons -1 through steps + 1, with winner on the line once reel stops
            var steps = CMUXenoGambleSpin.GetReelSteps(gambler, i);
            var entries = new List<CMUXenoGambleReelEntry>(steps + 3);
            for (var k = -1; k <= steps + 1; k++)
            {
                entries.Add(PickFiller(gambler, pool));
            }

            if (TryGetWinner(gambler, i, out var tier, out var reward))
                entries[steps + 1] = new CMUXenoGambleReelEntry(_sprite.Frame0(reward.Icon), tier.Overlay ?? gambler.DefaultOverlay);
            reels.Add(new CMUXenoGambleReelStrip(entries));
        }

        return reels;
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
