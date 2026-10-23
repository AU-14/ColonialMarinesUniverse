using Content.Shared.Eui;
using Content.Shared.CMU14.Threats;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Ghost.Roles
{
    [NetSerializable, Serializable]
    public struct GhostRoleInfo
    {
        public uint Identifier { get; set; }
        public NetEntity Entity { get; set; }
        public string? EntityPrototype { get; set; }
        public string? JobPrototype { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Rules { get; set; }

        /// <summary>
        /// A list of all antag and job prototype IDs of the ghost role and its mind role(s).
        /// </summary>
        public (List<ProtoId<JobPrototype>>?,List<ProtoId<AntagPrototype>>?)  RolePrototypes;

        /// <summary>
        /// Optional requirements that replace the linked role prototype requirements for this ghost role.
        /// </summary>
        public HashSet<JobRequirement>? Requirements { get; set; }

        /// <inheritdoc cref="GhostRoleKind"/>
        public GhostRoleKind Kind { get; set; }

        /// <summary>
        /// if <see cref="Kind"/> is <see cref="GhostRoleKind.RaffleInProgress"/>, specifies how many players are currently
        /// in the raffle for this role.
        /// </summary>
        public uint RafflePlayerCount { get; set; }

        /// <summary>
        /// if <see cref="Kind"/> is <see cref="GhostRoleKind.RaffleInProgress"/>, specifies when raffle finishes.
        /// </summary>
        public TimeSpan RaffleEndTime { get; set; }

        /// <summary>
        /// if <see cref="Kind"/> is <see cref="GhostRoleKind.RaffleInProgress"/>, how long the raffle runs in
        /// total - which is not fixed, since joining extends it. Without this the window can show how long is
        /// left but not how far along it is.
        /// </summary>
        public TimeSpan RaffleDuration { get; set; }

        /// <summary>
        /// Id of the <see cref="Content.Shared.CMU14.Ghost.Roles.GhostRoleCategoryPrototype"/> this role is
        /// listed under. Set from the role's own component where it says, worked out from the entity where it
        /// does not - see CMUGhostRoleCategorySystem.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Where this particular entity is, for the window to print under its role's banner.
        /// </summary>
        public string? Location { get; set; }
    }

    [NetSerializable, Serializable]
    public sealed class GhostRolesEuiState : EuiStateBase
    {
        public GhostRoleInfo[] GhostRoles { get; }
        public ForceInterestInfo[] Forces { get; }

        /// <summary>If set, ghosts may call in a random minor third party from this menu.</summary>
        public GhostThirdPartyCallState? ThirdPartyCall { get; } // CMU14

        public GhostRolesEuiState(GhostRoleInfo[] ghostRoles, ForceInterestInfo[]? forces = null,
            GhostThirdPartyCallState? thirdPartyCall = null) // CMU14
        {
            GhostRoles = ghostRoles;
            Forces = forces ?? [];
            ThirdPartyCall = thirdPartyCall; // CMU14
        }
    }

    [NetSerializable, Serializable]
    public sealed class RequestGhostRoleMessage : EuiMessageBase
    {
        public uint Identifier { get; }

        public RequestGhostRoleMessage(uint identifier)
        {
            Identifier = identifier;
        }
    }

    [NetSerializable, Serializable]
    public sealed class FollowGhostRoleMessage : EuiMessageBase
    {
        public uint Identifier { get; }

        public FollowGhostRoleMessage(uint identifier)
        {
            Identifier = identifier;
        }
    }

    [NetSerializable, Serializable]
    public sealed class LeaveGhostRoleRaffleMessage : EuiMessageBase
    {
        public uint Identifier { get; }

        public LeaveGhostRoleRaffleMessage(uint identifier)
        {
            Identifier = identifier;
        }
    }

    /// <summary>
    /// Determines whether a ghost role is a raffle role, and if it is, whether it's running.
    /// </summary>
    [NetSerializable, Serializable]
    public enum GhostRoleKind
    {
        /// <summary>
        /// Role is not a raffle role and can be taken immediately.
        /// </summary>
        FirstComeFirstServe,

        /// <summary>
        /// Role is a raffle role, but raffle hasn't started yet.
        /// </summary>
        RaffleReady,

        /// <summary>
        ///  Role is raffle role and currently being raffled, but player hasn't joined raffle.
        /// </summary>
        RaffleInProgress,

        /// <summary>
        /// Role is raffle role and currently being raffled, and player joined raffle.
        /// </summary>
        RaffleJoined
    }
}
