using System.Text.RegularExpressions;
using Content.Server._RMC14.Speech.Components;
using Robust.Shared.Random;
using Content.Server.Speech;

namespace Content.Server._RMC14.Speech.EntitySystems;

public sealed partial class VulpkaninAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VulpkaninAccentComponent, AccentGetEvent>(OnAccent);
    }
    
    private void OnAccent(EntityUid uid, VulpkaninAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;
        
        message = LowerRRegex().Replace(message, _random.Pick(new List<string> { "rr", "rrr" }));
        message = UpperRRegex().Replace(message, _random.Pick(new List<string> { "RR", "RRR" }));
        
        args.Message = message;
    }

    [GeneratedRegex("r+")]
    private static partial Regex LowerRRegex();

    [GeneratedRegex("R+")]
    private static partial Regex UpperRRegex();
}
