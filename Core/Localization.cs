using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Wyvern.i18n.Talyx
{
    public class Localization
    {
        private readonly Dictionary<string, Dictionary<string, Message>> _languages = new(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Dictionary<string, Metadata> _metadata = new(
            StringComparer.OrdinalIgnoreCase
        );
        private string _currentLanguage = "en-US";

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (!_languages.ContainsKey(value))
                    throw new ArgumentException($"Language '{value}' not loaded.");
                _currentLanguage = value;
            }
        }

        public void LoadFile(string language, string path)
        {
            var parser = new Parser();
            var messages = parser.ParseFile(path);

            if (!_languages.ContainsKey(language))
                _languages[language] = new Dictionary<string, Message>();

            _metadata[language] = parser.Metadata;

            foreach (var kv in messages)
                _languages[language][kv.Key] = kv.Value;
        }

        public string Get(string key, Dictionary<string, object>? variables = null)
        {
            if (!_languages.TryGetValue(_currentLanguage, out var dict))
                return $"!{key}!";

            if (!dict.TryGetValue(key, out var message))
                return $"!{key}!";

            string value = message.RawValue;

            if (message.InheritsFrom != null)
            {
                var parentValue = Get(message.InheritsFrom, variables);
                value = value.Replace("{parent}", parentValue);
            }

            if (message.IsPlural && variables != null && variables.ContainsKey("count"))
            {
                var count = Convert.ToInt32(variables["count"]);
                value =
                    (count == 1 && message.Choices.ContainsKey("one"))
                        ? message.Choices["one"]
                        : message.Choices.GetValueOrDefault("other") ?? "";
            }

            if (message.IsChoice && variables != null && variables.ContainsKey("count"))
            {
                var count = Convert.ToInt32(variables["count"]);
                value =
                    (count == 1 && message.Choices.ContainsKey("one"))
                        ? message.Choices["one"]
                        : message.Choices.GetValueOrDefault("other") ?? "";
            }

            if (message.HasConditional && variables != null)
            {
                value = EvaluateConditionals(value, variables);
            }

            if (variables != null)
            {
                foreach (var kv in variables)
                    value = value.Replace("{" + kv.Key + "}", kv.Value?.ToString() ?? "");
            }

            if (value.StartsWith("[") && value.EndsWith("]"))
            {
                value = value.Substring(1, value.Length - 2);
            }

            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                value = value.Substring(1, value.Length - 2);
            }

            return value;
        }

        private string EvaluateConditionals(string value, Dictionary<string, object> variables)
        {
            var ternaryPattern = @"\{(\w+)\s*==\s*(\d+)\s*\?\s*'([^']+)'\s*:\s*'([^']+)'\}";
            return Regex.Replace(
                value,
                ternaryPattern,
                match =>
                {
                    var varName = match.Groups[1].Value;
                    var compareValue = int.Parse(match.Groups[2].Value);
                    var trueValue = match.Groups[3].Value;
                    var falseValue = match.Groups[4].Value;

                    if (variables.TryGetValue(varName, out var varValue))
                    {
                        var intValue = Convert.ToInt32(varValue);
                        return intValue == compareValue ? trueValue : falseValue;
                    }
                    return match.Value;
                }
            );
        }

        public Metadata? GetMetadata(string? language = null)
        {
            language ??= _currentLanguage;
            return _metadata.GetValueOrDefault(language);
        }

        public IEnumerable<string> Keys(string? language = null)
        {
            language ??= _currentLanguage;
            if (!_languages.TryGetValue(language, out var dict))
                yield break;

            foreach (var key in dict.Keys)
                yield return key;
        }

        public IEnumerable<string> LoadedLanguages => _languages.Keys;

        public bool IsLanguageLoaded(string language) => _languages.ContainsKey(language);
    }
}
