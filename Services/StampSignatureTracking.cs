// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Services
{
    /// <summary>
    /// Keeps stamp signatures and wrapped fields valid after AutoCAD regenerates the anonymous
    /// definitions of the dynamic WM_FORMAT block. Works only at fixed moments, never by polling:
    /// after a command that changed a WM_FORMAT reference, after REGEN/REGENALL/UPDATEFIELD/DWGPROPS
    /// and before QSAVE/SAVE/SAVEAS. Drawings without WM_FORMAT are not observed at database level.
    /// </summary>
    internal static class StampSignatureTracking
    {
        private const string OwnerKey = "BotPlusAutocadTool.SignatureTracking.Stop";

        private sealed class State
        {
            public Database Database;
            public bool DatabaseHooked;
            public ObjectId FormatId = ObjectId.Null;
            public bool Dirty;
            public string SourceState;
        }

        private static readonly HashSet<string> RefreshCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "REGEN", "REGENALL", "UPDATEFIELD", "DWGPROPS" };
        private static readonly HashSet<string> RegenCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "REGEN", "REGENALL" };
        private static readonly HashSet<string> SaveCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QSAVE", "SAVE", "SAVEAS" };
        private static readonly HashSet<string> UndoCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "U", "UNDO", "REDO", "MREDO" };

        private static readonly Dictionary<Document, State> Documents = new Dictionary<Document, State>();
        private static readonly Dictionary<Database, State> Databases = new Dictionary<Database, State>();
        // Document -> regeneration allowed after the update.
        private static readonly Dictionary<Document, bool> Pending = new Dictionary<Document, bool>();
        private static bool _started, _updating, _idleHooked;

        public static void Start()
        {
            if (_started) return;
            (AppDomain.CurrentDomain.GetData(OwnerKey) as Action)?.Invoke();
            AppDomain.CurrentDomain.SetData(OwnerKey, (Action)Stop);
            _started = true;
            foreach (Document document in AcadApp.DocumentManager) Attach(document);
            AcadApp.DocumentManager.DocumentCreated += OnCreated;
            AcadApp.DocumentManager.DocumentToBeDestroyed += OnClosing;
            AcadApp.DocumentManager.DocumentActivated += OnActivated;
        }

        public static void Stop()
        {
            if (!_started) return;
            _started = false;
            AcadApp.DocumentManager.DocumentCreated -= OnCreated;
            AcadApp.DocumentManager.DocumentToBeDestroyed -= OnClosing;
            AcadApp.DocumentManager.DocumentActivated -= OnActivated;
            foreach (var document in new List<Document>(Documents.Keys)) Detach(document);
            Pending.Clear();
            UnhookIdle();
        }

        private static void Attach(Document document)
        {
            if (document == null || Documents.ContainsKey(document)) return;
            var state = new State { Database = document.Database };
            Documents[document] = state;
            document.CommandWillStart += OnCommandWillStart;
            document.CommandEnded += OnCommandEnded;
            document.CommandCancelled += OnCommandEnded;
            document.CommandFailed += OnCommandEnded;
            try { RefreshFormat(state); }
            catch (Exception ex) { Infrastructure.PluginLog.Error("Stamp tracking attach", ex); }
        }

        private static void Detach(Document document)
        {
            if (!Documents.TryGetValue(document, out var state)) return;
            Documents.Remove(document);
            Pending.Remove(document);
            try
            {
                document.CommandWillStart -= OnCommandWillStart;
                document.CommandEnded -= OnCommandEnded;
                document.CommandCancelled -= OnCommandEnded;
                document.CommandFailed -= OnCommandEnded;
            }
            catch { /* The document may already be closing. */ }
            UnhookDatabase(state);
        }

        private static void OnCreated(object sender, DocumentCollectionEventArgs args) => Attach(args.Document);
        private static void OnClosing(object sender, DocumentCollectionEventArgs args) => Detach(args.Document);
        private static void OnActivated(object sender, DocumentCollectionEventArgs args)
        {
            if (args.Document != null && Pending.ContainsKey(args.Document)) HookIdle();
        }

        /// <summary>Observes the database only while it contains the WM_FORMAT definition.</summary>
        private static void RefreshFormat(State state)
        {
            var formatId = ObjectId.Null;
            using (var transaction = state.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var table = (BlockTable)transaction.GetObject(state.Database.BlockTableId, OpenMode.ForRead);
                if (table.Has(FormatInsertService.BlockName))
                {
                    var definition = (BlockTableRecord)transaction.GetObject(table[FormatInsertService.BlockName], OpenMode.ForRead);
                    if (!definition.IsFromExternalReference) formatId = definition.ObjectId;
                }
            }
            state.FormatId = formatId;
            if (formatId.IsNull) { UnhookDatabase(state); return; }
            if (state.DatabaseHooked) return;
            state.Database.ObjectModified += OnObjectChanged;
            state.Database.ObjectAppended += OnObjectChanged;
            Databases[state.Database] = state;
            state.DatabaseHooked = true;
            // The current DWG properties are the baseline: opening a drawing must not modify it.
            state.SourceState = StampTextLayoutService.SourceState(state.Database);
        }

        private static void UnhookDatabase(State state)
        {
            if (!state.DatabaseHooked) return;
            try { state.Database.ObjectModified -= OnObjectChanged; state.Database.ObjectAppended -= OnObjectChanged; }
            catch { /* Database may already be disposed. */ }
            Databases.Remove(state.Database);
            state.DatabaseHooked = false;
            state.Dirty = false;
        }

        private static void OnObjectChanged(object sender, ObjectEventArgs args)
        {
            // Only flags the drawing: no objects are opened and nothing is calculated here.
            if (_updating || !(args.DBObject is BlockReference reference) || !(sender is Database database)) return;
            if (!Databases.TryGetValue(database, out var state) || state.Dirty) return;
            try
            {
                if (reference.BlockTableRecord == state.FormatId ||
                    (reference.IsDynamicBlock && reference.DynamicBlockTableRecord == state.FormatId))
                    state.Dirty = true;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) { /* An erased or proxy reference is not a frame. */ }
        }

        private static void OnCommandEnded(object sender, CommandEventArgs args)
        {
            if (_updating || !(sender is Document document) || !Documents.TryGetValue(document, out var state)) return;
            try
            {
                string command = args.GlobalCommandName ?? "";
                if (UndoCommands.Contains(command)) { state.Dirty = false; return; }
                if (!state.DatabaseHooked)
                {
                    // A frame can arrive by INSERT, paste or DesignCenter: one cheap block table lookup.
                    RefreshFormat(state);
                    if (!state.DatabaseHooked) return;
                    state.Dirty = true;
                }
                if (state.Dirty || RefreshCommands.Contains(command))
                    Request(document, allowRegen: !RegenCommands.Contains(command));
            }
            catch (Exception ex) { Infrastructure.PluginLog.Error("Stamp tracking command", ex); }
        }

        private static void OnCommandWillStart(object sender, CommandEventArgs args)
        {
            if (_updating || !(sender is Document document) || !Documents.TryGetValue(document, out var state)) return;
            if (!state.DatabaseHooked || !SaveCommands.Contains(args.GlobalCommandName ?? "")) return;
            Pending.Remove(document);
            // Saving writes the result: update synchronously, before the file is written.
            Update(document, state, allowRegen: false);
        }

        private static void Request(Document document, bool allowRegen)
        {
            Pending[document] = Pending.TryGetValue(document, out var previous) ? previous && allowRegen : allowRegen;
            HookIdle();
        }

        private static void HookIdle()
        {
            if (_idleHooked) return;
            AcadApp.Idle += OnIdle;
            _idleHooked = true;
        }

        private static void UnhookIdle()
        {
            if (!_idleHooked) return;
            AcadApp.Idle -= OnIdle;
            _idleHooked = false;
        }

        /// <summary>One-shot: runs once after the requesting command, then unsubscribes.</summary>
        private static void OnIdle(object sender, EventArgs args)
        {
            var document = AcadApp.DocumentManager.MdiActiveDocument;
            if (document == null || !Pending.TryGetValue(document, out bool allowRegen))
            {
                // Other drawings are processed when they become active again.
                UnhookIdle();
                return;
            }
            if (!document.Editor.IsQuiescent) return;
            Pending.Remove(document);
            if (Pending.Count == 0) UnhookIdle();
            if (Documents.TryGetValue(document, out var state)) Update(document, state, allowRegen);
        }

        private static void Update(Document document, State state, bool allowRegen)
        {
            if (!state.DatabaseHooked) return;
            var database = state.Database;
            string source;
            try { source = StampTextLayoutService.SourceState(database); }
            catch (Exception ex) { Infrastructure.PluginLog.Error("Stamp source state", ex); return; }
            bool sourceChanged = state.SourceState != source;
            // Cheap check: nothing changed since the previous update.
            if (!state.Dirty && !sourceChanged) return;
            state.Dirty = false;
            _updating = true;
            try
            {
                using (document.LockDocument())
                {
                    StampSignatureDwgService.Synchronize(database);
                    StampTextLayoutService.Apply(database, evaluateAllFields: sourceChanged);
                    if (sourceChanged && allowRegen) document.Editor.Regen();
                }
            }
            catch (Exception ex) { document.Editor.WriteMessage("\n[BotPlus AT] Не удалось обновить штамп: " + ex.Message + "\n"); }
            finally
            {
                try { state.SourceState = StampTextLayoutService.SourceState(database); } catch { state.SourceState = source; }
                _updating = false;
            }
        }
    }
}
