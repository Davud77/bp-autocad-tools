// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System.Collections.Generic;
using BotPlusAutocadTool.Models;
using BotPlusAutocadTool.Services;
namespace BotPlusAutocadTool.ViewModels
{
    public partial class StampPropsViewModel
    {
        public Dictionary<string, StampSignatureSlot> Signatures { get; } = CreateSlots();
        private static Dictionary<string, StampSignatureSlot> CreateSlots()
        {
            var slots = new Dictionary<string, StampSignatureSlot>();
            for (int i = 1; i <= 6; i++) slots.Add("Signer" + i, new StampSignatureSlot("Signer" + i, "Подписант — строка " + i));
            for (int i = 1; i <= 4; i++) slots.Add("Revision" + i, new StampSignatureSlot("Revision" + i, "Изменение " + i));
            return slots;
        }
        private void LoadSignatures()
        {
            var stored = (_autoCADService as IStampSignatureService)?.LoadSignatures();
            foreach (var slot in Signatures.Values)
                slot.Signature = stored != null && stored.TryGetValue(slot.Key, out var signature) ? signature : null;
        }
    }
}
