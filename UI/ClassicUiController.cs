// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using BotPlusAutocadTool.Infrastructure;
using BotPlusAutocadTool.Services;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.UI
{
    /// <summary>Desktop ActiveX menus/toolbars share the ribbon command catalog.</summary>
    internal sealed class ClassicUiController : IDisposable
    {
        // Renamed when the menu macros change: an existing CUI menu with other macros is never rewritten in place.
        internal static string UiName => CommandCatalog.TabTitle + " 1.10";
        private dynamic _menu, _toolbar;
        private string _workspace;
        private string _disabledWorkspace;
        private bool? _bitmapsDark;
        private const int AcToolbarButton = 0;
        internal void Show()
        {
            _workspace = null;
            EnsureCreated();
            AcadApp.SetSystemVariable("MENUBAR", 1);
        }
        // Called only on interface events (startup, workspace switch, CUI reload, settings change), not by polling.
        internal void RemoveIfPresent()
        {
            if (!HostInfo.IsDesktop) return;
            string workspace = SystemVariables.GetString("WSCURRENT");
            if (_disabledWorkspace == workspace && _menu == null && _toolbar == null) return;
            dynamic app = AcadApp.AcadApplication;
            dynamic group = app.MenuGroups.Item(0);
            _menu ??= Find(group.Menus, UiName);
            _toolbar ??= Find(group.Toolbars, UiName);
            if (_menu != null || _toolbar != null) Dispose();
            HideLegacy(group);
            _disabledWorkspace = workspace;
        }
        internal void EnsureCreated()
        {
            _disabledWorkspace = null;
            if (!HostInfo.IsDesktop) return;
            string workspace = SystemVariables.GetString("WSCURRENT");
            if (_menu != null && _toolbar != null && _workspace == workspace) return;
            dynamic app = AcadApp.AcadApplication;
            dynamic group = app.MenuGroups.Item(0);
            HideLegacy(group);
            _menu = Find(group.Menus, UiName);
            bool populateMenu = _menu == null || !MenuMatches(_menu);
            if (_menu == null)
            {
                _menu = group.Menus.Add(UiName);
            }
            if (populateMenu)
            {
                // Keep CUI definitions while hidden: deleting live ActiveX objects can invalidate native UI callbacks.
                if ((int)_menu.Count != 0) throw new InvalidOperationException("Классическое меню изменено. Используйте ленту или восстановите меню " + UiName + " в CUI.");
                foreach (var panel in CommandCatalog.Panels)
                {
                    dynamic submenu = _menu.AddSubMenu(_menu.Count, panel.Title);
                    foreach (var button in panel.Buttons)
                        submenu.AddMenuItem(submenu.Count, Caption(button.Title), Macro(button.Command));
                }
            }
            if (!(bool)_menu.OnMenuBar) _menu.InsertInMenuBar(app.MenuBar.Count);
            _toolbar = Find(group.Toolbars, UiName);
            int expectedCount = CommandCatalog.Panels.Sum(panel => panel.Buttons.Length) + CommandCatalog.Panels.Length - 1;
            if (_toolbar != null && (int)_toolbar.Count != expectedCount)
            { _toolbar.Visible = false; throw new InvalidOperationException("Классическая панель изменена. Используйте ленту или восстановите панель " + UiName + " в CUI."); }
            if (_toolbar == null)
            {
                _toolbar = group.Toolbars.Add(UiName);
                _bitmapsDark = SystemVariables.IsDarkTheme;
                _toolbar.Visible = true;
                foreach (var panel in CommandCatalog.Panels)
                {
                    if ((int)_toolbar.Count > 0) _toolbar.AddSeparator(_toolbar.Count);
                    foreach (var button in panel.Buttons)
                    {
                        dynamic item = _toolbar.AddToolbarButton(_toolbar.Count, Caption(button.Title), button.Description, Macro(button.Command));
                        try { item.SetBitmaps(GetBitmap(button.Icon, 16), GetBitmap(button.Icon, 32)); }
                        catch (Exception ex) { PluginLog.Error("Classic toolbar icon", ex); }
                    }
                }
                _toolbar.Dock(0); // acToolbarDockTop
            }
            // Classic mode replaces the ribbon tab, so the toolbar does not depend on ribbon visibility.
            _toolbar.Visible = true;
            ApplyTheme();
            _workspace = workspace;
        }
        private static void HideLegacy(dynamic group)
        {
            HideNamed(group, CommandCatalog.TabTitle + " 1.8.1");
            HideNamed(group, CommandCatalog.TabTitle + " 1.8");
            HideNamed(group, CommandCatalog.TabTitle);
        }
        private static void HideNamed(dynamic group, string name)
        {
            dynamic legacyMenu = Find(group.Menus, name);
            if (legacyMenu != null && (bool)legacyMenu.OnMenuBar) legacyMenu.RemoveFromMenuBar();
            dynamic legacyToolbar = Find(group.Toolbars, name);
            if (legacyToolbar != null) legacyToolbar.Visible = false;
        }
        private static bool MenuMatches(dynamic menu)
        {
            if ((int)menu.Count != CommandCatalog.Panels.Length) return false;
            for (int p = 0; p < CommandCatalog.Panels.Length; p++)
            {
                dynamic submenu = menu.Item(p).SubMenu;
                if ((int)submenu.Count != CommandCatalog.Panels[p].Buttons.Length) return false;
                for (int b = 0; b < CommandCatalog.Panels[p].Buttons.Length; b++)
                    if ((string)submenu.Item(b).Macro != Macro(CommandCatalog.Panels[p].Buttons[b].Command)) return false;
            }
            return true;
        }
        private static object Find(dynamic collection, string name)
        {
            for (int i = 0; i < (int)collection.Count; i++)
            {
                dynamic item = collection.Item(i);
                if (string.Equals((string)item.Name, name, StringComparison.Ordinal)) return item;
            }
            return null;
        }
        internal static string Caption(string title) => title.Replace("\n", " ").Replace("Мульти- копирование", "Мультикопирование");
        internal static string Macro(string command) => "\u0003\u0003" + command + "\r";
        /// <summary>Re-applies toolbar bitmaps when COLORTHEME or the icon artwork changed.</summary>
        internal void ApplyTheme()
        {
            if (_toolbar == null) return;
            bool dark = SystemVariables.IsDarkTheme;
            if (_bitmapsDark == dark) return;
            var iconByMacro = CommandCatalog.Panels.SelectMany(panel => panel.Buttons)
                .ToDictionary(button => Macro(button.Command), button => button.Icon, StringComparer.Ordinal);
            for (int i = 0; i < (int)_toolbar.Count; i++)
            {
                dynamic item = _toolbar.Item(i);
                if ((int)item.Type != AcToolbarButton || !iconByMacro.TryGetValue((string)item.Macro, out var icon)) continue;
                try { item.SetBitmaps(GetBitmap(icon, 16, dark), GetBitmap(icon, 32, dark)); }
                catch (Exception ex) { PluginLog.Error("Classic toolbar icon " + icon, ex); }
            }
            _bitmapsDark = dark;
        }
        internal static string GetBitmap(string icon, int size) => GetBitmap(icon, size, SystemVariables.IsDarkTheme);
        /// <summary>
        /// ActiveX toolbars take file paths to opaque BMPs, so the PNG is flattened onto the toolbar
        /// background once and cached per artwork version, theme and size.
        /// </summary>
        private static string GetBitmap(string icon, int size, bool dark)
        {
            string directory = Path.Combine(PluginPaths.UserDataDirectory, "UI", "Icons " + CommandIcons.ArtworkVersion,
                CommandIcons.ThemeName(dark), size.ToString(CultureInfo.InvariantCulture));
            string path = Path.Combine(directory, Path.ChangeExtension(icon, ".bmp"));
            if (File.Exists(path)) return path;
            Directory.CreateDirectory(directory);
            var source = new BitmapImage(CommandIcons.PackUri(icon, dark));
            var visual = new System.Windows.Media.DrawingVisual();
            using (var draw = visual.RenderOpen())
            {
                var background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(dark ? "#3B4453" : "#ECEFF3");
                draw.DrawRectangle(background, null, new System.Windows.Rect(0, 0, size, size));
                draw.DrawImage(source, new System.Windows.Rect(0, 0, size, size));
            }
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var opaque = new FormatConvertedBitmap(bitmap, System.Windows.Media.PixelFormats.Bgr24, null, 0);
            var encoder = new BmpBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(opaque));
            using var stream = File.Create(path); encoder.Save(stream);
            return path;
        }
        public void Dispose()
        {
            try { if (_toolbar != null) _toolbar.Visible = false; }
            finally
            {
                _toolbar = null;
                _bitmapsDark = null;
                try
                {
                    if (_menu != null)
                    {
                        if ((bool)_menu.OnMenuBar) _menu.RemoveFromMenuBar();
                        // AutoCAD retains hidden CUI definitions until its native UI can release them.
                    }
                }
                finally { _menu = null; _workspace = null; }
            }
        }
    }
}
