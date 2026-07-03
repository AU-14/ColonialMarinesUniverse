// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared._AU14.SavedBuilds;

/// <summary>
/// Stamped on any entity a player constructs. Doubles as:
///  * an accountability/logging mark (the builder's hidden user id),
///  * an examine line showing the builder's character name, and
///  * the "soft-whitelist" used by the saved-builds feature: only entities carrying this
///    component (built by the saver, or by someone who added the saver as a build partner)
///    may be captured into a saved build.
///
/// Server-authoritative: this component is never networked, so <see cref="BuilderUserId"/>
/// stays hidden from clients. The examine line is pushed by the server.
/// </summary>
/// <remarks>
/// Fields are intentionally runtime-only (no <c>[DataField]</c>): the mark is (re)applied at
/// construction time each round, and — critically — the hidden builder user id must never be written
/// into a saved-build YAML file (which players can share externally). Leaving these unsaved also keeps
/// <see cref="Guid"/> (which has no engine map-serializer) out of the entity serializer entirely.
/// </remarks>
[RegisterComponent]
public sealed partial class PlayerBuiltComponent : Component
{
    /// <summary>Character (mob entity) name of whoever built this, for examine.</summary>
    [ViewVariables]
    public string BuilderName = string.Empty;

    /// <summary>Hidden account id of the builder. Used for the save whitelist and admin logs only.</summary>
    [ViewVariables]
    public Guid BuilderUserId = Guid.Empty;

    /// <summary>Round time at which this entity was last (re)built by a player.</summary>
    [ViewVariables]
    public TimeSpan BuiltAt;
}
