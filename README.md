ChummerGen
-------

"Chummer is a character generator for Shadowrun 4th Edition.
Not only can you create your character quickly and easily,
but you can also use Chummer during your character's shadowrunning career,
to accurately track your Karma, Nuyen, ammo, and everything else all in one place.
Chummer also includes support for a number of optional rules and house rules and even includes
support for critters and is useful for players and Game Masters alike!
It also supports four languages: English, French, German, and Japanese.

Chummer is continuously updated to address bugs and introduce new and interesting features suggested by the community.
If you have a bug to report or idea to suggest, please report them on the issue section by using the link in the menu."

This text is a copy (slightly modified) from [Chummergen](https://web.archive.org/web/20191219091138/http://chummergen.com/) (original site directs to spam, this links to web archive). 

## Linux / Rider builds

The legacy `Chummer` executable targets .NET Framework 4.8 and must be built with Mono on Linux. Use `scripts/build-mono.sh` (optionally passing `Release`) for that path; it restores the `net48` core target and builds the legacy project through `/usr/bin/msbuild`.

For Rider, open `Chummer.LegacyMono.sln` when working on the WinForms application. In Settings → Build, Execution, Deployment → Toolset and Build, set **Mono executable path** to `/usr/bin/mono` and set **MSBuild version** to the custom path `/usr/lib/mono/msbuild/Current/bin/MSBuild.dll`. This keeps the legacy Mono build separate from the dotnet toolset required by `Chummer.Avalonia`.
