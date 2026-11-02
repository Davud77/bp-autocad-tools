// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 OOXGN. See LICENSE in the repository root.
using System.Collections.Generic;
using BotPlusAutocadTool.Models;
namespace BotPlusAutocadTool.Services
{
    public interface IStampSignatureService
    {
        Dictionary<string, StampSignature> LoadSignatures();
        void SaveStamp(StampProperties properties, Dictionary<string, StampSignature> signatures);
    }
}
