using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>Immutable in-memory character-file snapshot for undo/history UI. A snapshot owns
    /// serialized XML, so later mutations cannot alter the captured state.</summary>
    public sealed class CharacterSnapshot
    {
        internal CharacterSnapshot(string strLabel, string strDisplayName, string strXml, DateTime datCapturedAt)
        {
            Label = strLabel;
            DisplayName = strDisplayName;
            Xml = strXml;
            CapturedAtUtc = datCapturedAt.Kind == DateTimeKind.Utc ? datCapturedAt : datCapturedAt.ToUniversalTime();
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
            var objSnapshotDocument = new XmlDocument();
            objSnapshotDocument.LoadXml(objCharacter.Document.OuterXml);
            XmlNode? objPersistedHistory = objSnapshotDocument.DocumentElement?.SelectSingleNode("history");
            if (objPersistedHistory?.ParentNode != null)
                objPersistedHistory.ParentNode.RemoveChild(objPersistedHistory);
            var objSnapshot = new CharacterSnapshot(strLabel ?? string.Empty, objCharacter.DisplayName,
                objSnapshotDocument.OuterXml, objCharacter.EffectiveLastDate);
            _lstSnapshots.Add(objSnapshot);
            if (_lstSnapshots.Count > _intCapacity)
                _lstSnapshots.RemoveAt(0);
            return objSnapshot;
        }

        internal void Save(XmlElement objParent)
        {
            XmlDocument objDocument = objParent.OwnerDocument!;
            XmlElement objHistory = objDocument.CreateElement("history");
            foreach (CharacterSnapshot objSnapshot in _lstSnapshots)
            {
                XmlElement objNode = objDocument.CreateElement("snapshot");
                objNode.SetAttribute("label", objSnapshot.Label);
                objNode.SetAttribute("displayname", objSnapshot.DisplayName);
                objNode.SetAttribute("capturedatutc", objSnapshot.CapturedAtUtc.ToString("O"));
                var objSnapshotDocument = new XmlDocument();
                objSnapshotDocument.LoadXml(objSnapshot.Xml);
                objNode.AppendChild(objDocument.ImportNode(objSnapshotDocument.DocumentElement!, true));
                objHistory.AppendChild(objNode);
            }
            objParent.AppendChild(objHistory);
        }

        internal static CharacterHistory Load(XmlNode objParent, int intCapacity = 20)
        {
            var objHistory = new CharacterHistory(intCapacity);
            XmlNodeList? objSnapshots = objParent.SelectNodes("snapshot");
            if (objSnapshots == null) return objHistory;
            foreach (XmlNode objNode in objSnapshots)
            {
                XmlNode? objCharacter = objNode.SelectSingleNode("character");
                if (objCharacter == null) continue;
                DateTime.TryParse(objNode.Attributes?["capturedatutc"]?.Value, out DateTime datCaptured);
                objHistory._lstSnapshots.Add(new CharacterSnapshot(
                    objNode.Attributes?["label"]?.Value ?? string.Empty,
                    objNode.Attributes?["displayname"]?.Value ?? string.Empty,
                    objCharacter.OuterXml, datCaptured == default ? DateTime.UtcNow : datCaptured));
            }
            while (objHistory._lstSnapshots.Count > intCapacity)
                objHistory._lstSnapshots.RemoveAt(0);
            return objHistory;
        }


        public CharacterDocument Restore(CharacterSnapshot objSnapshot)
        {
            if (objSnapshot == null) throw new ArgumentNullException(nameof(objSnapshot));
            using (var objStream = new MemoryStream(Encoding.UTF8.GetBytes(objSnapshot.Xml)))
                return new CharacterFileService().Load(objStream, objSnapshot.DisplayName);
        }
    }
}
