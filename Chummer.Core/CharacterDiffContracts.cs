using System.Collections.Generic;

namespace Chummer
{
	public class CharacterDiffEntry
	{
		public string Collection { get; set; }
		public string Change { get; set; }
		public string Name { get; set; }
		public string Detail { get; set; }
	}

	public class CharacterDiffResult
	{
		public List<CharacterDiffEntry> Entries { get; } = new List<CharacterDiffEntry>();
		public bool HasChanges => Entries.Count > 0;
	}
}
