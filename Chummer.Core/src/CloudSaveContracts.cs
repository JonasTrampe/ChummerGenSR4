namespace Chummer.Core
{
    public enum CloudSaveResult
    {
        Cancelled,
        Saved
    }

    public class CloudSaveOutcome
    {
        public CloudSaveResult Result { get; set; }
        public string LastPushedHash { get; set; } = "";
    }
}