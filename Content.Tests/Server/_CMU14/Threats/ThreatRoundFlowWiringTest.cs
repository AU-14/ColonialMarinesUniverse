using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Content.Server._CMU14.Threats;
using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.Threats;

[TestFixture]
public sealed class ThreatRoundFlowWiringTest
{
    private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

    static ThreatRoundFlowWiringTest()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
                continue;

            if (opCode.Size == 1)
                OneByteOpCodes[(byte) opCode.Value] = opCode;
            else
                TwoByteOpCodes[(byte) (opCode.Value & 0xff)] = opCode;
        }
    }

    [Test]
    public void DistressSignalAndColonyFallThreatFlowIsConnected()
    {
        var prepareVote = typeof(ThreatVoteSystem).GetMethod(nameof(ThreatVoteSystem.TryPrepareThreatVote))!;
        var startVote = typeof(ThreatVoteSystem).GetMethod(nameof(ThreatVoteSystem.StartPreparedThreatVote))!;
        var spawnThreat = typeof(ThreatSystem).GetMethod(nameof(ThreatSystem.SpawnThreatAtRoundStart))!;
        var spawnVotedThreat = typeof(ThreatSystem).GetMethod(nameof(ThreatSystem.SpawnThreatFromVote))!;
        var scheduleThreat = typeof(ThreatSystem)
            .GetMethod("SchedulePendingThreatSpawn", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var forcedAssignments = typeof(AuJobSelectionSystem)
            .GetProperty(nameof(AuJobSelectionSystem.ForcedJobAssignments))!
            .GetMethod!;

        Assert.Multiple(() =>
        {
            Assert.That(AuRoundSystem.IsPostRoundstartThreatVotePreset("DistressSignal"), Is.True);
            Assert.That(AuRoundSystem.IsPostRoundstartThreatVotePreset("ColonyFall"), Is.True);
            Assert.That(HasReachableCallFrom<GameTicker>("SpawnPlayers", prepareVote), Is.True,
                "GameTicker must prepare the post-roundstart threat vote.");
            Assert.That(HasReachableCallFrom<GameTicker>("SpawnPlayers", startVote), Is.True,
                "GameTicker must start the prepared threat vote, including single-option auto-selection.");
            Assert.That(HasReachableCallFrom<GameTicker>("SpawnPlayers", spawnThreat), Is.True,
                "GameTicker must retain the immediate threat-spawn path for preselected threats.");
            Assert.That(HasReachableCallFrom<ThreatVoteSystem>("StartPreparedThreatVote", spawnVotedThreat), Is.True,
                "Starting a prepared vote must reach voted threat spawning, including single-option auto-selection.");
            Assert.That(HasReachableCallFrom<ThreatSystem>("SpawnThreatFromVote", scheduleThreat), Is.True,
                "Voted Colony Fall threats must reach delayed threat scheduling.");
            Assert.That(HasReachableCallFrom<StationJobsSystem>("AssignJobs", forcedAssignments), Is.True,
                "Station job assignment must consume forced threat-vote jobs.");
        });
    }

    private static bool HasReachableCallFrom<TCaller>(string entryPointName, MethodInfo target)
    {
        var callerType = typeof(TCaller);
        var entryPoint = callerType.GetMethods(BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static)
            .Single(method => method.Name == entryPointName);
        var pending = new Queue<MethodInfo>();
        var visited = new HashSet<MethodInfo>();
        pending.Enqueue(entryPoint);

        while (pending.TryDequeue(out var method))
        {
            if (!visited.Add(method))
                continue;

            foreach (var called in GetCalledMethods(method))
            {
                if (called.Module == target.Module && called.MetadataToken == target.MetadataToken)
                    return true;

                if (called is MethodInfo calledMethod && called.DeclaringType == callerType)
                    pending.Enqueue(calledMethod);
            }
        }

        return false;
    }

    private static IEnumerable<MethodBase> GetCalledMethods(MethodInfo method)
    {
        var body = method.GetMethodBody();
        var il = body?.GetILAsByteArray();
        if (il == null)
            yield break;

        var position = 0;
        while (position < il.Length)
        {
            var opCode = ReadOpCode(il, ref position);
            if (opCode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, position);
                position += sizeof(int);

                MethodBase called = null;
                try
                {
                    called = method.Module.ResolveMethod(token,
                        method.DeclaringType?.GetGenericArguments(),
                        method.GetGenericArguments());
                }
                catch (ArgumentException)
                {
                    // Ignore unresolved generic method tokens from unrelated callers.
                }

                if (called != null)
                    yield return called;

                continue;
            }

            position += GetOperandSize(opCode.OperandType, il, position);
        }
    }

    private static OpCode ReadOpCode(byte[] il, ref int position)
    {
        var value = il[position++];
        if (value != 0xfe)
            return OneByteOpCodes[value];

        return TwoByteOpCodes[il[position++]];
    }

    private static int GetOperandSize(OperandType operandType, byte[] il, int position)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or
                OperandType.InlineField or
                OperandType.InlineI or
                OperandType.InlineSig or
                OperandType.InlineString or
                OperandType.InlineTok or
                OperandType.InlineType or
                OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => sizeof(int) + BitConverter.ToInt32(il, position) * sizeof(int),
            _ => throw new ArgumentOutOfRangeException(nameof(operandType), operandType, null),
        };
    }
}
