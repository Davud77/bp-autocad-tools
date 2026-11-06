// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Diagnostics;

namespace BotPlusAutocadTool.Infrastructure
{
    /// <summary>Facts about the host process that cannot change during a session.</summary>
    internal static class HostInfo
    {
        private static readonly Lazy<bool> Desktop = new Lazy<bool>(() =>
        {
            try
            {
                using (var process = Process.GetCurrentProcess())
                    return process.ProcessName.Equals("acad", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        });

        /// <summary>Desktop AutoCAD (acad.exe) with menus and toolbars, not Core Console.</summary>
        public static bool IsDesktop => Desktop.Value;
    }
}
