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
