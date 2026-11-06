// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Windows.Input;
using BotPlusAutocadTool.Infrastructure;
using BotPlusAutocadTool.Models;

namespace BotPlusAutocadTool.ViewModels
{
    public class SelectSimilarViewModel : BaseViewModel
    {
        private const string SettingsName = "SelectSimilar";
        private const string LegacySettingsKey = @"Software\SelectSimilar";

        private SelectSimilarOptions _options;

        public SelectSimilarViewModel()
        {
            _options = RegistryOptions.Load<SelectSimilarOptions>(SettingsName, LegacySettingsKey);
            ExecuteCommand = new RelayCommand(_ => Close(true));
            CancelCommand = new RelayCommand(_ => Close(false));
        }

        public SelectSimilarOptions Options
        {
            get => _options;
            set { _options = value; OnPropertyChanged(); }
        }

        public ICommand ExecuteCommand { get; }
        public ICommand CancelCommand { get; }

        public bool? DialogResult { get; private set; }
        public Action CloseAction { get; set; }

        private void Close(bool accepted)
        {
            if (accepted && Options.RememberSettings) RegistryOptions.Save(SettingsName, Options);
            DialogResult = accepted;
            CloseAction?.Invoke();
        }
    }
}
