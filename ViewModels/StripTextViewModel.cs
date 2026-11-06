// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Windows.Input;
using BotPlusAutocadTool.Infrastructure;
using BotPlusAutocadTool.Models;
using Microsoft.Win32;

namespace BotPlusAutocadTool.ViewModels
{
    public class StripTextViewModel : BaseViewModel
    {
        // Same key and flag letters as the StripMtext LISP routine, so both share the last choice.
        private const string RegistryKey = @"Software\StripMtext";
        private const string FlagsValue = "Settings";
        private const string RememberValue = "Save";

        private static readonly (char Flag, Func<StripTextOptions, bool> Get, Action<StripTextOptions, bool> Set)[] Flags =
        {
            ('A', o => o.Alignment, (o, v) => o.Alignment = v),
            ('B', o => o.Tabs, (o, v) => o.Tabs = v),
            ('C', o => o.Color, (o, v) => o.Color = v),
            ('D', o => o.Fields, (o, v) => o.Fields = v),
            ('F', o => o.Font, (o, v) => o.Font = v),
            ('H', o => o.Height, (o, v) => o.Height = v),
            ('L', o => o.LineFeed, (o, v) => o.LineFeed = v),
            ('M', o => o.BackgroundMask, (o, v) => o.BackgroundMask = v),
            ('N', o => o.Columns, (o, v) => o.Columns = v),
            ('O', o => o.Overline, (o, v) => o.Overline = v),
            ('P', o => o.Paragraph, (o, v) => o.Paragraph = v),
            ('Q', o => o.Oblique, (o, v) => o.Oblique = v),
            ('S', o => o.Stacking, (o, v) => o.Stacking = v),
            ('T', o => o.Tracking, (o, v) => o.Tracking = v),
            ('U', o => o.Underline, (o, v) => o.Underline = v),
            ('W', o => o.Width, (o, v) => o.Width = v),
            ('~', o => o.NonBreakingSpace, (o, v) => o.NonBreakingSpace = v),
        };

        private StripTextOptions _options;

        public StripTextViewModel()
        {
            _options = Load();
            SelectAllCommand = new RelayCommand(_ => SetAll(true));
            ClearAllCommand = new RelayCommand(_ => SetAll(false));
            OkCommand = new RelayCommand(_ => Close(true));
            CancelCommand = new RelayCommand(_ => Close(false));
        }

        public StripTextOptions Options
        {
            get => _options;
            set { _options = value; OnPropertyChanged(); }
        }

        public ICommand SelectAllCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public bool? DialogResult { get; private set; }
        public Action CloseAction { get; set; }

        private void SetAll(bool value)
        {
            foreach (var flag in Flags) flag.Set(Options, value);
            // The options model does not raise change notifications; rebinding re-reads every check box.
            var current = _options;
            Options = null;
            Options = current;
        }

        private void Close(bool accepted)
        {
            if (accepted && Options.RememberSettings) Save(Options);
            DialogResult = accepted;
            CloseAction?.Invoke();
        }

        private static StripTextOptions Load()
        {
            var options = new StripTextOptions();
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
                if (key == null) return options;
                options.RememberSettings = string.Equals(key.GetValue(RememberValue)?.ToString(), "1", StringComparison.Ordinal);
                if (options.RememberSettings && key.GetValue(FlagsValue) is string stored && stored.Length > 0)
                {
                    foreach (var flag in Flags) flag.Set(options, stored.IndexOf(flag.Flag) >= 0);
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is System.IO.IOException)
            {
                PluginLog.Error("Read StripMtext settings", ex);
            }
            return options;
        }

        private static void Save(StripTextOptions options)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKey);
                key.SetValue(RememberValue, options.RememberSettings ? "1" : "0");
                key.SetValue(FlagsValue, new string(Flags.Where(f => f.Get(options)).Select(f => f.Flag).ToArray()));
            }
            catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is System.IO.IOException)
            {
                PluginLog.Error("Save StripMtext settings", ex);
            }
        }
    }
}
