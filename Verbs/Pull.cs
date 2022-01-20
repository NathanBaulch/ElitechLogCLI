using CommandLine;
using System.Diagnostics;
using ShellProgressBar;

namespace ElitechLogCLI.Verbs;

public static class Pull
{
    [Verb("pull", HelpText = "Pull the latest readings from the connected device")]
    public class Options
    {
        [Option('k', "keep_listening", HelpText = "Continue listening for another device on disconnect")]
        public bool KeepListening { get; set; }

        [Option('d', "db_file", HelpText = "Database file")]
        public string DatabaseFile { get; set; } = Database.DefaultFile;
    }

    public static int Run(Options opts)
    {
        var spinner = Interaction.StartSpinner();
        ProgressBar pbar = null;
        Stopwatch timer = null;

        var monitor = new Monitor();
        monitor.Connected += parms =>
        {
            spinner.Stop();
            Interaction.DisplayDevice(parms);

            if (parms.RecordsNumberActual == 0)
            {
                Console.WriteLine("No readings found");
                if (opts.KeepListening)
                {
                    Console.WriteLine("Device can be safely removed");
                }
                else
                {
                    monitor.Stop();
                }
            }
            else
            {
                pbar = new ProgressBar(parms.RecordsNumberActual, "");
                timer = Stopwatch.StartNew();
                monitor.Download();
            }
        };
        monitor.Downloading += (current, total) =>
        {
            if (pbar.MaxTicks != total)
            {
                pbar.MaxTicks = total;
            }

            pbar.Tick(current, TimeSpan.FromTicks((long)((double)timer.Elapsed.Ticks * pbar.MaxTicks / pbar.CurrentTick)));
        };
        monitor.Downloaded += parms =>
        {
            pbar.Dispose();

            using (var con = Database.Open(opts.DatabaseFile))
            {
                var inserted = Database.Store(con, parms);
                Console.WriteLine($"Inserted {inserted:N0}, skipped {parms.RecordDT.Rows.Count - inserted:N0}");
            }

            if (opts.KeepListening)
            {
                Console.WriteLine("Device can be safely removed");
            }
            else
            {
                monitor.Stop();
            }
        };
        if (opts.KeepListening)
        {
            monitor.Disconnected += () => spinner = Interaction.StartSpinner();
        }

        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            monitor.Stop();
        };

        monitor.Run();
        spinner.Stop();
        pbar?.Dispose();
        return 0;
    }
}