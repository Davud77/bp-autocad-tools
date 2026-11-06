// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Services;
namespace BotPlusAutocadTool.Commands
{
    public sealed class CheckUpdatesCommand
    {
        [CommandMethod("ATUPDATE")]
        [CommandMethod("АТОБНОВЛЕНИЯ")]
        [CommandMethod("BOTPLUS_CHECKUPDATES")]
        public void Run() => UpdateCheckService.Check(true);
    }
}
