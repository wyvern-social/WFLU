using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Wyvern.i18n.Talyx
{
    public class Parser
    {
        public Metadata Metadata { get; private set; } = new();

        public Dictionary<string, Message> ParseFile(string path)
        {
            var result = new Dictionary<string, Message>();
            var lines = File.ReadAllLines(path);

            string? currentNamespace = null;
            string? currentKey = null;

            bool inMetaBlock = false;
            bool inPluralBlock = false;
            bool inChoiceBlock = false;
            bool inArrayBlock = false;

            var blockLines = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("@meta"))
                {
                    inMetaBlock = true;
                    if (line.Contains("{") && line.Contains("}"))
                    {
                        ParseMetaBlock(line);
                        inMetaBlock = false;
                        continue;
                    }
                    continue;
                }
                if (inMetaBlock)
                {
                    ParseMetaBlock(line);
                    if (line.Contains("}"))
                    {
                        inMetaBlock = false;
                        continue;
                    }
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    var ns = line[1..^1].Trim();
                    currentNamespace = string.IsNullOrEmpty(ns) ? null : ns;
                    continue;
                }

                if (inPluralBlock || inChoiceBlock || inArrayBlock)
                {
                    blockLines.Add(line);
                    if (
                        (inPluralBlock && line.StartsWith("}"))
                        || (inChoiceBlock && line.StartsWith("}"))
                        || (inArrayBlock && line.EndsWith("]"))
                    )
                    {
                        string fullKey = BuildFullKey(currentNamespace, currentKey!);
                        if (inPluralBlock)
                        {
                            var msg = new Message
                            {
                                IsPlural = true,
                                Choices = new Dictionary<string, string>(),
                            };
                            foreach (var l in blockLines)
                            {
                                if (l.StartsWith("}") || l.StartsWith("plural"))
                                    continue;
                                var m = Regex.Match(l.Trim(), @"^(\w+):\s*""([^""]+)""$");
                                if (m.Success)
                                    msg.Choices[m.Groups[1].Value] = m.Groups[2].Value;
                            }
                            result[fullKey] = msg;
                            inPluralBlock = false;
                        }
                        else if (inChoiceBlock)
                        {
                            var msg = new Message
                            {
                                IsChoice = true,
                                Choices = new Dictionary<string, string>(),
                            };
                            foreach (var l in blockLines)
                            {
                                var m = Regex.Match(l.Trim(), @"^\[([^\]]+)\]\s*(.+)$");
                                if (m.Success)
                                    msg.Choices[m.Groups[1].Value.Trim()] = StripQuotes(m.Groups[2]
                                        .Value.Trim());
                                else if (l.Trim().StartsWith("*[other]"))
                                    msg.Choices["other"] = StripQuotes(l.Trim()
                                        .Substring("*[other]".Length)
                                        .Trim());
                            }
                            result[fullKey] = msg;
                            inChoiceBlock = false;
                        }
                        else if (inArrayBlock)
                        {
                            result[fullKey] = new Message
                            {
                                RawValue = string.Join(" ", blockLines),
                                HasConditional = false,
                            };
                            inArrayBlock = false;
                        }
                        blockLines.Clear();
                    }
                    continue;
                }

                var match = Regex.Match(line, @"^(.+?)\s*=\s*(.+)$");
                if (match.Success)
                {
                    currentKey = match.Groups[1].Value.Trim();
                    var value = match.Groups[2].Value.Trim();

                    var inheritMatch = Regex.Match(currentKey, @"^(.+?)\s*:\s*(.+)$");
                    string? parentKey = null;
                    if (inheritMatch.Success)
                    {
                        currentKey = inheritMatch.Groups[1].Value.Trim();
                        parentKey = inheritMatch.Groups[2].Value.Trim();
                    }

                    string fullKey = BuildFullKey(currentNamespace, currentKey);

                    if (value.StartsWith("plural"))
                    {
                        inPluralBlock = true;
                        blockLines.Clear();
                        blockLines.Add(value);
                        if (value.Contains("}"))
                        {
                            var msg = new Message
                            {
                                IsPlural = true,
                                Choices = new Dictionary<string, string>(),
                            };
                            foreach (var l in blockLines)
                            {
                                if (l.StartsWith("}") || l.StartsWith("plural"))
                                    continue;
                                var m = Regex.Match(l.Trim(), @"^(\w+):\s*""([^""]+)""$");
                                if (m.Success)
                                    msg.Choices[m.Groups[1].Value] = m.Groups[2].Value;
                            }
                            result[fullKey] = msg;
                            inPluralBlock = false;
                            blockLines.Clear();
                        }
                        continue;
                    }
                    if (value.StartsWith("->") || value.EndsWith("->"))
                    {
                        inChoiceBlock = true;
                        blockLines.Clear();
                        blockLines.Add(value);
                        if (value.Contains("}"))
                        {
                            var msg = new Message
                            {
                                IsChoice = true,
                                Choices = new Dictionary<string, string>(),
                            };
                            foreach (var l in blockLines)
                            {
                                var m = Regex.Match(l.Trim(), @"^\[([^\]]+)\]\s*(.+)$");
                                if (m.Success)
                                    msg.Choices[m.Groups[1].Value.Trim()] = StripQuotes(m.Groups[2]
                                        .Value.Trim());
                                else if (l.Trim().StartsWith("*[other]"))
                                    msg.Choices["other"] = StripQuotes(l.Trim()
                                        .Substring("*[other]".Length)
                                        .Trim());
                            }
                            result[fullKey] = msg;
                            inChoiceBlock = false;
                            blockLines.Clear();
                        }
                        continue;
                    }
                    if (value.StartsWith("["))
                    {
                        inArrayBlock = true;
                        blockLines.Clear();
                        blockLines.Add(value);
                        if (value.EndsWith("]"))
                        {
                            result[fullKey] = new Message
                            {
                                RawValue = string.Join(" ", blockLines),
                                HasConditional = false,
                            };
                            inArrayBlock = false;
                            blockLines.Clear();
                        }
                        continue;
                    }

                    result[fullKey] = new Message
                    {
                        RawValue = StripQuotes(value),
                        InheritsFrom = parentKey,
                        HasConditional = value.Contains("?"),
                    };
                }
            }

            return result;
        }

        private void ParseMetaBlock(string line)
        {
            var keyValueMatches = Regex.Matches(line, @"(\w+)\s*=\s*""([^""]+)""");
            foreach (Match match in keyValueMatches)
            {
                var key = match.Groups[1].Value;
                var value = match.Groups[2].Value;
                switch (key)
                {
                    case "locale":
                        Metadata.Locale = value;
                        break;
                    case "plural_rules":
                        Metadata.PluralRules = value;
                        break;
                    case "direction":
                        Metadata.Direction = value;
                        break;
                }
            }
        }

        private string StripQuotes(string value)
        {
            if (value.StartsWith("\"") && value.EndsWith("\""))
                return value.Substring(1, value.Length - 2);
            return value;
        }

        private string BuildFullKey(string? ns, string key) =>
            string.IsNullOrEmpty(ns) ? key : $"{ns}.{key}";
    }
}
