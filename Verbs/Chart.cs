using System.Data.SQLite;
using CommandLine;
using Dapper;

namespace ElitechLogCLI.Verbs;

public static class Chart
{
    [Verb("chart", HelpText = "Display device readings in a simple chart")]
    public class Options
    {
        private string _serialNumber;
        private int _width = Console.WindowWidth;
        private int _height = 20;
        private int _valueIndex = 1;

        [Option('d', "db_file", HelpText = "Database file")]
        public string DatabaseFile { get; set; } = Database.DefaultFile;

        [Option('s', "serial_number", HelpText = "Restrict to a specific device, required if multiple devices present")]
        public string SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (value != null && !value.All(char.IsLetterOrDigit)) throw new ArgumentException("Must be alphanumeric");
                _serialNumber = value;
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
        string periodFilter = null;
        if (opts.Period != null)
        {
            period = new Chronic.Parser().Parse(opts.Period);
            if (period == null) throw new ApplicationException("Could not parse period");

            periodFilter = (period.Start != null ? "timestamp >= @Start" : null) +
                           (period.Start != null && period.End != null ? " and " : null) +
                           (period.End != null ? "timestamp < @End" : null);
        }


        using var con = new SQLiteConnection($"Data Source={opts.DatabaseFile}");
        con.Open();

        var parms = new
        {
            opts.SerialNumber,
            Start = Utils.ToTimestamp(period?.Start),
            End = Utils.ToTimestamp(period?.End)
        };

        if (string.IsNullOrEmpty(opts.SerialNumber))
        {
            var where = periodFilter != null ? "where " + periodFilter : null;
            var nums = con.Query<string>($"select distinct serial_number from reading {where} order by serial_number", parms).ToList();
            switch (nums.Count)
            {
                case 0:
                    Console.WriteLine("No readings found");
                    return -1;
                case > 1:
                    Console.WriteLine("Please specify a serial number: " + string.Join(", ", nums));
                    return -1;
                default:
                    opts.SerialNumber = nums[0];
                    parms = new { opts.SerialNumber, parms.Start, parms.End };
                    break;
            }
        }

        // TODO: handle gaps in the data

        var sql = "from reading where serial_number = @SerialNumber";
        if (periodFilter != null)
        {
            sql += " and " + periodFilter;
        }

        var values = con.Query<double>($"select value{opts.ValueIndex} {sql} order by timestamp", parms).ToList();
        if (values.Count == 0)
        {
            Console.WriteLine("No readings found");
            return -1;
        }

        var (from, to) = con.QuerySingle<(long, long)>("select min(timestamp), max(timestamp) " + sql, parms);
        var fromDate = Utils.FromTimestamp(from);
        var toDate = Utils.FromTimestamp(to);
        Console.WriteLine($"Serial number {opts.SerialNumber}, period from " + (fromDate.Date == toDate.Date ? $"{fromDate:g} to {toDate:g}" : $"{fromDate:d} to {toDate:d}"));

        var max = values.Max();
        var min = values.Min();
        var range = max - min;
        var lblFormat = "0.0";
        var lblWidth = 6 + (int)Math.Floor(Math.Log10(Math.Max(1, Math.Max(Math.Abs(min), Math.Abs(max))))) + (min < 0 ? 1 : 0);
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

        var dataWidth = opts.Width - lblWidth;
        if (values.Count > dataWidth)
        {
            values = values.Chunk((int)Math.Ceiling(values.Count / (decimal)dataWidth))
                .Select(chunk => chunk.Average())
                .ToList();
        }

        Console.WriteLine(AsciiChart.Sharp.AsciiChart.Plot(values, new AsciiChart.Sharp.Options { Height = height, AxisLabelFormat = lblFormat }));
        return 0;
    }
}