#nullable enable
using Content.Shared.CMU14.BalanceRating;
using Content.Shared.CCVar;
using Robust.Shared;

namespace Content.IntegrationTests;

// Partial class containing test cvars
// This could probably be merged into the main file, but I'm keeping it separate to reduce
// conflicts for forks.
public static partial class PoolManager
{
    public static readonly (string cvar, string value)[] TestCvars =
    {
        // CMU14: the engine pool defaults this to 1, which caps the entire .NET worker pool when
        // CI sets DOTNET_PROCESSOR_COUNT=1. Prototype startup then deadlocks on queued worker tasks.
        // Keep the worker pool unrestricted while retaining CI's single-worker PLINQ workaround.
        (CVars.ThreadParallelCount.Name, "0"),
        // @formatter:off
        (CCVars.DatabaseSynchronous.Name,     "true"),
        (CCVars.DatabaseSnapshot.Name,        "true"),
        (CCVars.DatabaseSqliteDelay.Name,     "0"),
        (CCVars.HolidaysEnabled.Name,         "false"),
        (CCVars.GameMap.Name,                 TestMap),
        (CCVars.AdminLogsQueueSendDelay.Name, "0"),
        (CCVars.NPCMaxUpdates.Name,           "999999"),
        (CCVars.GameRoleTimers.Name,          "false"),
        (CCVars.GameRoleLoadoutTimers.Name,   "false"),
        (CCVars.GameRoleWhitelist.Name,       "false"),
        (CCVars.GridFill.Name,                "false"),
        (CCVars.PreloadGrids.Name,            "false"),
        (CCVars.ArrivalsShuttles.Name,        "false"),
        (CCVars.EmergencyShuttleEnabled.Name, "false"),
        (CCVars.ProcgenPreload.Name,          "false"),
        (CCVars.GameDummyTicker.Name,         "true"),
        (CCVars.GameLobbyEnabled.Name,        "false"),
        (CCVars.ConfigPresetDevelopment.Name, "false"),
        (CCVars.AdminLogsEnabled.Name,        "false"),
        (CCVars.AutosaveEnabled.Name,         "false"),
        (CCVars.InteractionRateLimitCount.Name, "9999999"),
        (CCVars.InteractionRateLimitPeriod.Name, "0.1"),
        (CCVars.MovementMobPushing.Name,       "false"),
        (CCVars.ResourceUploadingStoreDeletionDays.Name, "0"),
        (CMUBalanceRatingCVars.AutomaticEnabled.Name, "false"),
    };
}
