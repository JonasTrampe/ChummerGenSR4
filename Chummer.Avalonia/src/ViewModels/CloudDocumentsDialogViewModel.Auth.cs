using System.Threading.Tasks;

namespace Chummer.NewUI.ViewModels;

public sealed partial class CloudDocumentsDialogViewModel
{
    public async Task InitializeAsync()
    {
        if (_auth.HasStoredLogin() && !_auth.IsApiTokenLogin())
            UseApiToken = false;

        if (_auth.HasStoredLogin() && _auth.IsApiTokenLogin())
            ApiToken = "................................";

        UpdateConnectionState();
        if (IsLoggedIn)
            await RefreshAsync();
        else
            Status = T("String_Cloud_NotLoggedIn");
    }

    public async Task LoginWithOAuthAsync()
    {
        Status = T("String_Cloud_LoggingIn");
        await _auth.LoginAsync();
        UpdateConnectionState();
        await RefreshAsync();
    }

    public async Task UseApiTokenAsync()
    {
        if (ApiToken == "................................")
            return;

        _auth.SetApiToken(ApiToken);
        UpdateConnectionState();
        ApiToken = "................................";
        await RefreshAsync();
    }

    public void Logout()
    {
        _auth.Logout();
        _documents.Clear();
        VisibleDocuments.Clear();
        Folders.Clear();
        UpdateConnectionState();
        ApiToken = string.Empty;
        Status = T("String_Cloud_NotLoggedIn");
    }

    private void UpdateConnectionState()
    {
        ConnectionState = !_auth.HasStoredLogin()
            ? T("String_Cloud_ConnectionState_NotConnected")
            : _auth.IsApiTokenLogin() ? T("String_Cloud_ConnectionState_ApiToken") : T("String_Cloud_ConnectionState_OAuth");
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(TokenPresetState));
    }
}
