using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// Gives every Bad Blood a clan code (BB-10 to BB-99, unique for the round) and builds the name the clan knows them
/// by: their role and their code, "Leader (BB-12)". Bad Bloods see each other by it (Identity.cs) and the radio
/// speaks it (RadioSystem). Marines in local chat still see the masked identity.
/// </summary>
public sealed class BadBloodCodeSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly HashSet<int> _taken = new();

    public const int First = 10;
    public const int Last = 99;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBadBloodComponent, MapInitEvent>(OnBadBloodMapInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _taken.Clear());
    }

    private void OnBadBloodMapInit(Entity<YautjaBadBloodComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        EnsureCode(ent.Owner);
    }

    /// <summary>
    /// Gives <paramref name="uid"/> a clan code if it has none, and returns it.
    /// </summary>
    public int EnsureCode(EntityUid uid)
    {
        if (TryComp(uid, out BadBloodCodeComponent? existing) && existing.Number != 0)
            return existing.Number;

        var number = 0;
        for (var tries = 0; tries < 50; tries++)
        {
            var roll = _random.Next(First, Last + 1);
            if (_taken.Add(roll))
            {
                number = roll;
                break;
            }
        }

        // more Bad Bloods than codes: reuse one rather than leave a body nameless
        if (number == 0)
            number = _random.Next(First, Last + 1);

        var code = EnsureComp<BadBloodCodeComponent>(uid);
        code.Number = number;
        Dirty(uid, code);
        return number;
    }

    /// <summary>
    /// The name the clan knows <paramref name="uid"/> by, e.g. "Leader (BB-12)". Null without a code.
    /// </summary>
    public string? GetClanName(EntityUid uid)
    {
        if (!TryComp(uid, out BadBloodCodeComponent? code) || code.Number == 0)
            return null;

        string role;
        if (HasComp<BadBloodLeaderComponent>(uid))
            role = "cmu-bad-blood-code-role-leader";
        else if (TryComp(uid, out BadBloodCasteComponent? caste))
            role = caste.Caste == BadBloodCaste.Engineer ? "cmu-bad-blood-code-role-engineer" : "cmu-bad-blood-code-role-medic";
        else
            role = "cmu-bad-blood-code-role-grunt";

        return Loc.GetString("cmu-bad-blood-code-name", ("role", Loc.GetString(role)), ("number", code.Number));
    }
}
