// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using BotPlusAutocadTool.Models;

namespace BotPlusAutocadTool.Services
{
    /// <summary>
    /// Service interface for AutoCAD database operations
    /// </summary>
    public interface IAutoCADService
    {
        StampProperties LoadStampProperties();
        void SaveStampProperties(StampProperties properties);
    }
}

