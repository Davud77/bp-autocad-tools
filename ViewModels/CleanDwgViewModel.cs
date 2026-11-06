// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Windows.Input;
using BotPlusAutocadTool.Infrastructure;
using BotPlusAutocadTool.Models;

namespace BotPlusAutocadTool.ViewModels
{
    public class CleanDwgViewModel : BaseViewModel
    {
        private const string SettingsName = "CleanDwg";
        private const string LegacySettingsKey = @"Software\CleanDwg";

        private CleanDwgOptions _options;

        public CleanDwgViewModel()
        {
            _options = RegistryOptions.Load<CleanDwgOptions>(SettingsName, LegacySettingsKey);
            ExecuteCommand = new RelayCommand(_ => Close(true));
            CancelCommand = new RelayCommand(_ => Close(false));
            SelectAllCommand = new RelayCommand(_ => SetAll(true));
            ClearAllCommand = new RelayCommand(_ => SetAll(false));
        }

        public CleanDwgOptions Options
        {
            get => _options;
            set { _options = value; OnPropertyChanged(); }
        }

        public ICommand ExecuteCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand ClearAllCommand { get; }

        public bool? DialogResult { get; private set; }
        public Action CloseAction { get; set; }

        private void Close(bool accepted)
        {
            if (accepted && Options.RememberSettings) RegistryOptions.Save(SettingsName, Options);
            DialogResult = accepted;
            CloseAction?.Invoke();
        }

        private void SetAll(bool value)
        {
            // Saving the drawing is an action, not a cleanup category.
            RegistryOptions.SetAll(Options, value, nameof(CleanDwgOptions.SaveDoc));
            RefreshOptions();
        }

        /// <summary>The options model does not raise change notifications; rebinding re-reads every check box.</summary>
        private void RefreshOptions()
        {
            var current = _options;
            Options = null;
            Options = current;
        }
    }
}
