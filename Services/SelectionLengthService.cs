// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Windows;
using BotPlusAutocadTool.Infrastructure;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Forms = System.Windows.Forms;

namespace BotPlusAutocadTool.Services
{
    /// <summary>
    /// Status bar field with the total length of the current selection.
    /// Left click copies the value, right click opens "Enable/Disable". When disabled the service
    /// is not subscribed to selection, command or Idle events and calculates nothing.
    /// </summary>
    public static class SelectionLengthService
    {
        /// <summary>Larger selections are calculated only on click.</summary>
        internal const int AutomaticLimit = 5000;
        private const string Prefix = "Сумм. длина: ";
        private const string ToolTip = "Длина текущего выделения в единицах чертежа: линии, полилинии, дуги и сплайны.\n" +
            "Левый клик — копировать значение. Правый клик — включить или выключить подсчёт.";

        private static readonly HashSet<Document> Documents = new HashSet<Document>();
        private static Pane _pane;
        private static bool _started, _listening, _dirty, _idleHooked, _paneUnavailable, _largeSelection, _windowListening;
        private static DateTime _retryPaneAt, _nextRefreshUtc;
        private static double _total;
        private static int _largeCount;
        private static string _displayedText;
        private static string _previousMacro, _ownedMacro;
        private static Forms.ContextMenuStrip _menu;

        internal static bool Enabled => SettingsService.Settings.SelectionLengthEnabled;

        public static double Calculate(Transaction transaction, IEnumerable<ObjectId> ids)
        {
            double total = 0;
            foreach (var id in ids)
            {
                if (id.IsNull || !id.IsValid || id.IsErased) continue;
                try
                {
                    if (transaction.GetObject(id, OpenMode.ForRead, false) is Entity entity &&
                        CurveGeometry.TryGetLength(entity, includeCircles: false, out var length)) total += length;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception) { /* An erased or unsupported object must not break selection. */ }
            }
            return total;
        }

        public static void Start()
        {
            // Core Console has no status bar: touching it there terminates the host.
            if (_started || !Infrastructure.HostInfo.IsDesktop) return;
            _started = true;
            try
            {
                if (Enabled) StartListening();
                // The status bar may not exist yet while AutoCAD loads: create the field on the first Idle.
                _dirty = true;
                HookIdle();
            }
            catch { Stop(); throw; }
        }

        public static void Stop()
        {
            if (!_started) return;
            _started = false;
            StopListening();
            UnhookIdle();
            try
            {
                CloseMenu();
                if (_pane != null)
                {
                    _pane.MouseDown -= OnPaneMouseDown;
                    AcadApp.StatusBar.Panes.Remove(_pane); _pane.Dispose(); AcadApp.StatusBar.Update();
                }
                RestoreMacro();
            }
            catch (System.Exception) { /* Native UI may already be disposed during shutdown. */ }
            _pane = null;
            _displayedText = null;
            _previousMacro = _ownedMacro = null;
            _paneUnavailable = false;
            _nextRefreshUtc = DateTime.MinValue;
        }

        /// <summary>Turns the indicator on or off and remembers the choice.</summary>
        internal static void SetEnabled(bool enabled)
        {
            SettingsService.Settings.SelectionLengthEnabled = enabled;
            SettingsService.Save();
            if (!_started) return;
            _total = 0; _largeSelection = false;
            if (enabled) { StartListening(); _dirty = true; HookIdle(); }
            else { StopListening(); Display(); }
        }

        /// <summary>The "Sum of lengths" window calculates the same selection: reuse its result.</summary>
        internal static void SetWindowListening(bool listening)
        {
            _windowListening = listening;
            if (!listening && _listening) { _dirty = true; HookIdle(); }
        }

        internal static void PublishFromWindow(double total)
        {
            if (!_started || !_listening) return;
            _total = total; _largeSelection = false;
            Display();
        }

        private static void StartListening()
        {
            if (_listening) return;
            _listening = true;
            foreach (Document doc in AcadApp.DocumentManager) Attach(doc);
            AcadApp.DocumentManager.DocumentCreated += OnCreated;
            AcadApp.DocumentManager.DocumentActivated += OnActivated;
            AcadApp.DocumentManager.DocumentToBeDestroyed += OnClosing;
        }

        private static void StopListening()
        {
            if (!_listening) return;
            _listening = false;
            _dirty = false;
            AcadApp.DocumentManager.DocumentCreated -= OnCreated;
            AcadApp.DocumentManager.DocumentActivated -= OnActivated;
            AcadApp.DocumentManager.DocumentToBeDestroyed -= OnClosing;
            foreach (var doc in new List<Document>(Documents)) Detach(doc);
            // Idle stays only if the field itself still has to be created.
            if (_pane != null || _paneUnavailable) UnhookIdle();
        }

        private static void Attach(Document doc)
        {
            if (!Documents.Add(doc)) return;
            doc.ImpliedSelectionChanged += OnChanged;
            doc.CommandEnded += OnChanged;
            doc.CommandCancelled += OnChanged;
            doc.CommandFailed += OnChanged;
        }

        private static void Detach(Document doc)
        {
            if (!Documents.Remove(doc)) return;
            try
            {
                doc.ImpliedSelectionChanged -= OnChanged;
                doc.CommandEnded -= OnChanged;
                doc.CommandCancelled -= OnChanged;
                doc.CommandFailed -= OnChanged;
            }
            catch { /* The document may already be closing. */ }
        }

        private static void OnCreated(object sender, DocumentCollectionEventArgs e) { Attach(e.Document); MarkDirty(); }
        private static void OnActivated(object sender, DocumentCollectionEventArgs e) => MarkDirty();
        private static void OnClosing(object sender, DocumentCollectionEventArgs e) { Detach(e.Document); MarkDirty(); }
        private static void OnChanged(object sender, EventArgs e) => MarkDirty();

        private static void MarkDirty()
        {
            _dirty = true;
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

        /// <summary>One-shot: refreshes once after a change, then unsubscribes.</summary>
        private static void OnIdle(object sender, EventArgs e)
        {
            if (DateTime.UtcNow < _nextRefreshUtc) return;
            try { Refresh(); }
            catch (System.Exception ex)
            {
                UnhookIdle();
                Infrastructure.PluginLog.Error("Selection indicator", ex);
            }
        }

        private static void Refresh()
        {
            if (!_listening)
            {
                Display();
                if (_pane != null || _paneUnavailable) UnhookIdle();
                return;
            }
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            // The command end event schedules a new refresh.
            if (doc != null && !string.IsNullOrEmpty(doc.CommandInProgress)) { UnhookIdle(); return; }
            UnhookIdle();
            if (!_dirty) return;
            _dirty = false;
            _nextRefreshUtc = DateTime.UtcNow.AddMilliseconds(100);
            if (_windowListening) { Display(); return; }
            _total = 0; _largeSelection = false;
            try
            {
                if (doc != null)
                {
                    var selection = doc.Editor.SelectImplied();
                    if (selection.Status == PromptStatus.OK)
                    {
                        using (selection.Value)
                        {
                            var ids = selection.Value.GetObjectIds();
                            if (ids.Length > AutomaticLimit) { _largeSelection = true; _largeCount = ids.Length; }
                            else _total = CalculateFor(doc, ids);
                        }
                    }
                }
            }
            catch (System.Exception ex) { Infrastructure.PluginLog.Error("Calculate selected length", ex); }
            Display();
        }

        private static double CalculateFor(Document doc, ObjectId[] ids)
        {
            using (doc.LockDocument())
            using (var transaction = doc.Database.TransactionManager.StartOpenCloseTransaction())
                return Calculate(transaction, ids);
        }

        private static string FormatTotal() => _total.ToString("F2", CultureInfo.CurrentCulture);

        private static string CurrentText()
        {
            if (!_listening) return Prefix + "выкл.";
            if (_largeSelection) return Prefix + "? (" + _largeCount.ToString(CultureInfo.CurrentCulture) + " об., клик — расчёт)";
            return Prefix + (_total > 0 ? FormatTotal() : "—");
        }

        private static void Display()
        {
            var text = CurrentText();
            if (_pane == null && (!_paneUnavailable || DateTime.UtcNow >= _retryPaneAt))
            {
                try
                {
                    var pane = new Pane { Text = text, MinimumWidth = 110, ToolTipText = ToolTip, Visible = true };
                    // Native Model/Paper switch is the first built-in pane.
                    AcadApp.StatusBar.Panes.Insert(0, pane);
                    pane.MouseDown += OnPaneMouseDown;
                    _pane = pane;
                    _paneUnavailable = false;
                    _displayedText = null;
                    RestoreMacro();
                }
                catch (System.Exception) { _pane = null; _paneUnavailable = true; _retryPaneAt = DateTime.UtcNow.AddSeconds(10); }
            }
            if (_pane != null)
            {
                if (_displayedText == text) return;
                _displayedText = text;
                _pane.Text = text;
                _pane.Visible = true;
                AcadApp.StatusBar.Update();
                return;
            }
            // Same mechanism as the supplied LISP when native panes are unavailable (no mouse: always on).
            if (!_listening) { RestoreMacro(); return; }
            if (_previousMacro == null) _previousMacro = SystemVariables.GetString("MODEMACRO");
            var current = SystemVariables.GetString("MODEMACRO");
            if (_ownedMacro == null || current == _ownedMacro)
            {
                _ownedMacro = _total > 0 ? Prefix + FormatTotal() : _previousMacro;
                AcadApp.SetSystemVariable("MODEMACRO", _ownedMacro);
            }
        }

        private static void RestoreMacro()
        {
            if (_previousMacro != null && SystemVariables.GetString("MODEMACRO") == _ownedMacro)
                AcadApp.SetSystemVariable("MODEMACRO", _previousMacro);
            _previousMacro = _ownedMacro = null;
        }

        private static void OnPaneMouseDown(object sender, StatusBarMouseDownEventArgs e)
        {
            try
            {
                if (e.Button == Forms.MouseButtons.Right) ShowMenu();
                else if (e.Button == Forms.MouseButtons.Left) CopyValue();
            }
            catch (System.Exception ex) { Infrastructure.PluginLog.Error("Selection indicator click", ex); }
        }

        private static void CopyValue()
        {
            if (!_listening) return;
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (_largeSelection && doc != null && string.IsNullOrEmpty(doc.CommandInProgress))
            {
                // A large selection is calculated only on explicit request.
                var selection = doc.Editor.SelectImplied();
                if (selection.Status == PromptStatus.OK)
                    using (selection.Value) _total = CalculateFor(doc, selection.Value.GetObjectIds());
                _largeSelection = false;
                Display();
            }
            if (_total <= 0) return;
            string value = FormatTotal();
            try
            {
                Forms.Clipboard.SetText(value);
                doc?.Editor.WriteMessage("\n[BotPlus AT] Длина " + value + " скопирована в буфер обмена.\n");
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                doc?.Editor.WriteMessage("\n[BotPlus AT] Буфер обмена занят другой программой. Повторите клик.\n");
            }
        }

        private static void ShowMenu()
        {
            CloseMenu();
            var menu = new Forms.ContextMenuStrip();
            bool enabled = _listening;
            var toggle = new Forms.ToolStripMenuItem(enabled ? "Выключить подсчёт длины" : "Включить подсчёт длины");
            toggle.Click += (_, _) => SetEnabled(!enabled);
            menu.Items.Add(toggle);
            if (enabled && (_total > 0 || _largeSelection))
            {
                var copy = new Forms.ToolStripMenuItem("Копировать значение");
                copy.Click += (_, _) => CopyValue();
                menu.Items.Add(copy);
            }
            // Disposing inside Closed would destroy the menu before its Click handler runs.
            menu.Closed += (_, _) =>
            {
                EventHandler dispose = null;
                dispose = (_, _) =>
                {
                    AcadApp.Idle -= dispose;
                    if (ReferenceEquals(_menu, menu)) _menu = null;
                    menu.Dispose();
                };
                AcadApp.Idle += dispose;
            };
            _menu = menu;
            menu.Show(Forms.Cursor.Position);
        }

        private static void CloseMenu()
        {
            var menu = _menu;
            _menu = null;
            menu?.Dispose();
        }
    }
}
