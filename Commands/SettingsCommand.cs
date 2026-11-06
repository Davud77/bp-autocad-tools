// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
namespace BotPlusAutocadTool.Commands
{
    public sealed class SettingsCommand
    {
        [CommandMethod("ATSETTINGS", CommandFlags.Modal)]
        [CommandMethod("АТНАСТРОЙКИ", CommandFlags.Modal)]
        [CommandMethod("BOTPLUS_SETTINGS", CommandFlags.Modal)]
        public void Run() => AcadApp.ShowModalWindow(new Views.SettingsWindow());
    }
}
