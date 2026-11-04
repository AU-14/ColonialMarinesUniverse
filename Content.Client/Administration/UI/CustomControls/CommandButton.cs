using System.Diagnostics.CodeAnalysis;
using Content.Client.Administration.Managers;
using Content.Client.Guidebook.Richtext;
using Content.Shared.Administration;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Administration.UI.CustomControls
{
    [Virtual]
    public class CommandButton : Button, IDocumentTag
    {
        public string? Command { get; set; }

        /// <summary>
        ///     An optional admin flag required to display this command button.
        /// </summary>
        public AdminFlags RequiredAdminFlag { get; set; }

        public CommandButton()
        {
            OnPressed += Execute;
        }

        protected virtual bool CanPress()
        {
            var adminFlags = IoCManager.Resolve<IClientAdminManager>().GetAdminData()?.Flags ?? AdminFlags.None;
            if (!HasRequiredAdminFlag(adminFlags, RequiredAdminFlag))
            {
                return false;
            }

            return string.IsNullOrEmpty(Command) ||
                   IoCManager.Resolve<IClientConGroupController>().CanCommand(Command.Split(' ')[0]);
        }

        public static bool HasRequiredAdminFlag(AdminFlags currentFlags, AdminFlags requiredAdminFlag)
        {
            return requiredAdminFlag == AdminFlags.None ||
                   (currentFlags & requiredAdminFlag) == requiredAdminFlag;
        }

        protected override void EnteredTree()
        {
            if (!CanPress())
            {
                Visible = false;
            }
        }

        protected virtual void Execute(ButtonEventArgs obj)
        {
            // Default is to execute command
            if (!string.IsNullOrEmpty(Command))
                IoCManager.Resolve<IClientConsoleHost>().ExecuteCommand(Command);
        }

        public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
        {
            if (args.Count != 2 || !args.TryGetValue("Text", out var text) || !args.TryGetValue("Command", out var command))
            {
                Logger.GetSawmill("content").Error($"Invalid arguments passed to {nameof(CommandButton)}");
                control = null;
                return false;
            }

            Command = command;
            Text = Loc.GetString(text);
            control = this;
            return true;
        }
    }
}
