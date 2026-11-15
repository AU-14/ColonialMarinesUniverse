using System.Numerics;
using System.Threading;
using Content.Client.CombatMode;
using Content.Client.Gameplay;
using Content.Client.Mapping;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client.ContextMenu.UI
{
    /// <summary>
    ///     This class handles all the logic associated with showing a context menu, as well as all the state for the
    ///     entire context menu stack, including verb and entity menus. It does not currently support multiple
    ///     open context menus.
    /// </summary>
    /// <remarks>
    ///     This largely involves setting up timers to open and close sub-menus when hovering over other menu elements.
    /// </remarks>
    public sealed partial class ContextMenuUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>, IOnSystemChanged<CombatModeSystem>, IOnStateEntered<MappingState>, IOnStateExited<MappingState>
    {
        public static readonly TimeSpan HoverDelay = TimeSpan.FromSeconds(0.2);

        /// <summary>
        ///     Root menu of the entire context menu.
        /// </summary>
        public ContextMenuPopup RootMenu = default!;
        public Stack<ContextMenuPopup> Menus { get; } = new();

        /// <summary>
        ///     Used to cancel the timer that opens menus.
        /// </summary>
        public CancellationTokenSource? CancelOpen;

        /// <summary>
        ///     Used to cancel the timer that closes menus.
        /// </summary>
        public CancellationTokenSource? CancelClose;

        public Action? OnContextClosed;
        public Action<ContextMenuElement>? OnContextMouseEntered;
        public Action<ContextMenuElement>? OnContextMouseExited;
        public Action<ContextMenuElement>? OnSubMenuOpened;
        public Action<ContextMenuElement, GUIBoundKeyEventArgs>? OnContextKeyEvent;

        private bool _setup;

        public void OnStateEntered(GameplayState state)
        {
            Setup();
        }

        public void OnStateExited(GameplayState state)
        {
            Shutdown();
        }

        public void OnStateEntered(MappingState state)
        {
            Setup();
        }

        public void OnStateExited(MappingState state)
        {
            Shutdown();
        }

        public void Setup()
        {
            if (_setup)
                return;

            _setup = true;

            RootMenu = new(this, null);
            RootMenu.OnPopupHide += Close;
            Menus.Push(RootMenu);
        }

        public void Shutdown()
        {
            if (!_setup)
                return;

            _setup = false;

            Close();
            RootMenu.OnPopupHide -= Close;
            // CMU14: release the root as well as every submenu across state transitions.
            // RootMenu.Orphan();
            RootMenu.Dispose();
            Menus.Clear();
            RootMenu = default!;
        }

        /// <summary>
        ///     Close and clear the root menu. This will also dispose any sub-menus.
        /// </summary>
        // CMU14: unwind popups before removing their parent rows; hide callbacks can reenter Close.
        public void Close()
        {
            if (RootMenu == null || _cmuClosing)
                return;

            _cmuClosing = true;
            try
            {
                CMUCancelTimer(ref CancelOpen);
                CMUCancelTimer(ref CancelClose);
                CloseSubMenus(RootMenu);
                RootMenu.MenuBody.RemoveAllChildren();
                RootMenu.Close();
                OnContextClosed?.Invoke();
            }
            finally
            {
                _cmuClosing = false;
            }
        }

        /// <summary>
        ///     Starts closing menus until the top-most menu is the given one.
        /// </summary>
        /// <remarks>
        ///     CMU14: ignore popups that no longer belong to this stack, including delayed callbacks after teardown.
        /// </remarks>
        public void CloseSubMenus(ContextMenuPopup? menu)
        {
            // CMU14: hidden roots still own open submenu entries during teardown.
            // if (menu == null || !menu.Visible)
            if (menu == null || !Menus.Contains(menu))
                return;

            while (Menus.TryPeek(out var subMenu) && subMenu != menu)
            {
                Menus.Pop().Close();
            }

            // ensure no accidental double-closing happens.
            // CMU14: release cancelled timer sources too.
            CMUCancelTimer(ref CancelClose);
        }

        /// <summary>
        ///     Start a timer to open this element's sub-menu.
        /// </summary>
        private void OnMouseEntered(ContextMenuElement element)
        {
            if (!Menus.TryPeek(out var topMenu))
            {
                Log.Error("Context Menu: Mouse entered menu without any open menus?");
                return;
            }

            if (element.ParentMenu == topMenu || element.SubMenu == topMenu)
                CMUCancelTimer(ref CancelClose); // CMU14

            if (element.SubMenu == topMenu)
                return;

            // open the sub-menu after a short delay.
            CMUCancelTimer(ref CancelOpen); // CMU14
            CancelOpen = new();
            Timer.Spawn(HoverDelay, () => OpenSubMenu(element), CancelOpen.Token);
        }

        /// <summary>
        ///     Start a timer to close this element's sub-menu.
        /// </summary>
        /// <remarks>
        ///     Note that this timer will be aborted when entering the actual sub-menu itself.
        /// </remarks>
        private void OnMouseExited(ContextMenuElement element)
        {
            CMUCancelTimer(ref CancelOpen); // CMU14

            if (element.SubMenu == null)
                return;

            CMUCancelTimer(ref CancelClose); // CMU14
            CancelClose = new();
            Timer.Spawn(HoverDelay, () => CloseSubMenus(element.ParentMenu), CancelClose.Token);
            OnContextMouseExited?.Invoke(element);
        }

        private void OnKeyBindDown(ContextMenuElement element, GUIBoundKeyEventArgs args)
        {
            OnContextKeyEvent?.Invoke(element, args);
        }

        /// <summary>
        ///     Opens a new sub menu, and close the old one.
        /// </summary>
        /// <remarks>
        ///     If the given element has no sub-menu, just close the current one.
        /// </remarks>
        public void OpenSubMenu(ContextMenuElement element)
        {
            if (!Menus.TryPeek(out var topMenu))
            {
                Log.Error("Context Menu: Attempting to open sub menu without any open menus?");
                return;
            }

            // If This is already the top most menu, do nothing.
            if (element.SubMenu == topMenu)
                return;

            // Was the parent menu closed or disposed before an open timer completed?
            if (element.Disposed || element.ParentMenu == null || !element.ParentMenu.Visible)
                return;

            // Close any currently open sub-menus up to this element's parent menu.
            CloseSubMenus(element.ParentMenu);

            // cancel any queued openings to prevent weird double-open scenarios.
            // CMU14: release the completed/cancelled timer's closure and populate lazy verb categories.
            CMUCancelTimer(ref CancelOpen);

            if (element.SubMenu == null)
                return;

            element.SubMenu.CMUEnsureContents(); // CMU14

            // open pop-up adjacent to the parent element. We want the sub-menu elements to align with this element
            // which depends on the panel container style margins.
            var altPos = element.GlobalPosition;
            var pos = altPos + new Vector2(element.Width + 2 * ContextMenuElement.ElementMargin, -2 * ContextMenuElement.ElementMargin);
            element.SubMenu.Open(UIBox2.FromDimensions(pos, new Vector2(1, 1)), altPos);

            // draw on top of other menus
            element.SubMenu.SetPositionLast();

            Menus.Push(element.SubMenu);
            OnSubMenuOpened?.Invoke(element);
        }

        /// <summary>
        ///     Add an element to a menu and subscribe to GUI events.
        /// </summary>
        // CMU14: use removable handlers, including when a row is reattached.
        public void AddElement(ContextMenuPopup menu, ContextMenuElement element)
        {
            CMUUnbindElement(element);
            element.OnMouseEntered += CMUOnMouseEntered;
            element.OnMouseExited += CMUOnMouseExited;
            element.CMUKeyHandler = args => OnKeyBindDown(element, args);
            element.OnKeyBindDown += element.CMUKeyHandler;
            element.ParentMenu = menu;
            menu.MenuBody.AddChild(element);
            menu.InvalidateMeasure();
        }

        /// <summary>
        ///     Removes event subscriptions when an element is removed from a menu,
        /// </summary>
        // CMU14: remove the same delegates that were registered by AddElement.
        public void OnRemoveElement(ContextMenuPopup menu, Control control)
        {
            if (control is not ContextMenuElement element)
                return;

            CMUUnbindElement(element);

            menu.InvalidateMeasure();
        }

        private void OnCombatModeUpdated(bool inCombatMode)
        {
            if (inCombatMode)
                Close();
        }

        public void OnSystemLoaded(CombatModeSystem system)
        {
            system.LocalPlayerCombatModeUpdated += OnCombatModeUpdated;
        }

        public void OnSystemUnloaded(CombatModeSystem system)
        {
            system.LocalPlayerCombatModeUpdated -= OnCombatModeUpdated;
        }
    }
}
