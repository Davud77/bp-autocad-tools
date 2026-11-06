// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System.Windows;
using BotPlusAutocadTool.ViewModels;

namespace BotPlusAutocadTool.Views
{
    public partial class SumLengthWindow : Window
    {
        public SumLengthWindow()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Отписываемся от событий при закрытии окна
            if (DataContext is SumLengthViewModel vm)
            {
                vm.Dispose();
            }
        }
    }
}
