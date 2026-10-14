using Content.Server.Hands.Systems;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._RMC14.CCVar;
using Content.Shared.CMU14.CCVar; // cmu edit
using Content.Shared._RMC14.Medal;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Coordinates;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Medal;

public sealed partial class PlaytimeMedalSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private SharedUniformAccessorySystem _uniformAccessory = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;
    [Dependency] private IPrototypeManager _prototype = default!;


    private TimeSpan _bronzeTime;
    private TimeSpan _silverTime;
    private TimeSpan _goldTime;
    private TimeSpan _platinumTime;
    // cmu edit start: platinum is the highest medal
    // private TimeSpan _rubyTime;
    // private TimeSpan _amethystTime;
    // private TimeSpan _emeraldTime;
    // private TimeSpan _prismaticTime;
    // cmu edit end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        // cmu edit start: CMU medal hours, the rmc cvars still drive xeno rank icons in the stats window
        // Subs.CVar(_config, RMCCVars.RMCPlaytimeBronzeMedalTimeHours, v => _bronzeTime = TimeSpan.FromHours(v), true);
        // Subs.CVar(_config, RMCCVars.RMCPlaytimeSilverMedalTimeHours, v => _silverTime = TimeSpan.FromHours(v), true);
        // Subs.CVar(_config, RMCCVars.RMCPlaytimeGoldMedalTimeHours, v => _goldTime = TimeSpan.FromHours(v), true);
        // Subs.CVar(_config, RMCCVars.RMCPlaytimePlatinumMedalTimeHours, v => _platinumTime = TimeSpan.FromHours(v), true);
        Subs.CVar(_config, AU14CCVars.PlaytimeMedalBronzeHours, v => _bronzeTime = TimeSpan.FromHours(v), true);
        Subs.CVar(_config, AU14CCVars.PlaytimeMedalSilverHours, v => _silverTime = TimeSpan.FromHours(v), true);
        Subs.CVar(_config, AU14CCVars.PlaytimeMedalGoldHours, v => _goldTime = TimeSpan.FromHours(v), true);
        Subs.CVar(_config, AU14CCVars.PlaytimeMedalPlatinumHours, v => _platinumTime = TimeSpan.FromHours(v), true);
        // cmu edit end
        // cmu edit start: platinum is the highest medal
        // Subs.CVar(_config, RMCCVars.RMCPlaytimeRubyMedalTimeHours, v => _rubyTime = TimeSpan.FromHours(v), true);
        // Subs.CVar(_config, RMCCVars.RMCPlaytimeAmethystMedalTimeHours, v => _amethystTime = TimeSpan.FromHours(v), true);
        // Subs.CVar(_config, RMCCVars.RMCPlaytimeEmeraldMedalTimeHours, v => _emeraldTime = TimeSpan.FromHours(v), true);
        // Subs.CVar(_config, RMCCVars.RMCPlaytimePrismaticMedalTimeHours, v => _prismaticTime = TimeSpan.FromHours(v), true);
        // cmu edit end
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!ev.Profile.PlaytimePerks)
            return;

        if (ev.JobId == null ||
            !_prototype.TryIndex(ev.JobId, out JobPrototype? job) ||
            !_playTimeTracking.TryGetTrackerTime(ev.Player, job.PlayTimeTracker, out var time))
        {
            return;
        }

        if (job.Medals is not { } medals)
            return;

        RMCPlaytimeMedalType? medalType = null;

        // cmu edit start: ruby, amethyst, emerald and prismatic are no longer awarded
        // if (time >= _prismaticTime)
        //     medalType = RMCPlaytimeMedalType.Prismatic;
        // else if (time >= _emeraldTime)
        //     medalType = RMCPlaytimeMedalType.Emerald;
        // else if (time >= _amethystTime)
        //     medalType = RMCPlaytimeMedalType.Amethyst;
        // else if (time >= _rubyTime)
        //     medalType = RMCPlaytimeMedalType.Ruby;
        // else if (time >= _platinumTime)
        if (time >= _platinumTime)
        // cmu edit end
            medalType = RMCPlaytimeMedalType.Platinum;
        else if (time >= _goldTime)
            medalType = RMCPlaytimeMedalType.Gold;
        else if (time >= _silverTime)
            medalType = RMCPlaytimeMedalType.Silver;
        else if (time >= _bronzeTime)
            medalType = RMCPlaytimeMedalType.Bronze;

        if (medalType == null)
            return;

        if (!medals.TryGetValue(medalType.Value, out var medalId))
            return;

        var medal = SpawnAtPosition(medalId, ev.Mob.ToCoordinates());
        // Try to insert into a valid accessory slot. Otherwise, inserts it into the player's hands.
        if (!_uniformAccessory.TryInsertToValidSlot(medal, ev.Mob))
            _hands.TryPickupAnyHand(ev.Mob, medal, false);
        var medalComp = EnsureComp<UniformAccessoryComponent>(medal);
        medalComp.User = GetNetEntity(ev.Mob);
        Dirty(medal, medalComp);
    }
}
