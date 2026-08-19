using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Chummer.Core
{
    /// <summary>Immutable in-memory character-file snapshot for undo/history UI. A snapshot owns
    /// serialized XML, so later mutations cannot alter the captured state.</summary>
    public sealed class CharacterSnapshot
    {
        internal CharacterSnapshot(string strLabel, string strDisplayName, string strXml)
        {
            Label = strLabel;
            DisplayName = strDisplayName;
            Xml = strXml;
            CapturedAtUtc = DateTime.UtcNow;
        }

        public string Label { get; }
        public string DisplayName { get; }
        public DateTime CapturedAtUtc { get; }
        internal string Xml { get; }
    }

    /// <summary>Platform-neutral bounded history for one open character. Hosts decide when to
    /// capture; restoring always returns a fresh document rather than mutating a live one.</summary>
    public sealed class CharacterHistory
    {
        private readonly List<CharacterSnapshot> _lstSnapshots = new List<CharacterSnapshot>();
        private readonly int _intCapacity;

        public CharacterHistory(int intCapacity = 20)
        {
            if (intCapacity < 1) throw new ArgumentOutOfRangeException(nameof(intCapacity));
            _intCapacity = intCapacity;
        }

        public IReadOnlyList<CharacterSnapshot> Snapshots => _lstSnapshots;

        public CharacterSnapshot Capture(CharacterDocument objCharacter, string strLabel = "")
        {
            if (objCharacter == null) throw new ArgumentNullException(nameof(objCharacter));
            var objSnapshot = new CharacterSnapshot(strLabel ?? string.Empty, objCharacter.DisplayName,
                objCharacter.Document.OuterXml);
            _lstSnapshots.Add(objSnapshot);
            if (_lstSnapshots.Count > _intCapacity)
                _lstSnapshots.RemoveAt(0);
            return objSnapshot;
        }

        public CharacterDocument Restore(CharacterSnapshot objSnapshot)
        {
            if (objSnapshot == null) throw new ArgumentNullException(nameof(objSnapshot));
            using (var objStream = new MemoryStream(Encoding.UTF8.GetBytes(objSnapshot.Xml)))
                return new CharacterFileService().Load(objStream, objSnapshot.DisplayName);
        }
    }
}
