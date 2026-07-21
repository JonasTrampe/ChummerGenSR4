namespace Chummer.Core
{
    public enum ClipboardContentType
    {
        None = 0,
        Gear = 1,
        Commlink = 2,
        OperatingSystem = 3,
        Cyberware = 4,
        Bioware = 5,
        Armor = 6,
        Weapon = 7,
        Vehicle = 8,
        Lifestyle = 9
    }

    public class SourcebookInfo
    {
        #region Properties

        public string Code { get; set; } = "";

        public string Path { get; set; } = "";

        public int Offset { get; set; }

        #endregion
    }
}