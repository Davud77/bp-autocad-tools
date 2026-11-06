// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.IO;
using System.Reflection;
using BotPlusAutocadTool.Infrastructure;
using Newtonsoft.Json;

namespace BotPlusAutocadTool.Services
{
    /// <summary>
    /// Сервис для сохранения настроек плагина
    /// </summary>
    public static class SettingsService
    {
        private static readonly string SettingsPath;
        private static PluginSettings _settings = new PluginSettings();

        static SettingsService()
        {
            // Directory creation and serializer loading must not poison this type.
            try
            {
                SettingsPath = Path.Combine(PluginPaths.UserDataDirectory, "settings.json");
                Load();
            }
            catch (Exception ex) { PluginLog.Error("Initialize settings", ex); }
        }

        public static PluginSettings Settings => _settings;

        private static void Load()
        {
            try
            {
                var legacyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "wm_settings.json");
                var loadPath = File.Exists(SettingsPath) ? SettingsPath : legacyPath;
                if (File.Exists(loadPath))
                {
                    var json = File.ReadAllText(loadPath);
                    _settings = JsonConvert.DeserializeObject<PluginSettings>(json) ?? new PluginSettings();
                    _settings.Normalize();
                    if ((Newtonsoft.Json.Linq.JObject.Parse(json).Value<int?>("DefaultsRevision") ?? 0) < 181)
                    {
                        _settings.FrameInsertionScale = 100; _settings.PlotScale = 100;
                        _settings.OpenFolderAfterPlot = true; _settings.PlotInBackground = false;
                        _settings.DefaultsRevision = 181; SaveCore();
                    }
                }
                else
                {
                    _settings = new PluginSettings();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error("Read settings", ex);
                _settings = new PluginSettings();
            }
        }

        public static void Save()
            => TrySave();

        public static bool TrySave()
        {
            try
            {
                if (SettingsPath == null) return false;
                SaveCore();
                return true;
            }
            catch (Exception ex) { PluginLog.Error("Save settings", ex); return false; }
        }

        private static void SaveCore()
        {
            _settings.Normalize();
            var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            AtomicFile.WriteAllText(SettingsPath, json);
        }
    }

    public class PluginSettings
    {
        public int DefaultsRevision { get; set; } = 181;
        public bool AutoInstallUpdates { get; set; } = false;
        public string PendingUpdateDirectory { get; set; }
        public bool EnableClassicUi { get; set; } = false;
        /// <summary>Selection length in the status bar. Off: no selection tracking at all.</summary>
        public bool SelectionLengthEnabled { get; set; } = true;
        /// <summary>Installed BotPlus PDF plotter per AutoCAD release ("25.1", "26.0"): plugin version and
        /// profile, then the .pc3, .pmp and driver paths found on this PC.</summary>
        public System.Collections.Generic.Dictionary<string, string> PdfPlotterInstalls { get; set; } = new();
        public bool AutoCheckUpdates { get; set; } = true;
        public bool IncludePrereleaseUpdates { get; set; } = false;
        public DateTime LastUpdateCheckUtc { get; set; }
        public string LastNotifiedUpdateTag { get; set; }
        // SumLength settings
        public int ScaleNumerator { get; set; } = 1;
        public int ScaleDenominator { get; set; } = 1;
        public int LengthUnit { get; set; } = 0; // 0=mm, 1=m, 2=km
        public int AreaUnit { get; set; } = 0;   // 0=mm², 1=m², 2=km²
        public double FrameInsertionScale { get; set; } = 100;
        public int PlotScale { get; set; } = 100;
        public bool PlotInBackground { get; set; } = false;
        public bool PlotTransparency { get; set; }
        public bool OpenFolderAfterPlot { get; set; } = true;
        public bool OpenFileAfterPlot { get; set; }
        public bool KeepExistingPdfFiles { get; set; }
        public string PlotOutputDirectory { get; set; } = "";

        internal void Normalize()
        {
            ScaleNumerator = Math.Max(1, ScaleNumerator);
            ScaleDenominator = Math.Max(1, ScaleDenominator);
            if (LengthUnit < 0 || LengthUnit > 2) LengthUnit = 0;
            if (AreaUnit < 0 || AreaUnit > 2) AreaUnit = 0;
            if (!double.IsFinite(FrameInsertionScale) || FrameInsertionScale <= 0 || FrameInsertionScale > 1000000) FrameInsertionScale = 100;
            if (PlotScale != 1 && PlotScale != 100) PlotScale = 100;
            PlotOutputDirectory ??= "";
            PdfPlotterInstalls ??= new();
        }
    }
}
