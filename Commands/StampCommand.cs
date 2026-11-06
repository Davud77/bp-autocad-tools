// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Windows.Interop; // Необходимо для связи WPF окна с окном AutoCAD
using Autodesk.AutoCAD.Runtime;
using BotPlusAutocadTool.Services;
using BotPlusAutocadTool.ViewModels;
using BotPlusAutocadTool.Views;

// Псевдоним для Application
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Commands
{
    public class StampCommand
    {
        // Статическая ссылка для реализации Singleton (одно окно)
        private static StampWindow _stampWindow;

        [CommandMethod("ATSTAMP", CommandFlags.Session)]
        [CommandMethod("АТШТАМП", CommandFlags.Session)]
        // CommandFlags.Session важно для Modeless Window
        public void ShowStampWindow()
        {
            // Если окно уже открыто - активируем его и выходим
            if (_stampWindow != null && _stampWindow.IsLoaded)
            {
                _stampWindow.Activate();
                return;
            }

            // Инициализация сервисов и VM
            var autoCadService = new AutoCADService();
            var viewModel = new StampPropsViewModel(autoCadService);

            // Загрузка данных при старте
            viewModel.LoadValues();

            _stampWindow = new StampWindow(viewModel);

            // Окно принадлежит главному окну AutoCAD: сворачивается вместе с ним и остаётся поверх.
            if (AcadApp.MainWindow != null)
            {
                var helper = new WindowInteropHelper(_stampWindow);
                helper.Owner = AcadApp.MainWindow.Handle;
            }

            // Обработка закрытия, чтобы сбросить ссылку
            _stampWindow.Closed += (s, e) => _stampWindow = null;

            _stampWindow.Show();
        }
    }
}