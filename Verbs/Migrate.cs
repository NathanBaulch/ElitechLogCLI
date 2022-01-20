using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using CommandLine;
using ElitechLog;
using ElitechLog.Consts;
using Microsoft.Win32;
using ShellProgressBar;

namespace ElitechLogCLI.Verbs;

public static class Migrate
{
    [Verb("migrate", HelpText = "Migrate data from the ElitechLogWin DB")]
    public class Options
    {
        private string _serialNumber;

        [Option('d', "db_file", HelpText = "Database file")]
        public string DatabaseFile { get; set; } = Database.DefaultFile;

        [Option("source_file", HelpText = "Path to the installed ElitechLogWin DB file, auto-detected if not specified")]
        public string SourceFile { get; set; }

        [Option('s', "serial_number", HelpText = "Restrict to a specific device")]
        public string SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (value != null && !value.All(char.IsLetterOrDigit)) throw new ArgumentException("Must be alphanumeric");
                _serialNumber = value;
            }
        }
    }

    public static int Run(Options opts)
    {
        if (string.IsNullOrEmpty(opts.SourceFile))
        {
            opts.SourceFile = GetSourceFile();
            Console.WriteLine("Source file: " + opts.SourceFile);
        }

        using var con = Database.Open(opts.DatabaseFile);

        var db = GetDb(opts.SourceFile);
        var data = db.GetAllStoreData(!string.IsNullOrEmpty(opts.SerialNumber) ? $"where data_name like '{opts.SerialNumber}\\_%' escape '\\'" : null);

        var inserted = 0;
        var skipped = 0;

        using (var pbar = new ProgressBar(data.Rows.Count, "", new ProgressBarOptions { ShowEstimatedDuration = true }))
        {
            Console.CancelKeyPress += (_, _) => pbar.Dispose();
            var timer = Stopwatch.StartNew();

            foreach (DataRow row in data.Rows)
            {
                pbar.Message = (string)row["data_name"];

                foreach (var parms in db.GetParametersByIds(new List<int>(1) { (int)(long)row["parameters_id"] }))
                {
                    var stored = Database.Store(con, parms);
                    inserted += stored;
                    skipped += parms.RecordDT.Rows.Count - stored;
                }

                pbar.Tick(pbar.CurrentTick + 1, TimeSpan.FromTicks((long)((double)timer.Elapsed.Ticks * pbar.MaxTicks / pbar.CurrentTick)));
            }

            pbar.Message = null;
        }

        Console.WriteLine($"Inserted {inserted:N0}, skipped {skipped:N0}");
        return 0;
    }

    private static string GetSourceFile()
    {
        var dbFilePath = Path.Combine(Environment.CurrentDirectory, "data.db3");
        var parts = new List<string>(3) { "SOFTWARE" };
        if (Environment.Is64BitOperatingSystem)
        {
            parts.Add("WOW6432Node");
        }

        parts.Add(Default.RootName);
        using var key = Registry.LocalMachine.OpenSubKey(string.Join("\\", parts));
        if (key?.GetValue("DBPath") is string dbPath)
        {
            dbFilePath = Path.Combine(dbPath, "data.db3");
        }

        if (!string.IsNullOrEmpty(dbFilePath) && !File.Exists(dbFilePath)) throw new ApplicationException("Source file not found");

        return dbFilePath;
    }

    private static Db GetDb(string dbFilePath)
    {
        var conn = new SQLiteConnection($"Data Source={dbFilePath}; Password={Db.Password};");
        conn.Open();
        typeof(Db).GetField("conn", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, conn);
        return (Db)FormatterServices.GetUninitializedObject(typeof(Db));
    }
}