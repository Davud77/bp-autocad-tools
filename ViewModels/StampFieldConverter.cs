// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Windows.Data;

namespace BotPlusAutocadTool.ViewModels
{
    /// <summary>
    /// Converter to bind TextBox to StampField by key via FieldsMap
    /// </summary>
    public class StampFieldConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value - это сам ViewModel (через {Binding .})
            if (value is StampPropsViewModel viewModel && parameter is string key)
            {
                if (viewModel.FieldsMap != null && viewModel.FieldsMap.TryGetValue(key, out var field))
                {
                    return field.Value ?? string.Empty;
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // OneWay binding в данном случае достаточно, так как обновление идет через 
            // UpdateSourceTrigger=PropertyChanged в самом Binding'е окна,
            // но для совместимости вернем value.
            return value;
        }
    }
}