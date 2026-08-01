// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// Search/ancestry index over every selectable entity prototype, shared by the multi-select pickers
/// (Mass Entity Editor, Z-Sync Lists): display names, lowercase haystacks, and full ancestor chains
/// with ABSTRACT parents included (EnumerateAllParents; falls back to walking raw "parent:" YAML
/// mappings if the enumeration yields nothing client-side).
/// </summary>
public sealed class EntityParentIndex
{
    /// <summary>(id, display name, lowercased "name id" haystack), sorted by name.</summary>
    public readonly List<(string Id, string Name, string Haystack)> All = new();

    /// <summary>entity id -> every ancestor prototype id (abstract included).</summary>
    public readonly Dictionary<string, HashSet<string>> Parents = new();

    /// <summary>parent id -> how many selectable entities inherit from it.</summary>
    public readonly Dictionary<string, int> ParentCounts = new();

    public static EntityParentIndex Build(IPrototypeManager prototypes)
    {
        var index = new EntityParentIndex();

        foreach (var proto in prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || proto.HideSpawnMenu)
                continue;

            var name = string.IsNullOrEmpty(proto.Name) ? proto.ID : proto.Name;
            index.All.Add((proto.ID, name, $"{name} {proto.ID}".ToLowerInvariant()));
        }

        index.All.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.InvariantCultureIgnoreCase));

        foreach (var (id, _, _) in index.All)
        {
            var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var (parentId, _) in prototypes.EnumerateAllParents<EntityPrototype>(id, includeSelf: false))
                {
                    parents.Add(parentId);
                }
            }
            catch (Exception e)
            {
                Logger.GetSawmill("au14.massedit").Warning($"EnumerateAllParents failed for {id}: {e.Message}");
            }
            index.Parents[id] = parents;
        }

        var anyParents = false;
        foreach (var parents in index.Parents.Values)
        {
            if (parents.Count > 0)
            {
                anyParents = true;
                break;
            }
        }

        if (!anyParents)
            index.BuildFromMappings(prototypes);

        foreach (var parents in index.Parents.Values)
        {
            foreach (var parentId in parents)
            {
                index.ParentCounts[parentId] = index.ParentCounts.GetValueOrDefault(parentId) + 1;
            }
        }

        return index;
    }

    public bool HasAncestor(string id, string ancestorId) =>
        Parents.TryGetValue(id, out var parents) && parents.Contains(ancestorId);

    /// <summary>Fallback ancestry from raw "parent:" YAML nodes (works for abstract prototypes too).</summary>
    private void BuildFromMappings(IPrototypeManager prototypes)
    {
        var directCache = new Dictionary<string, List<string>>();

        List<string> GetDirect(string id)
        {
            if (directCache.TryGetValue(id, out var cached))
                return cached;

            var list = new List<string>();
            if (prototypes.TryGetMapping(typeof(EntityPrototype), id, out var mapping) &&
                mapping.TryGet("parent", out var parentNode))
            {
                switch (parentNode)
                {
                    case ValueDataNode value when !string.IsNullOrWhiteSpace(value.Value):
                        list.Add(value.Value);
                        break;
                    case SequenceDataNode sequence:
                        foreach (var child in sequence)
                        {
                            if (child is ValueDataNode v && !string.IsNullOrWhiteSpace(v.Value))
                                list.Add(v.Value);
                        }
                        break;
                }
            }

            directCache[id] = list;
            return list;
        }

        foreach (var (id, _, _) in All)
        {
            var parents = Parents[id];
            var queue = new Queue<string>(GetDirect(id));
            while (queue.TryDequeue(out var parentId))
            {
                if (!parents.Add(parentId))
                    continue;

                foreach (var grand in GetDirect(parentId))
                    queue.Enqueue(grand);
            }
        }
    }
}
