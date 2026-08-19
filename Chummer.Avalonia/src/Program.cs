using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using UltralightNet;
using UltralightNet.AppCore;
using UltralightNet.Platform;

namespace Chummer.NewUI;

internal class Program
{
	private static TextWriterTraceListener? _characterFileLog;

	// Shared by every UltralightHtmlView (SheetPreviewDialog's sheet renderer). Ultralight's
	// engine is a process-wide singleton internally (thread pools, ICU data, font caches are set
	// up once) - creating a second Renderer after disposing the first breaks it ("no FileSystem
	// instance set" on the second CreateRenderer call, even though it was already configured), so
	// this is created exactly once and reused for the whole app's lifetime; only Views are
	// created/disposed per render.
	public static Renderer UltralightRenderer { get; private set; } = null!;
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
		if (ReExecWithWebKitGtkGbmWorkaroundOnLinux(args))
			return;

		InitializeLogging();
		InitializeUltralight();
		try
		{
			Trace.TraceInformation("Chummer Avalonia spike starting");
			BuildAvaloniaApp()
				.StartWithClassicDesktopLifetime(args);
		}
		finally
		{
			Trace.TraceInformation("Chummer Avalonia spike shutting down");
			Trace.Flush();
			if (_characterFileLog is not null)
			{
				Trace.Listeners.Remove(_characterFileLog);
				_characterFileLog.Dispose();
			}
		}
    }

	// SheetPreviewDialog's own native print button (Avalonia.Controls.WebView's NativeWebDialog)
	// opens a WebKitGTK view; on many Mesa/DRM driver combos its default DMA-BUF renderer fails
	// with "Failed to create GBM buffer" and the view stays blank instead of rendering. Disabling
	// it fixes this, but WebKitGTK's own WebProcess/NetworkProcess helpers are separate processes
	// it forks and execs itself - Environment.SetEnvironmentVariable from managed code updates
	// this process' environ, but WebKitGTK's process launcher was observed not to see that update
	// (it likely captures its spawn environment earlier than our fix runs). Re-execing this same
	// process with the variable set in the child's ProcessStartInfo guarantees every descendant
	// actually inherits it, since that's real OS-level process creation instead of an in-place
	// mutation. Returns true if a re-exec was launched (caller should return immediately without
	// running the rest of Main - this process's job is done once the replacement is started).
	private static bool ReExecWithWebKitGtkGbmWorkaroundOnLinux(string[] args)
	{
		if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("WEBKIT_DISABLE_DMABUF_RENDERER") != null)
			return false;

		string? strProcessPath = Environment.ProcessPath;
		if (string.IsNullOrEmpty(strProcessPath))
			return false;

		// Framework-dependent launches ("dotnet Chummer.Avalonia.dll") have ProcessPath pointing
		// at the dotnet muxer, with the actual assembly as GetCommandLineArgs()[0] - re-execing
		// the muxer alone (without that assembly path) would just print dotnet's own usage text.
		// Self-contained/AppImage launches have ProcessPath already pointing at the real
		// executable, matching GetCommandLineArgs()[0], so this branch is a harmless no-op there.
		string[] astrFullArgs = Environment.GetCommandLineArgs();
		var startInfo = new ProcessStartInfo(strProcessPath) { UseShellExecute = false };
		if (astrFullArgs.Length > 0 && !string.Equals(astrFullArgs[0], strProcessPath, StringComparison.Ordinal))
			startInfo.ArgumentList.Add(astrFullArgs[0]);
		foreach (string strArg in args)
			startInfo.ArgumentList.Add(strArg);
		startInfo.Environment["WEBKIT_DISABLE_DMABUF_RENDERER"] = "1";
		using var objChild = Process.Start(startInfo);
		objChild?.WaitForExit();
		return true;
	}

	// Required once before any Ultralight Renderer/View is created - without both a font loader
	// and a registered filesystem/CachePath, ulDefaultSession() aborts the whole process (a native
	// assertion failure, not a catchable .NET exception) the moment a Renderer's DefaultSession is
	// first touched.
	private static void InitializeUltralight()
	{
		AppCoreMethods.SetPlatformFontLoader();
		Platform.SetDefaultFileSystem = true;
		string strCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"ChummerGenSR4", "ultralight-cache");
		Directory.CreateDirectory(strCachePath);
		UltralightRenderer = Platform.CreateRenderer(new UlConfig { CachePath = strCachePath }, dispose: true);
	}

	private static void InitializeLogging()
	{
		var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChummerGenSR4", "logs");
		Directory.CreateDirectory(directory);
		_characterFileLog = new TextWriterTraceListener(Path.Combine(directory, "chummer-avalonia.log"));
		Trace.Listeners.Add(_characterFileLog);
		Trace.AutoFlush = true;
	}

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
