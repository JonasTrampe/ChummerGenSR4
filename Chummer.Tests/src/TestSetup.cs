using System.Runtime.CompilerServices;
using Chummer.Core;

namespace Chummer.Tests;

/// <summary>
/// Runs once before any test in this assembly. Several ported ViewModels/dialogs now resolve UI
/// strings via <see cref="LanguageManager"/> (e.g. "Alle"/"UI_All" for a picker's category
/// filter) - in the real app this is loaded by App.axaml.cs's Initialize() before any ViewModel
/// is constructed, but nothing does that for this test host, so any test that happened to reach
/// such a code path would throw a bare KeyNotFoundException. Loads the always-complete en-us.xml
/// base (not "de", which App.axaml.cs prefers - tests shouldn't depend on that fork-specific
/// default) so every catalog key resolves regardless of which ViewModel a test exercises.
/// </summary>
internal static class TestSetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        LanguageManager.Instance.Load("en-us");
    }
}
