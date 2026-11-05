using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.PlottingServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Services
{
    public static class PdfPlotterService
    {
        public const string PlotterName = "BotPlus PDF.pc3";

        // Resolve the active profile at runtime, including other languages, custom
        // printer search paths, new users and isolated Core Console profiles.
        public static string EnsureInstalled()
        {
            // Fast path: the same plugin version already installed the plotter for this AutoCAD release
            // and profile. No device enumeration, file reading or compression.
            string release = null, environment = null;
            try { release = AcadApp.Version.Major + "." + AcadApp.Version.Minor; environment = Environment(); }
            catch (Exception ex) { Infrastructure.PluginLog.Error("PDF plotter environment", ex); }
            var installs = SettingsService.Settings.PdfPlotterInstalls;
            if (release != null && installs.TryGetValue(release, out var stored) && Installed(stored, environment, out var installedPath))
                return installedPath;
            string result = Install(out var pmpPath, out var driver);
            if (release != null)
            {
                installs[release] = string.Join("\n", environment, result, pmpPath, driver);
                SettingsService.Save();
            }
            return result;
        }

        /// <summary>Everything that changes where AutoCAD looks for plotters. Paths come from AutoCAD on this PC.</summary>
        private static string Environment()
        {
            string version = typeof(PdfPlotterService).Assembly.GetName().Version?.ToString() ?? "";
            string roaming = Convert.ToString(AcadApp.GetSystemVariable("ROAMABLEROOTPREFIX")) ?? "";
            string searchPath = "";
            if (Infrastructure.HostInfo.IsDesktop)
            {
                try { dynamic host = AcadApp.AcadApplication; searchPath = (string)host.Preferences.Files.PrinterConfigPath ?? ""; }
                catch { searchPath = ""; }
            }
            return version + "|" + roaming + "|" + searchPath;
        }

        private static bool Installed(string stored, string environment, out string configPath)
        {
            configPath = null;
            var parts = (stored ?? "").Split('\n');
            if (parts.Length != 4 || parts[0] != environment) return false;
            // Moved profiles, deleted files or a reinstalled AutoCAD invalidate the record.
            if (!File.Exists(parts[1]) || !File.Exists(parts[2]) || !File.Exists(parts[3])) return false;
            configPath = parts[1];
            return true;
        }

        private static string Install(out string pmpPath, out string driver)
        {
            string plotters = GetPlotterFolder();
            plotters = plotters.Split(';').First(p => !string.IsNullOrWhiteSpace(p)).Trim();
            var assets = Path.Combine(Path.GetDirectoryName(typeof(PdfPlotterService).Assembly.Location), "Assets", "Plotters");
            pmpPath = Path.Combine(plotters, "PMP Files", "BotPlus PDF.pmp");
            Directory.CreateDirectory(Path.GetDirectoryName(pmpPath));
            var pmp = ReadConfiguration(Path.Combine(assets, "PMP Files", "BotPlus PDF.pmp"));
            bool changed = WriteIfChanged(pmpPath, pmp);
            var template = ReadConfiguration(Path.Combine(assets, PlotterName));
            var drivers = Path.Combine(Path.GetDirectoryName(typeof(PlotConfigManager).Assembly.Location), "drv");
            driver = Directory.GetFiles(drivers, "pdfplot*.hdi").OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase).First();
            template = template.Replace("PMP Files\\BotPlus PDF.pmp", pmpPath)
                .Replace("driver_pathname=\"pdfplot.hdi", "driver_pathname=\"" + driver);
            var configPath = Path.Combine(plotters, PlotterName);
            changed |= WriteIfChanged(configPath, template);
            if (changed) PlotConfigManager.RefreshList(RefreshCode.All);
            return configPath;
        }

        private static string GetPlotterFolder()
        {
            // Read the native configuration search results, rather than guessing
            // from the UI language or the roaming folder (which differs in Core Console).
            var configurations = PlotConfigManager.Devices.Cast<PlotConfigInfo>()
                .Where(c => !string.IsNullOrWhiteSpace(c.FullPath) &&
                    string.Equals(Path.GetExtension(c.FullPath), ".pc3", StringComparison.OrdinalIgnoreCase)).ToList();
            var reference = configurations.FirstOrDefault(c =>
                c.DeviceName.Equals("DWG To PDF.pc3", StringComparison.OrdinalIgnoreCase)) ?? configurations.FirstOrDefault();
            return reference != null ? Path.GetDirectoryName(reference.FullPath) :
                Path.Combine((string)AcadApp.GetSystemVariable("ROAMABLEROOTPREFIX"), "Plotters");
        }

        // Compares the unpacked text: compression runs only when the file really has to be written.
        private static bool WriteIfChanged(string path, string text)
        {
            if (File.Exists(path))
            {
                try { if (ReadConfiguration(path) == text.TrimEnd('\0')) return false; }
                catch (Exception) { /* Damaged or foreign file: overwrite it. */ }
            }
            Infrastructure.AtomicFile.WriteAllBytes(path, CompressConfiguration(text));
            return true;
        }

        private static string ReadConfiguration(string path)
        {
            using var input = File.OpenRead(path);
            input.Position = 60;
            using var codec = new ZLibStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(codec, Encoding.ASCII);
            return reader.ReadToEnd().TrimEnd('\0');
        }

        private static byte[] CompressConfiguration(string text)
        {
            var payload = Encoding.UTF8.GetBytes(text + '\0');
            using var compressed = new MemoryStream();
            using (var codec = new ZLibStream(compressed, CompressionLevel.SmallestSize, true)) codec.Write(payload);
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            writer.Write(Encoding.ASCII.GetBytes("PIAFILEVERSION_2.0,PC3VER1,compress\r\npmzlibcodec"));
            uint a = 1, b = 0;
            foreach (byte value in compressed.ToArray()) { a = (a + value) % 65521; b = (b + a) % 65521; }
            writer.Write((b << 16) | a);
            writer.Write(payload.Length);
            writer.Write((int)compressed.Length);
            writer.Write(compressed.ToArray());
            return output.ToArray();
        }
    }
}
