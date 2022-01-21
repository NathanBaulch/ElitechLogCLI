using System.Globalization;
using ElitechLog.Consts;
using ElitechLog.Models;
using Humanizer;
using Humanizer.Localisation;
using Kurukuru;

namespace ElitechLogCLI;

public static class Interaction
{
    public static Spinner StartSpinner()
    {
        var spinner = new Spinner("Waiting for device...", Patterns.GrowVertical);
        if (!Console.IsOutputRedirected)
        {
            spinner.Start();
        }

        return spinner;
    }

    public static void DisplayDevice(Parameters parms)
    {
        Console.Write($"Device: {parms.TravelDesc}, serial number: {parms.SerialNum}");

        if (!string.IsNullOrEmpty(parms.DeviceStateDesc))
        {
            Console.Write($", state: {parms.DeviceStateDesc.TrimEnd('.', ' ')}");
        }

        if (!string.IsNullOrEmpty(parms.BatteryDesc))
        {
            Console.Write($", battery: {parms.BatteryDesc}");
        }

        if (parms.DevicerCapacityMax > 0)
        {
            Console.Write($", storage: {(double)parms.RecordsNumberActual / parms.DevicerCapacityMax:P1}");
            if (DateTime.TryParseExact(parms.expectStopTime, Default.DateTimeFormat, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None, out var stopTime))
            {
                Console.Write($" ({(stopTime - DateTime.Now).Humanize(maxUnit: TimeUnit.Year)})");
            }
        }

        Console.WriteLine();
    }

    public static bool Confirm(bool yes, string action, int recordCount)
    {
        if (yes) return true;
        Console.WriteLine($"Are you sure you want to {action}? The device will be stopped and {recordCount:N0} reading(s) deleted. Enter [y]es to confirm.");
        if (Console.ReadKey(true).KeyChar is not (not 'y' or 'Y')) return true;
        Console.WriteLine("Aborted");
        return false;
    }
}