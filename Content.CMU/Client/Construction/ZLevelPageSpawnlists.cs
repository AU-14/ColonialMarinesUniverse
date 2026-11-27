// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;

namespace Content.Client.CMU14.Construction;

/// <summary>Experimental spawnlists excluded from the standard construction catalog.</summary>
public static class ZLevelPageSpawnlists
{
    public static readonly string[] Names = { "Tiles", "ZLevel" };

    public static bool Contains(string spawnlist) => Array.IndexOf(Names, spawnlist) >= 0;
}
