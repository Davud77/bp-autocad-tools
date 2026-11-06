// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System.Collections.Generic;

namespace BotPlusAutocadTool.Models
{
    /// <summary>
    /// Container for all stamp properties
    /// </summary>
    public class StampProperties
    {
        public Dictionary<string, string> Properties { get; set; }

        public StampProperties()
        {
            Properties = new Dictionary<string, string>();
        }

        public string GetValue(string key)
        {
            return Properties.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value) ? value : " ";
        }

        public void SetValue(string key, string value)
        {
            Properties[key] = value;
        }
    }
}

