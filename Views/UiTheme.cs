// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System.Windows;
namespace BotPlusAutocadTool.Views
{
    internal static class UiTheme
    {
        public static void Apply(Window window)
        {
            UI.WindowTheme.SetEnabled(window, true);
            UI.WindowTheme.Apply(window);
        }
    }
}
