namespace Content.Shared.Administration
{
    /// <summary>
    ///     Permissions that admins can have.
    /// </summary>
    [Flags]
    public enum AdminFlags : ulong
    {
        None = 0,

        /// <summary>
        ///     Basic admin verbs.
        /// </summary>
        Admin = 1 << 0,

        /// <summary>
        ///     Ability to ban people.
        /// </summary>
        Ban = 1 << 1,

        /// <summary>
        ///     Debug commands for coders.
        /// </summary>
        Debug = 1 << 2,

        /// <summary>
        ///     !!FUN!!
        ///     This is stuff that trial administrators shouldn't quite have access to yet, e.g. for running events.
        /// </summary>
        Fun = 1 << 3,

        /// <summary>
        ///     Ability to edit permissions for other administrators.
        /// </summary>
        Permissions = 1 << 4,

        /// <summary>
        ///     Ability to control the server like restart it or change the round type.
        /// </summary>
        Server = 1 << 5,

        /// <summary>
        ///     Ability to spawn stuff in.
        /// </summary>
        Spawn = 1 << 6,

        /// <summary>
        ///     Ability to use VV.
        /// </summary>
        VarEdit = 1 << 7,

        /// <summary>
        ///     Large mapping operations.
        /// </summary>
        Mapping = 1 << 8,

        /// <summary>
        ///     Makes you british.
        /// </summary>
        //Piss = 1 << 9,

        /// <summary>
        ///     Lets you view admin logs.
        /// </summary>
        Logs = 1 << 9,

        /// <summary>
        ///     Lets you modify the round (forcemap, loadgamemap, etc)
        /// </summary>
        Round = 1 << 10,

        /// <summary>
        ///     Lets you use BQL queries.
        /// </summary>
        Query = 1 << 11,

        /// <summary>
        ///     Lets you use the admin help system.
        /// </summary>
        Adminhelp = 1 << 12,

        /// <summary>
        ///     Lets you view admin notes.
        /// </summary>
        ViewNotes = 1 << 13,

        /// <summary>
        ///     Lets you create, edit and delete admin notes.
        /// </summary>
        EditNotes = 1 << 14,

        /// <summary>
        ///     Lets you Massban, on SS14.Admin
        /// </summary>
        MassBan = 1 << 15,

        /// <summary>
        /// Allows you to remain hidden from adminwho except to other admins with this flag.
        /// </summary>
        Stealth = 1 << 16,

        ///<summary>
		/// Allows you to use Admin chat
		///</summary>
		Adminchat = 1 << 17,

        ///<summary>
        /// Permits the visibility of Pii in game and on SS14 Admin
        ///</summary>
        Pii = 1 << 18,

        /// <summary>
        ///     Lets you take moderator actions on the game server.
        /// </summary>
        Moderator = 1 << 19,

        /// <summary>
        ///     Lets you check currently online admins.
        /// </summary>
        AdminWho = 1 << 20,

        /// <summary>
        ///     Lets you set the color of your OOC name.
        /// </summary>
        NameColor = 1 << 21,

        /// <summary>
        ///     Lets you to administer commendations.
        /// </summary>
        Commendations = 1 << 29,

        /// <summary>
        ///     Lets you use the mentor help system.
        /// </summary>
        MentorHelp = 1 << 30,

        /// <summary>
        ///     Dangerous host permissions like scsi.
        /// </summary>
        Host = 1u << 31,

        // RMC Permissions

        /// <summary>
        ///     Test Commands
        /// </summary>
        RMCMaintainer = 1ul << 32,

        /// <summary>
        ///     Admin Ghost
        /// </summary>
        AdminGhost = 1ul << 33,

        /// <summary>
        ///     Yautja clan administration.
        /// </summary>
        Clans = 1ul << 34,

        // cmu edit start: things being on the admin list used to grant by itself, split out so each rank only gets
        // what it's given. Holding a rank with none of these (a mentor, say) gives nothing beyond its other flags.

        /// <summary>
        ///     Receive admin alerts, admin announcements and admin notification sounds.
        /// </summary>
        AdminAlerts = 1ul << 40,

        /// <summary>
        ///     Receive the admin player list, which shows antagonists.
        /// </summary>
        PlayerList = 1ul << 41,

        /// <summary>
        ///     As a ghost: hear everything regardless of range, see through Yautja masks, and use dead chat even while
        ///     it's disabled.
        /// </summary>
        GhostSenses = 1ul << 42,

        /// <summary>
        ///     Move and zoom the camera freely while not a ghost.
        /// </summary>
        FreeZoom = 1ul << 43,

        /// <summary>
        ///     On-duty staff: listed in adminwho and showadmins, counts as an admin online for votes, and exempt from the
        ///     player cap, AFK kicks, votekicks and multi-server kicks. Tracks admin playtime.
        /// </summary>
        Staff = 1ul << 44,
        // cmu edit end
    }
}
