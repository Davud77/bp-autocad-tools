// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System.Windows;
using BotPlusAutocadTool.ViewModels;

namespace BotPlusAutocadTool.Views
{
    /// <summary>
    /// Interaction logic for AutoNumSettingsWindow.xaml
    /// </summary>
    public partial class AutoNumSettingsWindow : Window
    {
        public AutoNumSettingsViewModel ViewModel { get; private set; }

        public AutoNumSettingsWindow(AutoNumSettingsViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;

            // Subscribe to DialogResult changes
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.DialogResult) && viewModel.DialogResult.HasValue)
                {
                    DialogResult = viewModel.DialogResult.Value;
                    Close();
                }
            };
        }
    }
}
