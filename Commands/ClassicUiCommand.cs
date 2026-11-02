// SPDX-License-Identifier: AGPL-3.0-only
using Autodesk.AutoCAD.Runtime;
namespace BotPlusAutocadTool.Commands
{
    public sealed class ClassicUiCommand
    {
        [CommandMethod("ATCLASSIC", CommandFlags.Modal)]
        [CommandMethod("АТМЕНЮ", CommandFlags.Modal)]
        [CommandMethod("BOTPLUS_CLASSIC", CommandFlags.Session)]
        public void Show() => App.ShowClassicUi();
    }
}
