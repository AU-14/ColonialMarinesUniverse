// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Trade;

// A crate that a character of the job spawns with: it is put down beside them when they come into the round.
[Prototype("jobStartingCrate")]
public sealed partial class JobStartingCratePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // The id of the job.
    [DataField(required: true)]
    public string Job = default!;

    [DataField(required: true)]
    public EntProtoId Crate;
}
