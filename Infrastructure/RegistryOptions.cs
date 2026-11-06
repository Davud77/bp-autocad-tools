// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;

namespace BotPlusAutocadTool.Infrastructure
{
    /// <summary>
    /// Persists every public read/write <see cref="bool"/> property of a dialog options object
    /// as a DWORD (0/1) under HKCU. Missing or malformed values keep the defaults of a new instance.
    /// </summary>
    internal static class RegistryOptions
    {
        private const string Root = @"Software\BotPlus\BotPlusAutocadTool\";

        public static T Load<T>(string name, string legacyKey = null) where T : new()
        {
            var options = new T();
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Root + name)
                    ?? (legacyKey == null ? null : Registry.CurrentUser.OpenSubKey(legacyKey));
                if (key == null) return options;
                foreach (var property in Properties<T>())
                {
                    if (TryRead(key.GetValue(property.Name), out bool value))
                        property.SetValue(options, value);
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is System.IO.IOException)
            {
                PluginLog.Error("Read options " + name, ex);
            }
            return options;
        }

        public static void Save<T>(string name, T options)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(Root + name);
                foreach (var property in Properties<T>())
                    key.SetValue(property.Name, (bool)property.GetValue(options) ? 1 : 0, RegistryValueKind.DWord);
            }
            catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is System.IO.IOException)
            {
                PluginLog.Error("Save options " + name, ex);
            }
        }

        /// <summary>Sets every option flag except <c>RememberSettings</c> and the listed properties.</summary>
        public static void SetAll<T>(T options, bool value, params string[] except)
        {
            foreach (var property in Properties<T>())
            {
                if (property.Name == "RememberSettings" || Array.IndexOf(except, property.Name) >= 0) continue;
                property.SetValue(options, value);
            }
        }

        private static PropertyInfo[] Properties<T>() => Cache<T>.Properties;

        private static bool TryRead(object raw, out bool value)
        {
            value = false;
            switch (raw)
            {
                case int number: value = number != 0; return true;
                case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed):
                    value = parsed != 0; return true;
                default: return false;
            }
        }

        private static class Cache<T>
        {
            public static readonly PropertyInfo[] Properties = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(bool) && p.CanRead && p.CanWrite)
                .ToArray();
        }
    }
}
