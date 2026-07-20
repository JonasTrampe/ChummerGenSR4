using System;
using System.Diagnostics;
using System.IO;
using Avalonia;

namespace Chummer.AvaloniaSpike;

internal class Program
{
	private static TextWriterTraceListener? _characterFileLog;
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
		InitializeLogging();
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

	private static void InitializeLogging()
	{
		string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChummerGenSR4", "logs");
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
