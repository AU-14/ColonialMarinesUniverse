using Content.Shared.Forensics.Components;
using Content.Shared.Tools.Components;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Forensics.Toolmarks;

/// <summary>
///     Gives every tool a signature, and has prying or welding something leave marks matching it. A forensic scanner
///     finds the signature on the tool and the marks on the door or locker it was used on.
/// </summary>
public sealed partial class CMUToolmarkSystem : EntitySystem
{
    private const string Hex = "0123456789ABCDEF";

    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ToolComponent, ComponentStartup>(OnToolStartup);
        SubscribeLocalEvent<CMUToolmarkEvent>(OnToolmark);
    }

    private void OnToolStartup(Entity<ToolComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient || HasComp<CMUToolSignatureComponent>(ent))
            return;

        var signature = EnsureComp<CMUToolSignatureComponent>(ent);
        signature.Signature = NewSignature();
        AddResidue(ent, Loc.GetString("cmu-forensic-tool-signature", ("signature", signature.Signature)));
    }

    private void OnToolmark(ref CMUToolmarkEvent args)
    {
        if (_net.IsClient || !TryComp(args.Tool, out CMUToolSignatureComponent? signature) || signature.Signature.Length == 0)
            return;

        var marks = args.Kind == CMUToolmarkKind.Weld ? "cmu-forensic-weld-marks" : "cmu-forensic-pry-marks";
        AddResidue(args.Target, Loc.GetString(marks, ("signature", signature.Signature)));
    }

    private string NewSignature()
    {
        var chars = new char[9];
        chars[0] = 'T';
        for (var i = 1; i < chars.Length; i++)
            chars[i] = i == 4 ? '-' : Hex[_random.Next(Hex.Length)];
        return new string(chars);
    }

    private void AddResidue(EntityUid uid, string residue)
    {
        var forensics = EnsureComp<ForensicsComponent>(uid);
        if (forensics.Residues.Add(residue))
            Dirty(uid, forensics);
    }
}
