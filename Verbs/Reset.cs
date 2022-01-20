using CommandLine;

namespace ElitechLogCLI.Verbs;

public static class Reset
{
    [Verb("reset", HelpText = "Delete all readings on the connected device")]
    public class Options
    {
        [Option('k', "keep_listening", HelpText = "Continue listening for another device on disconnect")]
        public bool KeepListening { get; set; }

        [Option('y', "yes", HelpText = "Suppress confirmation prompt")]
        public bool Yes { get; set; }
    }

    public static int Run(Options opts)
    {
        var spinner = Interaction.StartSpinner();

        var monitor = new Monitor();
        monitor.Connected += parms =>
        {
            spinner.Stop();
            Interaction.DisplayDevice(parms);

            if (parms.DeviceWorkMode != 2) throw new ApplicationException("Device cannot be reset");

            if (!Interaction.Confirm(opts.Yes, "reset this device", parms.RecordsNumberActual))
            {
                monitor.Stop();
                return;
            }

            monitor.QuickReset();
        };
        monitor.Updated += () =>
        {
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
        return 0;
    }
}