// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Views;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Commands
{
    public class InfoCommand
    {
        [CommandMethod("ATINFO")]
        [CommandMethod("АТИНФО")]
        public void Execute()
        {
            var window = new AboutWindow();
            // Показываем как модальное окно поверх AutoCAD
            AcadApp.ShowModalWindow(window);
        }
    }
}