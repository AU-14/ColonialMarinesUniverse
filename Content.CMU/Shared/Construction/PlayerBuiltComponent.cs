// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.Construction;

/// <summary>Records the builder for examine and administration. Also marks player-built z-level supports.</summary>
[RegisterComponent]
public sealed partial class PlayerBuiltComponent : Component
{
    /// <summary>Character (mob entity) name of whoever built this, for examine.</summary>
    [ViewVariables]
    public string BuilderName = string.Empty;

    /// <summary>Hidden account id of the builder. Available to administrators.</summary>
    [ViewVariables]
    public Guid BuilderUserId = Guid.Empty;

    /// <summary>Round time at which this entity was last (re)built by a player.</summary>
    [ViewVariables]
    public TimeSpan BuiltAt;
}
