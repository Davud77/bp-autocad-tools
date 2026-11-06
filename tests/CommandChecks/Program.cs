// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 BotPlus. See LICENSE in the repository root.
using System.Text;
using System.Text.RegularExpressions;
using BotPlusAutocadTool.Services;

string root = args[0];
string scratch = Path.Combine(Path.GetTempPath(), "BotPlusAutocadTool-CommandChecks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
CommandAlias Row(CommandAliases aliases, string command) => aliases.Rows.Single(r => r.Command.English == command);
try
{
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var expected = CommandCatalog.All.SelectMany(c => new[] { c.English, c.Russian }).ToArray();
    var registered = Directory.GetFiles(Path.Combine(root, "Commands"), "*.cs")
        .SelectMany(p => Regex.Matches(File.ReadAllText(p), "CommandMethod\\(\"([^\"]+)\"([^\\n]*)").Cast<Match>()).ToArray();
    Check(expected.Length == 54 && expected.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 54, "Unique bilingual catalog");
    Check(expected.All(n => registered.Any(m => m.Groups[1].Value == n)) && registered.Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() == registered.Length, "Exact registrations; no obsolete names");
    foreach (var definition in CommandCatalog.All)
    {
        Check(Regex.IsMatch(definition.English, "^(AT[A-Z]+|BOTPLUS_CLEARTABLESTYLE)$") && Regex.IsMatch(definition.Russian, "^(АТ|БП)[А-ЯЁ]+$"), "One keyboard layout per name");
        var pair = registered.Where(m => m.Groups[1].Value == definition.English || m.Groups[1].Value == definition.Russian).ToArray();
        Check(pair[0].Groups[2].Value == pair[1].Groups[2].Value, "Same command flags for " + definition.English);
    }
    string text = "; Пользовательские настройки\r\nL, *LINE\r\n; keep this comment\r\nМК, *COPY\r\nSHELL, start, 1,,\r\n";
    foreach (var encoding in new Encoding[] { new UTF8Encoding(false, true), new UTF8Encoding(true, true),
        Encoding.GetEncoding(1251, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback), new UnicodeEncoding(false, true, true) })
    {
        string path = Path.Combine(scratch, "aliases-" + encoding.CodePage + "-" + encoding.GetPreamble().Length + ".pgp");
        byte[] before = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        File.WriteAllBytes(path, before);
        var aliases = new CommandAliases(path, 1251);
        Check(!aliases.Save() && File.ReadAllBytes(path).SequenceEqual(before), "No-op preserves bytes/encoding");
        Row(aliases, "ATMCOPY").Aliases = "мк";
        Check(aliases.Validate()?.Contains("занято") == true, "Case-insensitive PGP conflict");
        Row(aliases, "ATMCOPY").Aliases = "LINE";
        Check(aliases.Validate() != null, "Known command name conflict");
        Row(aliases, "ATMCOPY").Aliases = "SHELL";
        Check(aliases.Validate() != null, "External command alias preserved");
        Row(aliases, "ATMCOPY").Aliases = "AUTONUM";
        Check(aliases.Validate() != null, "Legacy plugin commands reserved");
        Row(aliases, "ATMCOPY").Aliases = "ATSTAMP";
        Check(aliases.Validate() != null, "Plugin command names reserved");
        Row(aliases, "ATMCOPY").Aliases = "КП, MC";
        Row(aliases, "ATSTAMP").Aliases = "кп";
        Check(aliases.Validate()?.Contains("несколько") == true, "Cross-row duplicate alias");
        Row(aliases, "ATSTAMP").Aliases = "ШТ";
        Check(aliases.Validate() == null && aliases.Save(), "Valid custom Cyrillic/Latin aliases");
        byte[] saved = File.ReadAllBytes(path);
        Check(File.ReadAllBytes(path + ".BotPlusAT.bak").SequenceEqual(before), "Original backup");
        Check(saved.Take(before.Length).SequenceEqual(before), "Outside section preserved byte for byte");
        var loaded = new CommandAliases(path, 1251);
        Check(Row(loaded, "ATMCOPY").Aliases == "КП, MC" && Row(loaded, "ATSTAMP").Aliases == "ШТ", "Alias round-trip");
        Check(!loaded.Save() && File.ReadAllBytes(path).SequenceEqual(saved), "Stable repeated save");
        Row(loaded, "ATMCOPY").Aliases = "";
        Row(loaded, "ATSTAMP").Aliases = "";
        Check(loaded.Save() && File.ReadAllBytes(path).SequenceEqual(before), "Remove only managed aliases");
        loaded = new CommandAliases(path, 1251);
        loaded.Suggest();
        Check(Row(loaded, "ATMCOPY").Aliases == "" && Row(loaded, "ATSTAMP").Aliases == "ШТ", "Suggestions skip occupied aliases");
        Row(loaded, "ATSTAMP").Aliases = "BAD;COMMAND";
        Check(loaded.Validate() != null, "Reject PGP injection");
        Row(loaded, "ATSTAMP").Aliases = "ШТ";
        File.AppendAllText(path, "; external edit", encoding);
        byte[] external = File.ReadAllBytes(path);
        bool refused = false;
        try { loaded.Save(); } catch (IOException) { refused = true; }
        Check(refused && File.ReadAllBytes(path).SequenceEqual(external), "Concurrent edits are not overwritten");
    }
    foreach (string malformed in new[] { "; BEGIN BOTPLUS AT ALIASES\n", "; END BOTPLUS AT ALIASES\n; BEGIN BOTPLUS AT ALIASES\n" })
    {
        string path = Path.Combine(scratch, "broken.pgp"); File.WriteAllText(path, malformed);
        bool refused = false; try { new CommandAliases(path); } catch (InvalidOperationException) { refused = true; }
        Check(refused, "Corrupt section rejected");
    }
    Console.WriteLine($"PASS: {checks} command catalog and PGP checks; bilingual names, encoding, conflicts, backups and concurrent edits.");
}
finally { Directory.Delete(scratch, true); }
