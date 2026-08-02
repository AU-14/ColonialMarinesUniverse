using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server._CMU14.Yautja;
using Content.Server._RMC14.LinkAccount;
using Content.Shared._RMC14.LinkAccount;
using Content.Shared._CMU14.Yautja;
using Content.Shared.CCVar;
using Content.Shared.Players.JobWhitelist;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Serilog;

namespace Content.Server.Players.JobWhitelist;

public sealed partial class JobWhitelistManager : IPostInjectInit
{
    private const string YautjaHunterJob = "CMUYautjaHunter";
    private const YautjaWhitelistFlags HunterWhitelistFlags =
        YautjaWhitelistFlags.Yautja |
        YautjaWhitelistFlags.Legacy |
        YautjaWhitelistFlags.Council |
        YautjaWhitelistFlags.CouncilLegacy |
        YautjaWhitelistFlags.Leader;

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private LinkAccountManager _linkAccount = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    [Dependency] private YautjaRankManager _yautjaRank = default!;

    private readonly Dictionary<NetUserId, HashSet<string>> _whitelists = new();
    private readonly Dictionary<NetUserId, YautjaWhitelistFlags> _yautjaWhitelistFlags = new();

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgJobWhitelist>();
    }

    private async Task LoadData(ICommonSession session, CancellationToken cancel)
    {
        var whitelists = await _db.GetJobWhitelists(session.UserId, cancel);
        cancel.ThrowIfCancellationRequested();
        _whitelists[session.UserId] = whitelists.ToHashSet();
        _yautjaWhitelistFlags[session.UserId] =
            (YautjaWhitelistFlags) await _db.GetYautjaWhitelistFlagsAsync(session.UserId.UserId);
    }

    private void FinishLoad(ICommonSession session)
    {
        SendJobWhitelist(session);
    }

    private void ClientDisconnected(ICommonSession session)
    {
        _whitelists.Remove(session.UserId);
        _yautjaWhitelistFlags.Remove(session.UserId);
    }

    public async void AddWhitelist(NetUserId player, ProtoId<JobPrototype> job)
    {
        if (_whitelists.TryGetValue(player, out var whitelists))
            whitelists.Add(job);

        await _db.AddJobWhitelist(player, job);

        if (_player.TryGetSessionById(player, out var session))
            SendJobWhitelist(session);
    }

    public bool IsAllowed(ICommonSession session, ProtoId<JobPrototype> job)
    {
        if (!_config.GetCVar(CCVars.GameRoleWhitelist))
            return true;

        // RMC14-Whitelist-Tweak-Start
        if (!_prototypes.TryIndex(job, out var jobPrototype))
            return true;

        if (!jobPrototype.Whitelisted)
            return true;

        if (BoostyYautjaWhitelist.IsAllowed(job, _linkAccount.GetConnectedPatron(session.UserId)?.Tier?.Priority))
            return true;

        if (IsWhitelisted(session.UserId, job))
            return true;

        if (jobPrototype.WhitelistParent != null)
            return IsAllowed(session, jobPrototype.WhitelistParent.Value);

        return false;
        // RMC14-Whitelist-Tweak-End
    }

    public bool IsWhitelisted(NetUserId player, ProtoId<JobPrototype> job)
    {
        if (!_whitelists.TryGetValue(player, out var whitelists))
        {
            Log.Error("Unable to check if player {Player} is whitelisted for {Job}. Stack trace:\\n{StackTrace}",
                player,
                job,
                Environment.StackTrace);
            return AllowsYautjaHunter(player, job);
        }

        return whitelists.Contains(job) || AllowsYautjaHunter(player, job);
    }

    public async Task RefreshYautjaWhitelist(NetUserId player)
    {
        await _yautjaRank.Refresh(player);
        _yautjaWhitelistFlags[player] =
            (YautjaWhitelistFlags) await _db.GetYautjaWhitelistFlagsAsync(player.UserId);

        if (_player.TryGetSessionById(player, out var session))
            SendJobWhitelist(session);
    }

    public async void RemoveWhitelist(NetUserId player, ProtoId<JobPrototype> job)
    {
        _whitelists.GetValueOrDefault(player)?.Remove(job);
        await _db.RemoveJobWhitelist(player, job);

        if (_player.TryGetSessionById(new NetUserId(player), out var session))
            SendJobWhitelist(session);
    }

    public void SendJobWhitelist(ICommonSession player)
    {
        var whitelist = _whitelists.GetValueOrDefault(player.UserId)?.ToHashSet() ?? new HashSet<string>();
        if (AllowsYautjaHunter(player.UserId, YautjaHunterJob))
            whitelist.Add(YautjaHunterJob);

        var msg = new MsgJobWhitelist
        {
            Whitelist = whitelist,
            YautjaCapabilities = _yautjaRank.ResolveProfileCapabilitiesCached(player.UserId),
        };

        _net.ServerSendMessage(msg, player.Channel);
    }

    private bool AllowsYautjaHunter(NetUserId player, ProtoId<JobPrototype> job)
    {
        if (job.Id != YautjaHunterJob)
            return false;

        if (BoostyYautjaWhitelist.IsAllowed(job.Id, _linkAccount.GetConnectedPatron(player)?.Tier?.Priority))
            return true;

        return _yautjaWhitelistFlags.TryGetValue(player, out var flags) &&
               (flags & HunterWhitelistFlags) != YautjaWhitelistFlags.None;
    }

    void IPostInjectInit.PostInject()
    {
        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnFinishLoad(FinishLoad);
        _userDb.AddOnPlayerDisconnect(ClientDisconnected);
    }
}
