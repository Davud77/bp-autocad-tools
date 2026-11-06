// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Services;
// Core.Application
using AcadCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace BotPlusAutocadTool.Commands
{
    public sealed class TableExcelCommand
    {
        [CommandMethod("ATEXCEL", CommandFlags.UsePickSet | CommandFlags.Modal)]
        [CommandMethod("АТЭКСЕЛЬ", CommandFlags.UsePickSet | CommandFlags.Modal)]
        public void Run()
        {
            var doc = AcadCoreApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;

            try
            {
                var peo = new PromptEntityOptions("\nВыберите таблицу AutoCAD: ");
                peo.SetRejectMessage("\nНужен объект TABLE.");
                peo.AddAllowedClass(typeof(Table), exactMatch: false);

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                var service = new TableExcelService();
                service.ExportOpenAndAutoImport(doc, per.ObjectId);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[BotPlus AT] Ошибка: {ex}\n");
            }
        }
    }
}
