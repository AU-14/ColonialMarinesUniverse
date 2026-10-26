using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.Religion;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Roles;

/// <summary>
///     The kind of auxiliary staff a character works as, picked in their loadout: it retitles their ID card, changes
///     their radio prefix and adds any access the work needs.
/// </summary>
public sealed partial class CMUAuxiliaryStaffLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public LocId Title;

    [DataField]
    public LocId? Prefix;

    [DataField]
    public List<ProtoId<AccessLevelPrototype>> Access = new();

    /// <summary>Whether the character needs a religion other than atheism, like a chaplain.</summary>
    [DataField]
    public bool RequiresReligion;

    /// <summary>Whether they get their religion's holy book once they spawn, like a chaplain.</summary>
    [DataField]
    public bool HolyBook;

    public override bool Validate(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession? session, IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        if (RequiresReligion && profile.Religion == CMUReligionPrototype.Atheist)
        {
            reason = FormattedMessage.FromUnformatted(Loc.GetString("cmu-auxiliary-staff-needs-religion"));
            return false;
        }

        reason = null;
        return true;
    }

    public override void ApplyToEntity(EntityUid entity, IEntityManager entityManager, IPrototypeManager prototypeManager)
    {
        if (Prefix is { } prefix && entityManager.TryGetComponent(entity, out JobPrefixComponent? jobPrefix))
        {
            jobPrefix.Prefix = prefix;
            entityManager.Dirty(entity, jobPrefix);
        }

        if (HolyBook)
            entityManager.EnsureComponent<CMUChaplainComponent>(entity);

        // Squads retitle their members' IDs as "<squad> <role>", so give the squad the role's name to use as well.
        var roleName = entityManager.EnsureComponent<RMCVendorRoleOverrideComponent>(entity);
        roleName.GiveSquadRoleName = Title;
        roleName.IsAppendSquadRoleName = false;
        entityManager.Dirty(entity, roleName);

        var ids = entityManager.System<SharedIdCardSystem>();
        if (!ids.TryFindIdCard(entity, out var card))
            return;

        ids.TryChangeJobTitle(card, Loc.GetString(Title), card.Comp);
        if (entityManager.HasComponent<SquadMemberComponent>(entity))
            entityManager.System<SquadSystem>().UpdateSquadTitle(entity);

        if (Access.Count == 0)
            return;

        var access = entityManager.System<SharedAccessSystem>();
        var tags = access.TryGetTags(card)?.ToHashSet() ?? new HashSet<ProtoId<AccessLevelPrototype>>();
        tags.UnionWith(Access);
        access.TrySetTags(card, tags);
    }
}
