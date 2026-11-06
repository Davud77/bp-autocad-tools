// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Globalization;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BotPlusAutocadTool.Infrastructure
{
    /// <summary>
    /// Culture-independent access to AutoCAD system variables.
    /// AutoCAD returns boxed <see cref="short"/>, <see cref="int"/> or <see cref="string"/> values;
    /// conversions here never depend on the regional settings of the current user.
    /// </summary>
    internal static class SystemVariables
    {
        public static int GetInt(string name) =>
            Convert.ToInt32(AcadApp.GetSystemVariable(name), CultureInfo.InvariantCulture);

        public static string GetString(string name) =>
            Convert.ToString(AcadApp.GetSystemVariable(name), CultureInfo.InvariantCulture) ?? string.Empty;

        /// <summary>COLORTHEME = 0 selects the dark AutoCAD theme. Core Console and errors fall back to dark.</summary>
        public static bool IsDarkTheme
        {
            get
            {
                try { return GetInt("COLORTHEME") == 0; }
                catch (Exception) { return true; }
            }
        }
    }
}
