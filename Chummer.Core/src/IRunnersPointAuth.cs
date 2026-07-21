using System.Threading.Tasks;

namespace Chummer.Core
{
    /// <summary>
    ///     Credential provider required by the RunnersPoint HTTP transport.
    ///     Hosts remain responsible for the login UI and secure token storage.
    /// </summary>
    public interface IRunnersPointAuth
    {
        Task<string> GetAccessTokenAsync();
        Task<bool> TryForceRefreshAsync();
    }
}