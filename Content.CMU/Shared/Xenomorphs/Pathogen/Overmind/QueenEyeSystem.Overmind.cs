using Content.Shared.CMU14.Xenomorphs.Pathogen.Overmind;

// ReSharper disable once CheckNamespace
namespace Content.Shared._RMC14.Xenonids.Eye;

public sealed partial class QueenEyeSystem
{
    // the overmind's eye form relays movement to its own eye entity, not a queen eye action,
    // so do-afters that only checked IsInQueenEye broke whenever the overmind moved its eye
    public bool IsMovingRemoteEye(EntityUid xeno)
    {
        return IsInQueenEye(xeno) ||
               TryComp(xeno, out CMUXenoOvermindComponent? overmind) && overmind.Eye != null;
    }
}
