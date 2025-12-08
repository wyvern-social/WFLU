using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Wyvern.i18n.Talyx
{
    public class Metadata
    {
        public string Locale { get; set; } = "en-US";
        public string PluralRules { get; set; } = "cardinal";
        public string Direction { get; set; } = "ltr";
    }

    public class Parser
    {
        public Metadata Metadata { get; private set; } = new();

        public Dictionary<string, Message> ParseFile(string path)
        {
            var result = new Dictionary<string, Message>();
            var lines = File.ReadAllLines(path);
            string? currentKey = null;
            bool inChoiceBlock = false;
            bool inMetaBlock = false;
            bool inPluralBlock = false;
            var choiceLines = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("@meta"))
                {
                    inMetaBlock = true;
                    continue;
                }

                if (inMetaBlock)
                {
                    if (line.StartsWith("}"))
                    {
                        inMetaBlock = false;
                        continue;
                    }

                    var metaMatch = Regex.Match(line, @"^(\w+)\s*=\s*""([^""]+)""");
                    if (metaMatch.Success)
                    {
                        var key = metaMatch.Groups[1].Value;
                        var value = metaMatch.Groups[2].Value;

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
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    continue;
                }

                var match = Regex.Match(line, @"^(.+?)\s*=\s*(.+)$");
                if (match.Success && !inChoiceBlock && !inPluralBlock)
                {
                    currentKey = match.Groups[1].Value.Trim();
                    var value = match.Groups[2].Value.Trim();

                    var inheritMatch = Regex.Match(currentKey, @"^(.+?)\s*:\s*(.+)$");
                    if (inheritMatch.Success)
                    {
                        var actualKey = inheritMatch.Groups[1].Value.Trim();
                        var parentKey = inheritMatch.Groups[2].Value.Trim();
                        currentKey = actualKey;

                        result[currentKey] = new Message
                        {
                            RawValue = value,
                            InheritsFrom = parentKey,
                            HasConditional = value.Contains("?"),
                        };
                        continue;
                    }

                    if (value.StartsWith("plural("))
                    {
                        inPluralBlock = true;
                        choiceLines.Clear();
                        continue;
                    }

                    if (value.EndsWith("->"))
                    {
                        inChoiceBlock = true;
                        choiceLines.Clear();
                        continue;
                    }

                    if (currentKey is not null)
                    {
                        result[currentKey] = new Message
                        {
                            RawValue = value,
                            HasConditional = value.Contains("?"),
                        };
                    }
                    continue;
                }

                if (inPluralBlock)
                {
                    if (line.StartsWith("}"))
                    {
                        inPluralBlock = false;
                        if (currentKey is not null)
                        {
                            var message = new Message
                            {
                                IsPlural = true,
                                Choices = new Dictionary<string, string>(),
                            };

                            foreach (var l in choiceLines)
                            {
                                var trimmed = l.Trim();
                                var pluralMatch = Regex.Match(trimmed, @"^(\w+):\s*""([^""]+)""$");
                                if (pluralMatch.Success)
                                {
                                    message.Choices[pluralMatch.Groups[1].Value] = pluralMatch
                                        .Groups[2]
                                        .Value;
                                }
                            }
                            result[currentKey] = message;
                        }
                        continue;
                    }
                    choiceLines.Add(line);
                }

                if (inChoiceBlock)
                {
                    if (line.StartsWith("}"))
                    {
                        inChoiceBlock = false;
                        if (currentKey is not null)
                        {
                            var message = new Message
                            {
                                IsChoice = true,
                                Choices = new Dictionary<string, string>(),
                            };

                            foreach (var l in choiceLines)
                            {
                                var trimmed = l.Trim();
                                var choiceMatch = Regex.Match(trimmed, @"^\[([^\]]+)\]\s*(.+)$");
                                if (choiceMatch.Success)
                                {
                                    message.Choices[choiceMatch.Groups[1].Value.Trim()] =
                                        choiceMatch.Groups[2].Value.Trim();
                                }
                                else if (trimmed.StartsWith("*[other]"))
                                {
                                    message.Choices["other"] = trimmed
                                        .Substring("*[other]".Length)
                                        .Trim();
                                }
                            }
                            result[currentKey] = message;
                        }
                        continue;
                    }
                    choiceLines.Add(line);
                }
            }

            return result;
        }
    }
}
