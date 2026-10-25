// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.WorldMarket;

// Marks a console of the colony administration as one with the window of the world market. The window has its own component because a component has one listener of an event per system, and
// the system of the console already listens for the opening of the window and for the taxes on the component that it has.
[RegisterComponent]
public sealed partial class ColonyConsoleComponent : Component;
