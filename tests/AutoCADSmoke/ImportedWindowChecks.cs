using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using BotPlusAutocadTool.Models;
using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.Views;

internal static class ImportedWindowChecks
{
    internal static void Run()
    {
        foreach (var panel in BotPlusAutocadTool.UI.CommandCatalog.Panels)
        foreach (var button in panel.Buttons)
        {
            string macro = BotPlusAutocadTool.UI.ClassicUiController.Macro(button.Command);
            if (!macro.StartsWith("\u0003\u0003") || macro.Contains("^C")) throw new InvalidOperationException("Invalid ActiveX macro");
            foreach (int size in new[] {16,32})
            {
                var icon = new BitmapImage(new Uri(BotPlusAutocadTool.UI.ClassicUiController.GetBitmap(button.Icon,size)));
                if (icon.PixelWidth != size || BitConverter.ToUInt16(File.ReadAllBytes(BotPlusAutocadTool.UI.ClassicUiController.GetBitmap(button.Icon,size)),28) != 24) throw new InvalidOperationException("Invalid BMP");
                byte[] pixel = new byte[4]; icon.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);
                if (pixel[0] == 0 && pixel[1] == 0 && pixel[2] == 0) throw new InvalidOperationException("Black icon background");
            }
        }
        string directory = Path.Combine(PluginPaths.UserDataDirectory, "WindowChecks");
        Directory.CreateDirectory(directory);
        string pgp = Path.Combine(directory, "test.pgp");
        File.WriteAllText(pgp, "; isolated window fixture\nL, *LINE\n");
        Window[] windows = {
            new CommandAliasesWindow(new CommandAliases(pgp)),
            new NumberedCopyWindow(new[] { new NumberedCopySource("Текст", "П1-007 DN50", false) }, new NumberedCopyOptions()),
            new DrawingSymbolCatalogWindow(), new DrawingSymbolWindow(new DrawingSymbolOptions()),
            new SignatureWindow(new[] { new StampSignatureSlot("Signer1", "Разработал") }, "Signer1"), new AlignSkewWindow()
        };
        foreach (var window in windows)
        {
            try
            {
                var content = (FrameworkElement)window.Content;
                double width = double.IsNaN(window.Width) ? 800 : window.Width;
                double height = double.IsNaN(window.Height) ? 760 : window.Height;
                content.Measure(new Size(width, height));
                content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(directory, window.GetType().Name + ".png")); encoder.Save(file);
            }
            finally { window.Close(); }
        }
        Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nFEATURE_PASS: all six imported WPF windows, resources and rendered controls.\n");
    }
}
