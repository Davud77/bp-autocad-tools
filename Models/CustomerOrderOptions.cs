// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
namespace BotPlusAutocadTool.Models
{
    public sealed class CustomerOrderOptions
    {
        public bool CreateNewFile { get; set; }
        public bool ExplodeBlocks { get; set; }
        public bool ExplodePolylines { get; set; }
        public bool ExplodeText { get; set; }
        public bool PurgeUnused { get; set; }
        public bool BlockCopy { get; set; }
    }
}
