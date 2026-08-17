using Content.Server.Administration;
using Content.Shared._CMU14.Ops.Sfx;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Ops.Sfx.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class ScriptedSoundCommand : LocalizedCommands
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IEntitySystemManager _sysMan = default!;

    public override string Command => "scriptedsound";
    public override string Help => "scriptedsound start <sequenceId> [anchorUid] | scriptedsound stop <handle> | scriptedsound list";
    public override string Description => "Start/stop/list scripted sound sequence.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var scriptedSound = _sysMan.GetEntitySystem<ScriptedSoundSystem>();
        if (args.Length == 0 || (args[0] != "list" && args.Length is not (2 or 3)))
        {
            shell.WriteLine($"Usage: {Help}");
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "list":
                {
                    foreach (var (uid, comp) in scriptedSound.GetActiveSequences())
                        shell.WriteLine($"{uid}: {comp.SequenceId} (anchor {comp.AnchorEntity}, next {comp.NextEntryIndex})");
                    break;
                }
            case "start":
                {
                    if (!_protoMan.HasIndex<ScriptedSoundSequencePrototype>(args[1]))
                    {
                        shell.WriteLine($"Unknown sequence prototype '{args[1]}'.");
                        return;
                    }
                    var anchor = shell.Player?.AttachedEntity;
                    if (args.Length == 3)
                    {
                        if (!EntityUid.TryParse(args[2], out var entity) || !_entMan.EntityExists(entity))
                        {
                            shell.WriteLine($"Invalid entity UID '{args[2]}'.");
                            return;
                        }
                        anchor = entity;
                    }
                    var result = scriptedSound.StartSequence(args[1], anchor);
                    shell.WriteLine(result != null
                        ? $"Started sequence '{args[1]}' as handle {result.Value}"
                        : $"Failed to start sequence '{args[1]}'.");
                    break;
                }
            case "stop":
                {
                    if (!int.TryParse(args[1], out var handle))
                    {
                        shell.WriteLine("Invalid sequence handle.");
                        return;
                    }
                    scriptedSound.StopSequence(handle);
                    shell.WriteLine($"Stopped sequence {handle}.");
                    break;
                }
            default:
                shell.WriteLine($"Usage: {Help}");
                break;
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(new[] { "start", "stop", "list" }, "Subcommand");

        if (args.Length == 2)
        {
            switch (args[0])
            {
                case "start":
                    return CompletionResult.FromHintOptions(
                        CompletionHelper.PrototypeIDs<ScriptedSoundSequencePrototype>(true, _protoMan),
                        "Sequence prototype ID");
                case "stop":
                    var scriptedSound = _sysMan.GetEntitySystem<ScriptedSoundSystem>();
                    var active = scriptedSound.GetActiveSequences();
                    if (active.Count == 0)
                        return CompletionResult.FromHint("No active sequences.");

                    var options = new List<CompletionOption>();
                    foreach (var (h, comp) in active)
                        options.Add(new CompletionOption(h.ToString(), $"{comp.SequenceId} ({h})"));

                    return CompletionResult.FromHintOptions(options, "Active sequence handle");
            }
        }

        if (args.Length == 3 && args[0] == "start")
            return CompletionResult.FromHint("Anchor entity UID (optional, defaults to your character)");

        return CompletionResult.Empty;
    }
}
