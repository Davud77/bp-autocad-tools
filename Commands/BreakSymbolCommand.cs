// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 OOXGN. See LICENSE in the repository root.
using Autodesk.AutoCAD.Runtime;

namespace BotPlusAutocadTool.Commands
{
    public sealed class BreakSymbolCommand
    {
        [CommandMethod("ATBREAK", CommandFlags.Modal)]
        [CommandMethod("АТРАЗРЫВ", CommandFlags.Modal)]
        public void Run() => DrawingSymbolCommands.Run(false);
    }
}
