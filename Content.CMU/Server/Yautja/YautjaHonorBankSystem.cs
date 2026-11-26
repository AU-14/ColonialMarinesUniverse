using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Yautja;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Shared.Network;

namespace Content.Server.CMU14.Yautja;

// the round's trophy score used to vanish at round end, so stored honor (and both clan hall
// boards) never moved. now it's banked into the hunter and their clan once the round is over
public sealed partial class YautjaHonorBankSystem : EntitySystem
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private YautjaClanManager _clans = default!;

    private ISawmill _sawmill = default!;

    // honour is owed to the player, not to whatever body they end the round in. a hunter who
    // self-destructed, got gibbed or ghosted out still keeps what they earned (cmss13 adds it on the spot)
    private readonly Dictionary<NetUserId, int> _roundScores = new();

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("cmu.yautja.honor");
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.PostRound)
            return;

        _ = BankAll(CollectScores());
        _roundScores.Clear();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _roundScores.Clear();
    }

    public void Credit(EntityUid hunter, int score)
    {
        if (score <= 0 ||
            !_mind.TryGetMind(hunter, out _, out var mind) ||
            (mind.OriginalOwnerUserId ?? mind.UserId) is not { } user)
        {
            return;
        }

        _roundScores[user] = _roundScores.GetValueOrDefault(user) + score;
    }

    public Dictionary<NetUserId, int> CollectScores()
    {
        return new Dictionary<NetUserId, int>(_roundScores);
    }

    public async Task BankAll(Dictionary<NetUserId, int> scores)
    {
        foreach (var (user, score) in scores)
        {
            try
            {
                // only real hunters. a non-whitelisted youngblood doesn't get a clan profile out of one round
                if (await _db.GetYautjaWhitelistFlagsAsync(user.UserId) == (int) YautjaWhitelistFlags.None &&
                    await _db.GetYautjaClanMemberAsync(user.UserId) == null)
                {
                    continue;
                }

                if (await _db.AddYautjaHonorAsync(user.UserId, score))
                    _clans.InvalidateCache(user);
            }
            catch (System.Exception e)
            {
                _sawmill.Error($"Failed to bank {score} Yautja honor for {user}:\n{e}");
            }
        }

        if (scores.Count > 0)
            _clans.NotifyChanged();
    }
}
