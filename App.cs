// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Infrastructure;
using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(BotPlusAutocadTool.App))]

namespace BotPlusAutocadTool
{
    /// <summary>
    /// Startup tasks run once on the first Idle, then the Idle handler is removed.
    /// The interface is checked again only on events: workspace switch (WSCURRENT), commands that
    /// reload menus or the ribbon, a new ribbon instance, and a change of the classic UI setting.
    /// </summary>
    public sealed class App : IExtensionApplication
    {
        // Global command names: work in localized AutoCAD versions.
        private static readonly HashSet<string> InterfaceCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CUI", "CUILOAD", "CUIUNLOAD", "MENU", "MENULOAD", "MENUUNLOAD", "WORKSPACE", "WSSAVE",
            "RIBBON", "RIBBONCLOSE", "MENUBAR", "TOOLBAR", "OPTIONS", "CUIIMPORT", "CUIEXPORT"
        };

        private readonly RibbonController _ribbon = new();
        private readonly ContextMenuController _contextMenu = new();
        private readonly ClassicUiController _classic = new();
        private readonly HashSet<Document> _documents = new();
        private static App _instance;
        private bool _initialized, _idleHooked, _uiPending, _retryAfterCommand, _plotterChecked, _templateChecked;
        private int _uiErrorsLogged;

        internal static void ShowClassicUi()
        {
            if (SettingsService.Settings.EnableClassicUi) _instance?._classic.Show();
            else AcadApp.ShowModalWindow(new Views.AboutWindow());
        }

        /// <summary>The classic UI setting changed: switch between ribbon tab and classic menus.</summary>
        internal static void ApplyInterfaceSettings() => _instance?.RequestUi();

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _instance = this;
            RunSafely("Start selection indicator", SelectionLengthService.Start);
            RunSafely("Start update checks", UpdateCheckService.Start);
            RunSafely("Start stamp tracking", StampSignatureTracking.Start);
            RunSafely("Watch interface events", WatchInterface);
            RequestUi();
        }

        public void Terminate()
        {
            if (!_initialized) return;
            _initialized = false;
            UnhookIdle();
            RunSafely("Stop interface events", UnwatchInterface);
            RunSafely("Stop updates", UpdateCheckService.Stop);
            RunSafely("Stop selection indicator", SelectionLengthService.Stop);
            RunSafely("Stop stamp tracking", StampSignatureTracking.Stop);
            RunSafely("Stop publication", DsdPublishService.Shutdown);
            RunSafely("Stop Excel sessions", TableExcelService.Shutdown);
            RunSafely("Stop copy restrictions", CopyProtectionService.Shutdown);
            RunSafely("Close length window", Commands.SumLengthCommand.CloseWindow);
            RunSafely("Remove context menu", _contextMenu.Dispose);
            RunSafely("Remove ribbon", _ribbon.Dispose);
            RunSafely("Remove classic UI", _classic.Dispose);
            _instance = null;
        }

        private void WatchInterface()
        {
            foreach (Document document in AcadApp.DocumentManager) Attach(document);
            AcadApp.DocumentManager.DocumentCreated += OnDocumentCreated;
            AcadApp.DocumentManager.DocumentToBeDestroyed += OnDocumentClosing;
            AcadApp.DocumentManager.DocumentDestroyed += OnDocumentDestroyed;
            AcadApp.DocumentManager.DocumentActivated += OnDocumentActivated;
            AcadApp.SystemVariableChanged += OnSystemVariableChanged;
            Autodesk.Windows.ComponentManager.ItemInitialized += OnRibbonItemInitialized;
        }

        private void UnwatchInterface()
        {
            Autodesk.Windows.ComponentManager.ItemInitialized -= OnRibbonItemInitialized;
            AcadApp.SystemVariableChanged -= OnSystemVariableChanged;
            AcadApp.DocumentManager.DocumentCreated -= OnDocumentCreated;
            AcadApp.DocumentManager.DocumentToBeDestroyed -= OnDocumentClosing;
            AcadApp.DocumentManager.DocumentDestroyed -= OnDocumentDestroyed;
            AcadApp.DocumentManager.DocumentActivated -= OnDocumentActivated;
            foreach (var document in new List<Document>(_documents)) Detach(document);
        }

        private void Attach(Document document)
        {
            if (document == null || !_documents.Add(document)) return;
            document.CommandEnded += OnCommandEnded;
            document.CommandCancelled += OnCommandEnded;
            document.CommandFailed += OnCommandEnded;
        }

        private void Detach(Document document)
        {
            if (!_documents.Remove(document)) return;
            try
            {
                document.CommandEnded -= OnCommandEnded;
                document.CommandCancelled -= OnCommandEnded;
                document.CommandFailed -= OnCommandEnded;
            }
            catch { /* The document may already be closing. */ }
        }

        private void OnDocumentCreated(object sender, DocumentCollectionEventArgs args) { Attach(args.Document); RibbonCommand.RaiseAvailabilityChanged(); }
        private void OnDocumentClosing(object sender, DocumentCollectionEventArgs args) => Detach(args.Document);
        private void OnDocumentDestroyed(object sender, DocumentDestroyedEventArgs args) => RibbonCommand.RaiseAvailabilityChanged();
        private void OnDocumentActivated(object sender, DocumentCollectionEventArgs args) => RibbonCommand.RaiseAvailabilityChanged();

        private void OnSystemVariableChanged(object sender, Autodesk.AutoCAD.ApplicationServices.SystemVariableChangedEventArgs args)
        {
            if (string.Equals(args.Name, "WSCURRENT", StringComparison.OrdinalIgnoreCase)) RequestUi();
        }

        private void OnCommandEnded(object sender, CommandEventArgs args)
        {
            if (_retryAfterCommand || InterfaceCommands.Contains(args.GlobalCommandName ?? "")) RequestUi();
        }

        // Fires while AutoCAD builds ribbon items; only a reference comparison here.
        private void OnRibbonItemInitialized(object sender, Autodesk.Windows.RibbonItemEventArgs args)
        {
            if (_uiPending || SettingsService.Settings.EnableClassicUi) return;
            var ribbon = Autodesk.Windows.ComponentManager.Ribbon;
            if (ribbon != null && !_ribbon.IsAttachedTo(ribbon)) RequestUi();
        }

        private void RequestUi()
        {
            _uiPending = true;
            HookIdle();
        }

        private void HookIdle()
        {
            if (_idleHooked) return;
            AcadApp.Idle += OnIdle;
            _idleHooked = true;
        }

        private void UnhookIdle()
        {
            if (!_idleHooked) return;
            AcadApp.Idle -= OnIdle;
            _idleHooked = false;
        }

        /// <summary>One-shot: runs pending work, then unsubscribes from Idle.</summary>
        private void OnIdle(object sender, EventArgs args)
        {
            UnhookIdle();
            // Publishing in progress: the plotter is checked after a later command.
            if (!_plotterChecked && !DsdPublishService.IsBusy)
            {
                _plotterChecked = true;
                // A failure is not repeated in this session: printing installs the plotter on demand.
                RunSafely("Install PDF plotter", () => PdfPlotterService.EnsureInstalled());
            }
            if (!_templateChecked)
            {
                try { RepairTemplate(); _templateChecked = true; }
                catch (System.Exception ex) { PluginLog.Error("Repair QNEW template", ex); }
            }
            bool uiReady = !_uiPending || EnsureUi();
            // Unfinished work waits for the end of the next command, never for a timer.
            _retryAfterCommand = !_plotterChecked || !_templateChecked || !uiReady;
        }

        /// <returns>False when the interface must be checked again later.</returns>
        private bool EnsureUi()
        {
            _uiPending = false;
            try
            {
                _contextMenu.EnsureCreated();
                // One interface at a time: classic menus and toolbar, or the ribbon tab.
                if (SettingsService.Settings.EnableClassicUi)
                {
                    _ribbon.RemoveIfPresent();
                    _classic.EnsureCreated();
                    return true;
                }
                _classic.RemoveIfPresent();
                // No ribbon yet (startup, closed ribbon, Core Console): ItemInitialized or a later command retries.
                return _ribbon.EnsureCreated() || !HostInfo.IsDesktop;
            }
            catch (System.Exception ex)
            {
                // Typically "property unavailable in the current state" while AutoCAD is busy.
                if (_uiErrorsLogged++ < 3) PluginLog.Error("Create UI", ex);
                return false;
            }
        }

        private static void RepairTemplate()
        {
            if (!HostInfo.IsDesktop) return;
            dynamic host = AcadApp.AcadApplication;
            dynamic files = host.Preferences.Files;
            string old = ((string)files.QNewTemplateFile ?? "").TrimEnd('\\', '/');
            string template = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(App).Assembly.Location), "Assets", "botplus.dwt");
            if (System.IO.Path.GetFileName(old).Equals("botplus.dwt", StringComparison.OrdinalIgnoreCase) && !System.IO.File.Exists(old) && System.IO.File.Exists(template)) files.QNewTemplateFile = template;
        }

        private static void RunSafely(string operation, Action action)
        {
            try { action(); }
            catch (System.Exception ex) { PluginLog.Error(operation, ex); }
        }
    }
}
