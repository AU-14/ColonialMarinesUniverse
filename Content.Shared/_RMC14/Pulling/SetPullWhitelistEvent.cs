using Content.Shared.Whitelist;

namespace Content.Shared._RMC14.Pulling;

[ByRefEvent]
public record struct SetPullWhitelistEvent(EntityWhitelist Whitelist);