// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Input;
using Autodesk.Windows;
using BotPlusAutocadTool.Infrastructure;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.UI
{
    internal sealed class RibbonController : IDisposable
    {
        private const string ButtonIdPrefix = "BOTPLUS_";

        private readonly Dictionary<string, string> _iconByButtonId = new(StringComparer.Ordinal);
        private RibbonTab _tab;
        private RibbonControl _ribbon;
        private bool _darkIcons;

        /// <summary>True when the tab was created on this ribbon instance (no tab lookup).</summary>
        internal bool IsAttachedTo(RibbonControl ribbon) => ribbon != null && ReferenceEquals(_ribbon, ribbon) && _tab != null;

        /// <returns>False while AutoCAD has not created the ribbon yet.</returns>
        public bool EnsureCreated()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return false;
            if (ReferenceEquals(_ribbon, ribbon) && ReferenceEquals(ribbon.FindTab(CommandCatalog.TabId), _tab) && _tab?.Panels.Count == CommandCatalog.Panels.Length)
            {
                ApplyTheme();
                return true;
            }

            var tab = BuildTab();
            var previous = ribbon.FindTab(CommandCatalog.TabId);
            if (previous != null) ribbon.Tabs.Remove(previous);
            ribbon.Tabs.Add(tab);
            _ribbon = ribbon;
            _tab = tab;
            return true;
        }

        /// <summary>Classic mode: the ribbon tab is not shown.</summary>
        public void RemoveIfPresent()
        {
            var ribbon = ComponentManager.Ribbon;
            var existing = ribbon?.FindTab(CommandCatalog.TabId);
            if (existing != null) ribbon.Tabs.Remove(existing);
            _tab = null;
            _ribbon = null;
        }

        /// <summary>Swaps button images after a COLORTHEME change without rebuilding the tab.</summary>
        public void ApplyTheme()
        {
            bool dark = SystemVariables.IsDarkTheme;
            if (_tab == null || dark == _darkIcons) return;
            foreach (var panel in _tab.Panels)
            foreach (var item in panel.Source.Items)
            {
                if (item is RibbonButton button && button.Id != null && _iconByButtonId.TryGetValue(button.Id, out var icon))
                    button.LargeImage = CommandIcons.Get(icon, dark);
            }
            _darkIcons = dark;
        }

        internal RibbonTab BuildTab()
        {
            // Build before attaching: a missing resource cannot leave a half-built tab.
            bool dark = SystemVariables.IsDarkTheme;
            var tab = new RibbonTab { Id = CommandCatalog.TabId, Name = CommandCatalog.TabTitle, Title = CommandCatalog.TabTitle };
            foreach (var definition in CommandCatalog.Panels)
            {
                var source = new RibbonPanelSource { Title = definition.Title };
                foreach (var button in definition.Buttons)
                {
                    string id = ButtonIdPrefix + button.Command;
                    _iconByButtonId[id] = button.Icon;
                    source.Items.Add(new RibbonButton
                    {
                        Id = id,
                        Text = button.Title,
                        ToolTip = button.Description + "\nКоманда: " + button.Command,
                        ShowText = true,
                        ShowImage = true,
                        Size = RibbonItemSize.Large,
                        Orientation = System.Windows.Controls.Orientation.Vertical,
                        LargeImage = CommandIcons.Get(button.Icon, dark),
                        CommandHandler = new RibbonCommand(button.Command)
                    });
                }
                tab.Panels.Add(new RibbonPanel { Source = source });
            }
            _darkIcons = dark;
            return tab;
        }

        public void Dispose()
        {
            if (_tab != null && _ribbon != null) _ribbon.Tabs.Remove(_tab);
            _tab = null;
            _ribbon = null;
        }
    }

    internal sealed class RibbonCommand : ICommand
    {
        // Raised only when drawings open, close or switch, not after every mouse or keyboard input.
        private static event EventHandler AvailabilityChanged;
        internal static void RaiseAvailabilityChanged() => AvailabilityChanged?.Invoke(null, EventArgs.Empty);

        private readonly string _name;
        public RibbonCommand(string name) => _name = name;
        public event EventHandler CanExecuteChanged { add { AvailabilityChanged += value; } remove { AvailabilityChanged -= value; } }
        public bool CanExecute(object parameter) => AcadApp.DocumentManager.MdiActiveDocument != null;
        public void Execute(object parameter)
        {
            try { AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute(_name + " ", true, false, false); }
            catch (Exception ex) { PluginLog.Error("Ribbon command " + _name, ex); }
        }
    }
}
