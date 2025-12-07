using System;
using System.Collections.Generic;

namespace WFLU
{
    public class Localization
    {
        private readonly Dictionary<string, Dictionary<string, Message>> _languages = new(
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

            if (message.IsChoice && variables != null && variables.ContainsKey("count"))
            {
                var count = Convert.ToInt32(variables["count"]);
                value =
                    (count == 1 && message.Choices.ContainsKey("one"))
                        ? message.Choices["one"]
                        : message.Choices.GetValueOrDefault("other") ?? "";
            }

            if (variables != null)
            {
                foreach (var kv in variables)
                    value = value.Replace("{" + kv.Key + "}", kv.Value?.ToString() ?? "");
            }

            return value;
        }

        public IEnumerable<string> Keys(string? language = null)
        {
            language ??= _currentLanguage;
            if (!_languages.TryGetValue(language, out var dict))
                yield break;

            foreach (var key in dict.Keys)
                yield return key;
        }
    }
}
