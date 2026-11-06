// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Windows.Interop;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.ViewModels;
using BotPlusAutocadTool.Views;
// Псевдоним
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Commands
{
    public sealed class CustomerOrderCommand
    {
        [CommandMethod("ATHANDOFF")]
        [CommandMethod("АТПЕРЕДАЧА")]
        public void Run()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                var vm = new CustomerOrderViewModel();
                var win = new CustomerOrderWindow { DataContext = vm };

                // Привязка Owner к окну AutoCAD (чтобы модалка была нормальной)
                new WindowInteropHelper(win).Owner = AcadApp.MainWindow.Handle;

                AcadApp.ShowModalWindow(win);

                if (win.DialogResult != true)
                    return;

                var options = vm.BuildOptions();
                var service = new CustomerOrderService();
                service.Execute(doc, options);
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage($"\n[BotPlus AT] Ошибка: {ex}\n");
            }
        }
    }
}
