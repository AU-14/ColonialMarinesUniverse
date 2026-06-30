using System.Text.RegularExpressions;
using Content.Server._RMC14.Speech.Components;
using Robust.Shared.Random;
using Content.Server.Speech;

namespace Content.Server._RMC14.Speech.EntitySystems;

public sealed partial class VulpkaninAccentSystem : EntitySystem
{
    private static readonly string[] LowerRReplacements = ["rr", "rrr"];
    private static readonly string[] UpperRReplacements = ["RR", "RRR"];

    [Dependency] private IRobustRandom _random = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VulpkaninAccentComponent, AccentGetEvent>(OnAccent);
    }
    
    private void OnAccent(EntityUid uid, VulpkaninAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;
        
        message = LowerRRegex.Replace(message, _random.Pick(LowerRReplacements));
        message = UpperRRegex.Replace(message, _random.Pick(UpperRReplacements));
        
        args.Message = message;
    }

    private static readonly Regex LowerRRegex = new("r+");

    private static readonly Regex UpperRRegex = new("R+");
}
