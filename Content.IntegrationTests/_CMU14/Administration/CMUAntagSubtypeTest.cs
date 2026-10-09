#nullable enable
using System.IO;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Administration;
using Content.Shared.Roles.Components;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests._CMU14.Administration;

/// <summary>
/// Admin tooling (overlay, Players tab, quick info) shows a mind role's subtype instead of its generic role type,
/// and <see cref="CMUAdminRoleIconComponent"/> next to it.
/// </summary>
[TestFixture]
public sealed class CMUAntagSubtypeTest : GameTest
{
    private static readonly string[] CMUAntagMindRoles =
    {
        "MindRoleCLFSubvertedSynth",
        "MindRoleCLFRecruit",
        "MindRoleCLFGuerrilla",
        "MindRoleCLFPhysician",
        "MindRoleCLFSurgeon",
        "MindRoleCLFLeader",
        "MindRoleCLFSaboteur",
        "MindRoleCLFSleeperAgent",
        "MindRoleCLFVeteran",
        "MindRoleArsonist",
        "MindRoleBountyHunter",
        "MindRoleCannibal",
        "MindRoleWeylandYutaniAgent",
        "MindRoleCorporateSpy",
        "MindRoleDrugDealer",
        "MindRoleFugitive",
        "MindRoleReplicant",
        "MindRoleRider",
        "MindRoleRunawaySynth",
        "MindRoleSerialKiller",
        "MindRoleStrikeOrganizer",
        "MindRoleVigilante",
        "MindRoleThreat",
        "MindRoleCultist",
        "MindRoleXenoHackedSynth",
    };

    private static readonly ResPath CMUPrototypeRoot = new("/Prototypes/CMU14");

    /// <summary>
    /// Every non-abstract antag mind role authored under the CMU prototype root must name a localized subtype,
    /// so a newly added CMU antag cannot silently fall back to the generic role type in admin tooling.
    /// </summary>
    [Test]
    public async Task CMUAntagMindRolesHaveLocalizedSubtypes()
    {
        var prototypes = Server.ResolveDependency<IPrototypeManager>();
        var factory = Server.ResolveDependency<IComponentFactory>();
        var loc = Server.ResolveDependency<ILocalizationManager>();
        var resources = Server.ResolveDependency<IResourceManager>();

        // Prototypes don't record their source file, so CMU ownership comes from the YAML under the CMU root.
        var cmuEntityIds = new HashSet<string>();
        foreach (var path in resources.ContentFindFiles(CMUPrototypeRoot))
        {
            if (path.Extension != "yml")
                continue;

            using var reader = new StreamReader(resources.ContentFileRead(path));
            var yaml = new YamlStream();
            yaml.Load(reader);
            foreach (var document in yaml.Documents)
            {
                if (document.RootNode is not YamlSequenceNode sequence)
                    continue;

                foreach (var node in sequence.OfType<YamlMappingNode>())
                {
                    if (node.Children.TryGetValue(new YamlScalarNode("type"), out var type) &&
                        type is YamlScalarNode { Value: "entity" } &&
                        node.Children.TryGetValue(new YamlScalarNode("id"), out var id) &&
                        id is YamlScalarNode { Value: { } idValue })
                    {
                        cmuEntityIds.Add(idValue);
                    }
                }
            }
        }

        await Server.WaitAssertion(() =>
        {
            var antagRoles = new Dictionary<string, MindRoleComponent>();
            foreach (var id in cmuEntityIds)
            {
                if (!prototypes.TryIndex<EntityPrototype>(id, out var proto) ||
                    proto.Abstract ||
                    !proto.TryComp<MindRoleComponent>(out var mindRole, factory) ||
                    !IsAntagRoleType(mindRole.RoleType))
                {
                    continue;
                }

                antagRoles[id] = mindRole;
            }

            Assert.Multiple(() =>
            {
                // Guards the discovery itself: if known roles go missing, the generic check proves nothing.
                Assert.That(antagRoles.Keys, Is.SupersetOf(CMUAntagMindRoles),
                    $"Known CMU antag mind roles were not discovered under {CMUPrototypeRoot}");

                foreach (var (id, mindRole) in antagRoles.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    Assert.That(mindRole.Subtype, Is.Not.Null, $"{id} has no subtype");
                    if (mindRole.Subtype is { } subtype)
                        Assert.That(loc.HasString(subtype), $"{id} subtype {subtype} is not localized");
                }
            });
        });
    }

    private static bool IsAntagRoleType(ProtoId<RoleTypePrototype>? roleType)
    {
        return roleType?.Id is "TeamAntagonist" or "SoloAntagonist";
    }

    [Test]
    public async Task CMUAdminRoleIconsResolve()
    {
        var client = Pair.Client;
        var prototypes = client.ResolveDependency<IPrototypeManager>();
        var factory = client.ResolveDependency<IComponentFactory>();
        var resources = client.ResolveDependency<IResourceCache>();

        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var proto in prototypes.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Abstract || !proto.TryComp<CMUAdminRoleIconComponent>(out var icon, factory))
                        continue;

                    if (icon.Icon is not SpriteSpecifier.Rsi rsi)
                        continue;

                    var path = SpriteSpecifierSerializer.TextureRoot / rsi.RsiPath;
                    Assert.That(resources.TryGetResource<RSIResource>(path, out var resource), $"{proto.ID} icon RSI {path} not found");
                    if (resource != null)
                        Assert.That(resource.RSI.TryGetState(rsi.RsiState, out _), $"{proto.ID} icon state {rsi.RsiState} missing in {path}");
                }
            });
        });
    }
}
