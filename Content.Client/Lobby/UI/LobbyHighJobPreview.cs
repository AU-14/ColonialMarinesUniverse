using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

internal sealed class LobbyHighJobPreviewEntry
{
    public LobbyHighJobPreviewEntry(JobPrototype job, IReadOnlyList<string> gamemodeLabels)
    {
        Job = job;
        GamemodeLabels = gamemodeLabels;
    }

    public JobPrototype Job { get; }
    public IReadOnlyList<string> GamemodeLabels { get; }

    public string JobName => LobbyHighJobPreview.GetDisplayJobName(Job);

    public string DisplayName
    {
        get
        {
            if (GamemodeLabels.Count == 0)
                return JobName;

            return $"{string.Join("+", GamemodeLabels)} / {JobName}";
        }
    }

    public string Signature => $"{Job.ID}:{string.Join("+", GamemodeLabels)}";
}

internal static class LobbyHighJobPreview
{
    private static readonly string[] HiddenFactionSuffixes =
    {
        " (GOVFOR)",
        " (OPFOR)"
    };

    // Every gamePreset in _CMU14/RoundSetup/GameModes/game_presets.yml. Missing entries here mean
    // a character whose high-priority jobs are only set for that mode shows no job at all.
    private static readonly (string Key, string Label)[] Gamemodes =
    {
        ("ForceOnForce", "FOF"),
        ("Insurgency", "INS"),
        ("DistressSignal", "DS"),
        ("ColonyFall", "CF"),
        ("Jailbreak", "JB"),
        ("Prometheus", "PRO"),
        ("Criminal", "CRI")
    };

    public static string GetDisplayJobName(JobPrototype job)
    {
        var name = !string.IsNullOrWhiteSpace(job.SpawnMenuRoleName)
            ? (IoCManager.Resolve<ILocalizationManager>().TryGetString(job.SpawnMenuRoleName, out var loc) ? loc : job.SpawnMenuRoleName)
            : job.LocalizedName;
        return TrimHiddenFactionSuffix(name);
    }

    public static List<LobbyHighJobPreviewEntry> GetHighPriorityJobs(
        HumanoidCharacterProfile profile,
        IPrototypeManager prototypeManager)
    {
        var jobOrder = new List<string>();
        var jobs = new Dictionary<string, JobPrototype>();
        var gamemodeLabels = new Dictionary<string, List<string>>();

        foreach (var (gamemode, label) in Gamemodes)
        {
            foreach (var (jobId, priority) in profile.GetJobPrioritiesForGamemode(gamemode))
            {
                if (priority != JobPriority.High ||
                    !prototypeManager.TryIndex(jobId, out JobPrototype? job))
                {
                    continue;
                }

                if (!jobs.ContainsKey(job.ID))
                {
                    jobOrder.Add(job.ID);
                    jobs[job.ID] = job;
                    gamemodeLabels[job.ID] = new List<string>();
                }

                var labels = gamemodeLabels[job.ID];
                if (!labels.Contains(label))
                    labels.Add(label);
            }
        }

        var entries = new List<LobbyHighJobPreviewEntry>();
        foreach (var jobId in jobOrder)
        {
            var labels = gamemodeLabels[jobId];

            // A job high-priority in every mode needs no labels - that is the common case, since
            // GetJobPrioritiesForGamemode falls back to the character's general priorities for any
            // mode they haven't customised. Listing all of them would just be noise.
            if (labels.Count == Gamemodes.Length)
                labels = new List<string>();

            entries.Add(new LobbyHighJobPreviewEntry(jobs[jobId], labels));
        }

        // Nothing marked High anywhere still leaves a sprite on screen, so name whatever job the
        // character actually has rather than showing an unexplained figure.
        if (entries.Count == 0 && TryGetFallbackJob(profile, prototypeManager) is { } fallback)
            entries.Add(fallback);

        return entries;
    }

    private static LobbyHighJobPreviewEntry? TryGetFallbackJob(
        HumanoidCharacterProfile profile,
        IPrototypeManager prototypeManager)
    {
        // Passing null gets the character's general priorities rather than any per-mode overrides.
        var priorities = profile.GetJobPrioritiesForGamemode(null);

        foreach (var wanted in new[] { JobPriority.High, JobPriority.Medium, JobPriority.Low })
        {
            foreach (var (jobId, priority) in priorities)
            {
                if (priority == wanted && prototypeManager.TryIndex(jobId, out JobPrototype? job))
                    return new LobbyHighJobPreviewEntry(job, Array.Empty<string>());
            }
        }

        return null;
    }

    public static string GetSignature(IReadOnlyList<LobbyHighJobPreviewEntry> entries)
    {
        if (entries.Count == 0)
            return string.Empty;

        return string.Join("|", entries.Select(entry => entry.Signature));
    }

    private static string TrimHiddenFactionSuffix(string name)
    {
        var trimmed = name.TrimEnd();
        foreach (var suffix in HiddenFactionSuffixes)
        {
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring(0, trimmed.Length - suffix.Length).TrimEnd();
        }

        return name;
    }
}
