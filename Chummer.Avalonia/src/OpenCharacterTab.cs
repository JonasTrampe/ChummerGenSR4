using System;
using System.Threading.Tasks;
using Chummer.Core;
using Chummer.NewUI.Controls;

namespace Chummer.NewUI;

/// <summary>Presentation identity for one independently open character document.</summary>
public sealed class OpenCharacterTab
{
    public CharacterDocument Character { get; }
    public string Title { get; }
    public string? SourcePath { get; private set; }
    public bool IsDirty { get; private set; }

    /// <summary>The populated per-character view shown as this tab's content.</summary>
    public CharacterTab Content { get; }
    public event Action<string>? BackupFailed;

    public OpenCharacterTab(CharacterDocument character, string? sourcePath = null)
    {
        Character = character;
        SourcePath = sourcePath;
        Title = string.IsNullOrEmpty(character.Name) ? character.DisplayName : character.Name;
        Content = new CharacterTab();
        Content.FinalizingCreation += CreateCareerBackupAsync;
        Content.LoadCharacter(character);
        character.Changed += OnCharacterChanged;
    }

    public void SetSavedPath(string? sourcePath)
    {
        SourcePath = sourcePath;
        IsDirty = false;
    }

    private void OnCharacterChanged() => IsDirty = true;

    private Task<bool> CreateCareerBackupAsync(CharacterDocument character)
    {
        try
        {
            new CharacterFileService().CreateCareerBackup(character, SourcePath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            BackupFailed?.Invoke(ex.Message);
            return Task.FromResult(false);
        }
    }
}
