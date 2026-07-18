using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Serilog;

namespace Chummer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            InitializeLogging();
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Application.ThreadException += OnThreadException;

            // CharacterOptions.Load() has no UI toolkit of its own to ask this with (shared between the
            // WinForms and Avalonia hosts via Chummer.Core) - wire up the same confirmation dialog it
            // used to show directly, so this host's behavior is unchanged.
            CharacterOptions.ConfirmUseDefaultSettingsFile = strFileName =>
                MessageBox.Show(
                    LanguageManager.Instance.GetString("Message_CharacterOptions_CannotLoadSetting").Replace("{0}", strFileName),
                    LanguageManager.Instance.GetString("MessageTitle_CharacterOptions_CannotLoadSetting"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
				LanguageManager.Instance.Load(GlobalOptions.Instance.Language, null);
				// Make sure the default language has been loaded before attempting to open the Main Form.
				if (LanguageManager.Instance.Loaded)
					Application.Run(new frmMain());
				else
					Application.Exit();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void InitializeLogging()
        {
            string strLogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChummerGenSR4", "logs");
            if (!Directory.Exists(strLogDirectory))
                Directory.CreateDirectory(strLogDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithProperty("Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString())
                .WriteTo.File(
                    Path.Combine(strLogDirectory, "chummer-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("Chummer starting up");
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception (IsTerminating={IsTerminating})", e.IsTerminating);
            if (e.IsTerminating)
                Log.CloseAndFlush();
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            // WinForms shows its own default crash dialog for this after the event handler returns -
            // logging here doesn't change that behavior, it just makes sure the exception survives
            // past the dialog into a file we can actually look at afterwards.
            Log.Error(e.Exception, "Unhandled UI thread exception");
        }
    }
}
