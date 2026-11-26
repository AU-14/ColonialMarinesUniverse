using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Dataset;
using Content.Shared.Tips;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Tips;

public sealed partial class TipsSystem : SharedTipsSystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private GameTicker _ticker = default!;

    private bool _tipsEnabled;
    private float _tipTimeOutOfRound;
    private float _tipTimeInRound;
    private string _tipsDataset = "";
    private string _cmuTipsDataset = ""; // CMU14: extra tips dataset merged into the draw
    private float _tipTippyChance;

    [ViewVariables(VVAccess.ReadWrite)]
    private TimeSpan _nextTipTime = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnGameRunLevelChanged);
        Subs.CVar(_cfg, CCVars.TipsEnabled, SetEnabled, true);
        Subs.CVar(_cfg, CCVars.TipFrequencyOutOfRound, value => _tipTimeOutOfRound = value, true);
        Subs.CVar(_cfg, CCVars.TipFrequencyInRound, value => _tipTimeInRound = value, true);
        Subs.CVar(_cfg, CCVars.TipsDataset, value => _tipsDataset = value, true);
        Subs.CVar(_cfg, CCVars.CmuTipsDataset, value => _cmuTipsDataset = value, true); // CMU14
        Subs.CVar(_cfg, CCVars.TipsTippyChance, value => _tipTippyChance = value, true);

        RecalculateNextTipTime();
    }

    private void OnGameRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        // reset for lobby -> inround
        // reset for inround -> post but not post -> lobby
        if (ev.New == GameRunLevel.InRound || ev.Old == GameRunLevel.InRound)
        {
            RecalculateNextTipTime();
        }
    }

    private void SetEnabled(bool value)
    {
        _tipsEnabled = value;

        if (_nextTipTime != TimeSpan.Zero)
            RecalculateNextTipTime();
    }

    public override void RecalculateNextTipTime()
    {
        if (_ticker.RunLevel == GameRunLevel.InRound)
        {
            _nextTipTime = _timing.CurTime + TimeSpan.FromSeconds(_tipTimeInRound);
        }
        else
        {
            _nextTipTime = _timing.CurTime + TimeSpan.FromSeconds(_tipTimeOutOfRound);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_tipsEnabled)
            return;

        if (_nextTipTime != TimeSpan.Zero && _timing.CurTime > _nextTipTime)
        {
            AnnounceRandomTip();
            RecalculateNextTipTime();
        }
    }

    public override void SendTippy(
        string message,
        EntProtoId? prototype = null,
        float speakTime = 5f,
        float slideTime = 3f,
        float waddleInterval = 0.5f)
    {
        var ev = new TippyEvent(message, prototype, speakTime, slideTime, waddleInterval);
        RaiseNetworkEvent(ev);
    }

    public override void SendTippy(
        ICommonSession session,
        string message,
        EntProtoId? prototype = null,
        float speakTime = 5f,
        float slideTime = 3f,
        float waddleInterval = 0.5f)
    {
        var ev = new TippyEvent(message, prototype, speakTime, slideTime, waddleInterval);
        RaiseNetworkEvent(ev, session);
    }

    public override void AnnounceRandomTip()
    {
        if (!ProtoMan.TryIndex<LocalizedDatasetPrototype>(_tipsDataset, out var tips))
            return;

        // CMU14 Begin: merge the CMU tips dataset into the draw pool
        IReadOnlyList<string> values = tips.Values;
        if (!string.IsNullOrEmpty(_cmuTipsDataset)
            && ProtoMan.TryIndex<LocalizedDatasetPrototype>(_cmuTipsDataset, out var cmuTips)
            && cmuTips.Values.Count > 0)
        {
            var merged = new List<string>(values.Count + cmuTips.Values.Count);
            merged.AddRange(values);
            merged.AddRange(cmuTips.Values);
            values = merged;
        }
        // CMU14 End

        var tip = _random.Pick(values); // CMU14
        var msg = Loc.GetString("tips-system-chat-message-wrap", ("tip", Loc.GetString(tip)));

        if (_random.Prob(_tipTippyChance))
        {
            var speakTime = GetSpeechTime(msg);
            SendTippy(msg, speakTime: speakTime);
        }
        else
        {
            _chat.ChatMessageToManyFiltered(
                Filter.Broadcast(),
                ChatChannel.OOC,
                tip,
                msg,
                EntityUid.Invalid,
                false,
                false,
                Color.MediumPurple);
        }
    }
}
