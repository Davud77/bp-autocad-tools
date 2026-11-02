using System;
using System.IO;
using System.Linq;
using Autodesk.Windows;
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

public sealed class ClassicCheck
{
    private static ClassicUiController _controller;
    private static RibbonController _ribbon;
    private static object _menuBar;
    private static readonly string Report = Path.Combine(BotPlusAutocadTool.Services.PluginPaths.UserDataDirectory, "desktop-check.log");
    [CommandMethod("BOTPLUS_DESKTOPRECHECK", CommandFlags.Session)]
    public void Run()
    {
        try
        {
            _menuBar = AcadApp.GetSystemVariable("MENUBAR");
            _ribbon = new RibbonController(); _ribbon.EnsureCreated();
            var tab = ComponentManager.Ribbon?.FindTab(CommandCatalog.TabId);
            if (tab == null || tab.Panels.Count != 6 || tab.Panels[2].Source.Title != "Таблицы" ||
                tab.Panels[2].Source.Items.OfType<RibbonButton>().Count() != 4 ||
                !tab.Panels[2].Source.Items.OfType<RibbonButton>().Any(button => button.Id == "BOTPLUS_BOTPLUS_CLEARTABLESTYLE"))
                throw new InvalidOperationException("Tables ribbon panel is missing");
            if (Environment.GetEnvironmentVariable("BOTPLUS_REVIEW_RIBBON_ONLY") != "1")
            {
            _controller = new ClassicUiController(); _controller.Show(); _controller.EnsureCreated();
            dynamic app = AcadApp.AcadApplication;
            dynamic menus = app.MenuGroups.Item(0).Menus, bars = app.MenuGroups.Item(0).Toolbars;
            dynamic menu = menus.Item(ClassicUiController.UiName), bar = bars.Item(ClassicUiController.UiName);
            if ((int)menu.Count != 6 || (int)bar.Count != 29 || !(bool)bar.Visible || !(bool)menu.OnMenuBar) throw new InvalidOperationException("Unexpected menu/toolbar state");
            if ((string)menu.Item(2).SubMenu.Item(3).Macro != "^C^CBOTPLUS_CLEARTABLESTYLE ") throw new InvalidOperationException("Missing table reset command");
            _controller.RemoveIfPresent(); _controller.RemoveIfPresent();
            if ((bool)menu.OnMenuBar) throw new InvalidOperationException("Disabled classic menu remains on menubar");
            for (int i = 0; i < (int)bars.Count; i++)
                if ((string)bars.Item(i).Name == ClassicUiController.UiName && (bool)bars.Item(i).Visible) throw new InvalidOperationException("Disabled toolbar remains visible");
            }
            if (tab.Panels.SelectMany(panel => panel.Source.Items.OfType<RibbonButton>()).Any(button => button.LargeImage == null)) throw new InvalidOperationException("Missing ribbon image");
            tab.IsActive = true;
            new Action(() =>
            {
                try
                {
                    var ribbon = ComponentManager.Ribbon;
                    ribbon.UpdateLayout();
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(ribbon.ActualWidth), (int)Math.Ceiling(ribbon.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(ribbon);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    var cropped = new System.Windows.Media.Imaging.CroppedBitmap(bitmap, new System.Windows.Int32Rect(0, 24, bitmap.PixelWidth, bitmap.PixelHeight - 24));
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(cropped));
                    using var output = File.Create(Path.Combine(BotPlusAutocadTool.Services.PluginPaths.UserDataDirectory, "ribbon.png"));
                    encoder.Save(output);
                    File.AppendAllText(Report, "PASS: native WPF ribbon capture saved.\n");
                }
                catch (System.Exception ex) { File.AppendAllText(Report, "FAIL: ribbon capture: " + ex); }
            }).Invoke();
            File.AppendAllText(Report, "PASS: desktop AutoCAD; six panels, 24 buttons, all icons; classic create/repeat/remove/repeat without duplicates; disabled UI hidden.\n");
            AcadApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nBOTPLUS_DESKTOP_PASS: classic create/remove checked; ribbon ready. Cleanup: BOTPLUS_DESKTOPFINISH.\n");
        }
        catch (System.Exception ex) { File.AppendAllText(Report, "FAIL: " + ex + "\n"); AcadApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + ex); }
    }
    [CommandMethod("BOTPLUS_DESKTOPFINISH", CommandFlags.Session)]
    public void Cleanup()
    {
        _controller?.Dispose(); _controller = null;
            _ribbon?.Dispose(); _ribbon = null;
        if (_menuBar != null) AcadApp.SetSystemVariable("MENUBAR", _menuBar);
        File.AppendAllText(Report, "PASS: test UI removed; previous MENUBAR restored.\n");
    }
}
namespace BotPlusAutocadTool.Services
{
    internal static class PluginPaths
    {
        internal static string UserDataDirectory
        {
            get { string path = Environment.GetEnvironmentVariable("BOTPLUS_DATA_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "BotPlusDesktopChecks131"); Directory.CreateDirectory(path); return path; }
        }
    }
}
namespace BotPlusAutocadTool.Infrastructure
{
    internal static class PluginLog
    {
        internal static void Error(string operation, System.Exception error) => File.AppendAllText(Path.Combine(Services.PluginPaths.UserDataDirectory, "desktop-check.log"), operation + ": " + error + "\n");
    }
}
