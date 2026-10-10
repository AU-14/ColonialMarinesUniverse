using System;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Shared.CMU14.Expeditions;

/// <summary>Bounded patrol coverage and retry policy; the server supplies physically valid destinations.</summary>
public static class CMUPatrolPolicy
{
    public const int SectorCount = 24;

    public static Vector2 Offset(int sector, int squad)
    {
        var angle = (sector % 8 + squad % 8) * MathF.Tau / 8;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ((sector / 8 + 1) * 4);
    }

    public static int Select(IReadOnlyList<CMUPatrolOption> options, double now)
    {
        var best = -1;
        var score = double.NegativeInfinity;
        foreach (var option in options)
        {
            if (!option.Available || option.Distance is < 3 or > 16 || option.Memory.RetryAt > now)
                continue;
            var coverage = option.Memory.Visits == 0 ? 100 : Math.Min(75, (now - option.Memory.VisitedAt) / 2);
            var candidate = coverage - option.Distance * .7 - option.Memory.Failures * 3;
            if (candidate <= score)
                continue;
            score = candidate;
            best = option.Sector;
        }
        return best;
    }

    public static CMUPatrolMemory Visited(CMUPatrolMemory memory, double now) =>
        new(memory.Visits + 1, now, now, now + 12, 0);

    public static CMUPatrolMemory Failed(CMUPatrolMemory memory, double now)
    {
        var failures = now - memory.AttemptedAt >= 120 ? 1 : Math.Min(3, memory.Failures + 1);
        return memory with { AttemptedAt = now, RetryAt = now + failures * 10, Failures = failures };
    }
}

public readonly record struct CMUPatrolMemory(int Visits, double VisitedAt, double AttemptedAt, double RetryAt, int Failures);
public readonly record struct CMUPatrolOption(int Sector, float Distance, bool Available, CMUPatrolMemory Memory);
