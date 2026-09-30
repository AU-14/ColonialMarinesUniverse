using System.Linq;
using System.Text;
using Content.Server.Popups;
using Content.Shared._RMC14.Intel;
using Content.Shared.CMU14.Intel;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Intel;

/// <summary>
/// Data disks are the intel computer's own job. Using one on an intel computer opens its decryption: crack the
/// code and the disk uploads for that computer's faction. There's no limit on attempts.
/// </summary>
public sealed class CMUIntelDataDiskSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private IntelSystem _intel = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<IntelConsoleComponent, InteractUsingEvent>(OnConsoleInteractUsing);
        SubscribeLocalEvent<CMUIntelDataDiskComponent, CMUIntelDecryptGuessMessage>(OnGuess);
        SubscribeLocalEvent<CMUIntelDataDiskComponent, ExaminedEvent>(OnDiskExamined);
    }

    private void OnConsoleInteractUsing(Entity<IntelConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp(args.Used, out CMUIntelDataDiskComponent? disk))
            return;

        args.Handled = true;
        if (disk.Uploaded)
        {
            _popup.PopupEntity($"The {Name(args.Used)} has already been uploaded and wiped.", ent, args.User);
            return;
        }

        if (string.IsNullOrEmpty(disk.Code))
            RotateCode(disk);

        disk.Console = ent;
        _ui.OpenUi(args.Used, CMUIntelDecryptUiKey.Key, args.User);
        UpdateUi((args.Used, disk), args.User, "Encrypted disk detected. Enter a decryption key.");
    }

    private void OnGuess(Entity<CMUIntelDataDiskComponent> ent, ref CMUIntelDecryptGuessMessage args)
    {
        var user = args.Actor;
        var disk = ent.Comp;
        if (disk.Uploaded)
            return;

        // The disk has to be in the user's hands, at the computer it was slotted into.
        if (disk.Console is not { } console || TerminatingOrDeleted(console) ||
            !TryComp(console, out IntelConsoleComponent? consoleComp) ||
            !_hands.IsHolding(user, ent.Owner) ||
            !_interaction.InRangeUnobstructed(user, console))
        {
            _ui.CloseUi(ent.Owner, CMUIntelDecryptUiKey.Key, user);
            _popup.PopupEntity("You need to be at the intel computer, holding the disk.", user, user);
            return;
        }

        var guess = args.Guess;
        if (guess.Length != disk.CodeLength || guess.Any(c => c < '0' || c >= '0' + disk.DigitRange))
            return;

        var (exact, misplaced) = Score(disk.Code, guess);
        disk.History.Add(new CMUIntelDecryptGuess(guess, exact, misplaced));

        if (exact == disk.CodeLength)
        {
            disk.Uploaded = true;
            Dirty(ent);
            _intel.CreditDataDisk(consoleComp.Team, disk.Value);
            UpdateUi(ent, user, $"Decryption successful. Uploaded for {disk.Value.Double():0.##} intel point(s). Disk wiped.");
            _popup.PopupEntity("Decryption successful. The disk uploads and wipes itself.", console, user);
            return;
        }

        UpdateUi(ent, user, $"{guess}: {exact} right and in place, {misplaced} right but misplaced.");
    }

    private void RotateCode(CMUIntelDataDiskComponent disk)
    {
        var code = new StringBuilder(disk.CodeLength);
        for (var i = 0; i < disk.CodeLength; i++)
            code.Append((char) ('0' + _random.Next(disk.DigitRange)));

        disk.Code = code.ToString();
        disk.History.Clear();
    }

    private static (int Exact, int Misplaced) Score(string code, string guess)
    {
        var exact = 0;
        var codeCounts = new Dictionary<char, int>();
        var guessCounts = new Dictionary<char, int>();
        for (var i = 0; i < code.Length; i++)
        {
            if (code[i] == guess[i])
            {
                exact++;
                continue;
            }

            codeCounts[code[i]] = codeCounts.GetValueOrDefault(code[i]) + 1;
            guessCounts[guess[i]] = guessCounts.GetValueOrDefault(guess[i]) + 1;
        }

        var misplaced = guessCounts.Sum(kv => Math.Min(kv.Value, codeCounts.GetValueOrDefault(kv.Key)));
        return (exact, misplaced);
    }

    private void UpdateUi(Entity<CMUIntelDataDiskComponent> ent, EntityUid user, string status)
    {
        var state = new CMUIntelDecryptBuiState(
            ent.Comp.History.ToList(),
            ent.Comp.CodeLength,
            ent.Comp.DigitRange,
            status,
            ent.Comp.Uploaded);
        _ui.SetUiState(ent.Owner, CMUIntelDecryptUiKey.Key, state);
    }

    private void OnDiskExamined(Entity<CMUIntelDataDiskComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Uploaded
            ? "[color=gray]It's been wiped.[/color]"
            : "[color=cyan]It's encrypted. Use it on an intel computer to crack it and upload the data.[/color]");
    }
}
