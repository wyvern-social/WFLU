using System.Collections.Generic;

namespace Wyvern.i18n.Talyx
{
    public class Message
    {
        public string RawValue { get; set; } = "";
        public bool IsChoice { get; set; }
        public Dictionary<string, string> Choices { get; set; } = new();
        public bool IsPlural { get; set; }
        public string? InheritsFrom { get; set; }
        public bool HasConditional { get; set; }
    }
}
