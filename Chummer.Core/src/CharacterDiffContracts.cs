using System.Collections.Generic;

namespace Chummer.Core
{
    public class CharacterDiffEntry
    {
        public string Collection { get; set; } = "";
        public string Change { get; set; } = "";
        public string Name { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    public class CharacterDiffResult
    {
        public List<CharacterDiffEntry> Entries { get; } = new();
        public bool HasChanges => Entries.Count > 0;
    }
}