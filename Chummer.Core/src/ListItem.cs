namespace Chummer.Core
{
    /// <summary>
    ///     Simple named value used by UI hosts when binding a list of domain choices.
    /// </summary>
    public class ListItem
    {
        public string Value { get; set; } = "";
        public string Name { get; set; } = "";
    }
}