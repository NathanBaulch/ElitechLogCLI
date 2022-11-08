using System.Data.SQLite;
using CommandLine;
using Dapper;

namespace ElitechLogCLI.Verbs;

public static class Chart
{
    [Verb("chart", HelpText = "Display device readings in a simple chart")]
    public class Options
    {
        private IEnumerable<string> _serialNumbers;
        private int _width = Console.WindowWidth;
        private int _height = 20;
        private int _valueIndex = 1;

        [Option('d', "db_file", HelpText = "Database file")]
        public string DatabaseFile { get; set; } = Database.DefaultFile;

        [Option('s', "serial_number", HelpText = "Restrict to a specific devices")]
        public IEnumerable<string> SerialNumbers
        {
            get => _serialNumbers;
            set
            {
                if (value.Any(v => !v.All(char.IsLetterOrDigit))) throw new ArgumentException("Must be alphanumeric");
                _serialNumbers = value;
            }
        }

        [Option('p', "period", HelpText = "Restrict to a specified time period, for example yesterday, 'last month', 'this year'")]
        public string Period { get; set; }

        [Option('i', "value_index", HelpText = "Value index to display, between 1 and 9", Default = 1)]
        public int ValueIndex
        {
            get => _valueIndex;
            set
            {
                if (value is < 1 or > 9) throw new ArgumentOutOfRangeException(null, "Must be between 1 and 9");
                _valueIndex = value;
            }
        }

        [Option('w', "width", HelpText = "(Default: max) Chart width in characters")]
        public int Width
        {
            get => _width;
            set
            {
                if (value is < 12 or > 1000) throw new ArgumentOutOfRangeException(null, "Must be between 12 and 1000");
                _width = value;
            }
        }

        [Option('h', "height", HelpText = "Chart height in characters", Default = 20)]
        public int Height
        {
            get => _height;
            set
            {
                if (value is < 2 or > 1000) throw new ArgumentOutOfRangeException(null, "Must be between 2 and 1000");
                _height = value;
            }
        }
    }

    public static int Run(Options opts)
    {
        if (!File.Exists(opts.DatabaseFile)) throw new ApplicationException("Database file not found");

        Chronic.Span period = null;
        var filters = new List<string>(3);
        if (opts.Period != null)
        {
            period = new Chronic.Parser().Parse(opts.Period);
            if (period == null) throw new ApplicationException("Could not parse period");

            if (period.Start != null)
            {
                filters.Add("timestamp >= @Start");
            }

            if (period.End != null)
            {
                filters.Add("timestamp < @End");
            }
        }

        using var con = new SQLiteConnection($"Data Source={opts.DatabaseFile}");
        con.Open();

        var parms = new
        {
            opts.SerialNumbers,
            Start = Utils.ToTimestamp(period?.Start),
            End = Utils.ToTimestamp(period?.End)
        };

        if (opts.SerialNumbers?.Any() ?? false)
        {
            filters.Add("serial_number in @SerialNumbers");
        }

        var sql = "from reading";
        if (filters.Count > 0)
        {
            sql += " where " + string.Join(" and ", filters);
        }

        var readings = con.Query<(string sn, long ts, double val)>($"select serial_number, timestamp, value{opts.ValueIndex} {sql} order by serial_number, timestamp", parms).ToList();
        if (readings.Count == 0)
        {
            Console.WriteLine("No readings found");
            return -1;
        }

        var devices = readings.GroupBy(reading => reading.sn).ToList();
        var (tsMin, tsMax, countMax) = devices.Aggregate(
            (tsMin: long.MaxValue, tsMax: long.MinValue, countMax: int.MinValue),
            (a, d) => (Math.Min(a.tsMin, d.First().ts), Math.Max(a.tsMax, d.Last().ts), Math.Max(a.countMax, d.Count())));
        var fromDate = Utils.FromTimestamp(tsMin);
        var toDate = Utils.FromTimestamp(tsMax);
        Console.WriteLine("Device(s) " + string.Join(", ", devices.Select((device, i) => $"{ColorString((AsciiChart.Sharp.AnsiColor)(i + 1))}{device.Key}{ColorString(AsciiChart.Sharp.AnsiColor.Default)}")) +
                          " over period " + (fromDate.Date == toDate.Date ? $"{fromDate:g} to {toDate:g}" : $"{fromDate:d} to {toDate:d}"));

        var (valMin, valMax) = readings.Aggregate((valMin: double.MaxValue, valMax: double.MinValue), (a, r) => (Math.Min(a.valMin, r.val), Math.Max(a.valMin, r.val)));
        var range = valMax - valMin;
        var lblFormat = "0.0";
        var lblWidth = 7 + (int)Math.Floor(Math.Log10(Math.Max(1, Math.Max(Math.Abs(valMin), Math.Abs(valMax))))) + (valMin < 0 ? 1 : 0);
        var height = opts.Height - 1;
        if (range < height)
        {
            var digits = (int)Math.Ceiling(Math.Log10(height / range)) - 1;
            if (digits > 0)
            {
                lblFormat += new string('0', digits);
                lblWidth += digits;
            }
        }

        var dataWidth = Math.Min(opts.Width - lblWidth, readings.Count);

        if (countMax < dataWidth)
        {
            // choose appropriate width by brute force counting number of reading blocks
            var minBlocks = 0;
            for (var width = countMax; width <= dataWidth; width++)
            {
                var blocks = devices.Sum(device =>
                {
                    var factor = width / (double)(tsMax - tsMin);
                    var lookup = device.ToLookup(r => (int)((r.ts - tsMin) * factor));
                    var flag = false;
                    var count = 0;
                    for (var i = 0; i < width; i++)
                    {
                        if (!lookup[i].Any())
                        {
                            flag = false;
                        }
                        else if (!flag)
                        {
                            flag = true;
                            count++;
                        }
                    }

                    return count;
                });
                if (minBlocks == 0)
                {
                    minBlocks = blocks;
                }
                else if (blocks > minBlocks)
                {
                    dataWidth = width - 1;
                    break;
                }
            }
        }

        var factor = dataWidth / (double)(tsMax - tsMin);
        var values = devices.Select(device =>
        {
            var lookup = device.ToLookup(r => (int)((r.ts - tsMin) * factor), r => r.val);
            return Enumerable.Range(0, dataWidth).Select(i => lookup[i].Any() ? lookup[i].Average() : double.NaN).ToList();
        });
        Console.WriteLine(AsciiChart.Sharp.AsciiChart.Plot(values, new AsciiChart.Sharp.Options
        {
            Height = height,
            AxisLabelFormat = lblFormat,
            SeriesColors = Enumerable.Range(1, devices.Count).Select(i => (AsciiChart.Sharp.AnsiColor)i).ToArray()
        }));

        return 0;
    }

    private static string ColorString(AsciiChart.Sharp.AnsiColor color)
    {
        if (color == AsciiChart.Sharp.AnsiColor.Default)
        {
            return "\x1b[0m";
        }

        if (color == AsciiChart.Sharp.AnsiColor.Black)
        {
            color = AsciiChart.Sharp.AnsiColor.Default;
        }

        if (color <= AsciiChart.Sharp.AnsiColor.Silver)
        {
            return $"\x1b[{30 + (byte)color}m";
        }

        if (color <= AsciiChart.Sharp.AnsiColor.White)
        {
            return ($"\x1b[{82 + (byte)color}m");
        }

        return ($"\x1b[38;5;{(byte)color}m");
    }
}