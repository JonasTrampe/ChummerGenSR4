using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmUpdate.cs: shown when <see cref="UpdateChecker"/> finds a newer
/// release. Opens the GitHub release page in the user's default browser rather than attempting to
/// download/self-replace the running executable in place - the old per-file Windows-only
/// replacement for that doesn't translate to how Linux/AppImage packaging delivers updates, same
/// reasoning legacy's own already-adapted frmUpdate.cs uses.</summary>
public partial class UpdateDialog : Window
{
    private readonly string _strReleaseUrl;

    public UpdateDialog(string strCurrentVersion, UpdateChecker.UpdateCheckResult objResult)
    {
        InitializeComponent();
        _strReleaseUrl = objResult.ReleaseUrl;
        CurrentVersionText.Text = "Installierte Version: " + strCurrentVersion;
        LatestVersionText.Text = "Neueste Version: " + objResult.LatestVersion;
        ReleaseNotesText.Text = objResult.ReleaseNotes;
    }

    private void OnDownload(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_strReleaseUrl))
            return;

        Process.Start(new ProcessStartInfo(_strReleaseUrl) { UseShellExecute = true });
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
