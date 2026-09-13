using System;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     Runs one or more console commands once, shortly after the client has connected, from
///     <see cref="CCVars.CMUStartupCommand"/>.
/// </summary>
/// <remarks>
///     <para>
///     A development aid. <see cref="CmuPanelPreviewSystem"/> opens a window without anyone
///     clicking; this covers anything that needs a console command instead, since the client's
///     console is drawn in-game and cannot be written to from outside the process.
///     </para>
///     <para>
///     The case that prompted it: the lobby only exists before a round starts, and the dev config
///     boots straight into a running round, so the lobby UI could not be looked at at all. The
///     server has had a <c>golobby</c> command the whole time; there was simply no way to reach it.
///     <c>--cvar cmu.startup_command=golobby</c> closes that.
///     </para>
///     <para>
///     Deliberately not ARCHIVE. An archived value would sit in a player's config running a command
///     at every launch, which is a strange thing to have happen to you and a hard one to work out.
///     </para>
/// </remarks>
public sealed partial class CmuStartupCommandSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    ///     Long enough for the connection and the admin handshake to finish. A server command sent
    ///     before the client is known to be an admin is refused, and this only fires once.
    /// </summary>
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(6);

    private bool _done;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_done)
            return;

        var spec = _cfg.GetCVar(CCVars.CMUStartupCommand);
        if (string.IsNullOrWhiteSpace(spec))
            return;

        if (_timing.RealTime < Delay)
            return;

        _done = true;

        // Semicolon-separated, so a setup that takes two commands does not need two cvars. The
        // client console forwards anything it does not recognise to the server, which is how a
        // server-side admin command like golobby is reachable from here at all.
        foreach (var command in spec.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TrySetLocalCVar(command))
                continue;

            _console.ExecuteCommand(command);
        }
    }

    /// <summary>
    ///     Handle <c>cvar name value</c> here rather than letting the console have it.
    /// </summary>
    /// <remarks>
    ///     The console's own <c>cvar</c> command ends up setting the value on the server, and a
    ///     CLIENTONLY cvar set there does nothing whatsoever - no error, no effect. That is a
    ///     miserable thing to debug, and it is the case that matters most here: the options a
    ///     player toggles are almost all client-side, and driving one of those is the main reason
    ///     to want a startup command at all.
    /// </remarks>
    private bool TrySetLocalCVar(string command)
    {
        var parts = command.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !parts[0].Equals("cvar", StringComparison.OrdinalIgnoreCase))
            return false;

        var name = parts[1];
        if (!_cfg.IsCVarRegistered(name))
            return false;

        var type = _cfg.GetCVarType(name);
        object value;

        if (type == typeof(bool) && bool.TryParse(parts[2], out var b))
            value = b;
        else if (type == typeof(int) && int.TryParse(parts[2], out var i))
            value = i;
        else if (type == typeof(float) && float.TryParse(parts[2], out var f))
            value = f;
        else if (type == typeof(string))
            value = parts[2];
        else
            return false;

        _cfg.SetCVar(name, value);
        return true;
    }
}
