using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Text.Json;
using System.Xml;
using CommandLine;
using CsvHelper;
using Dapper;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace ElitechLogCLI.Verbs;

public static class Export
{
    [Verb("export", HelpText = "Export readings in the specified format")]
    public class Options
    {
        private string _serialNumber;

        [Option('d', "db_file", HelpText = "Database file")]
        public string DatabaseFile { get; set; } = Database.DefaultFile;

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

        [Option('p', "period", HelpText = "Restrict to a specified time period, for example yesterday, 'last month', 'this year'")]
        public string Period { get; set; }

        [Option('f', "format", HelpText = "Output format, available options are CSV, YAML, JSON, XML", Default = Format.CSV)]
        public Format Format { get; set; }
    }

    public enum Format
    {
        CSV,
        YAML,
        JSON,
        XML
    }

    public static int Run(Options opts)
    {
        if (!File.Exists(opts.DatabaseFile)) throw new ApplicationException("Database file not found");

        Chronic.Span period = null;
        if (opts.Period != null)
        {
            period = new Chronic.Parser().Parse(opts.Period);
            if (period == null) throw new ApplicationException("Could not parse period");
        }

        using var con = new SQLiteConnection($"Data Source={opts.DatabaseFile}");
        con.Open();

        var filters = new List<string>(3);
        if (!string.IsNullOrEmpty(opts.SerialNumber))
        {
            filters.Add("serial_number = @SerialNumber");
        }

        if (period != null)
        {
            if (period.Start != null)
            {
                filters.Add("timestamp >= @Start");
            }

            if (period.End != null)
            {
                filters.Add("timestamp < @End");
            }
        }

        var where = filters.Count > 0 ? "where " + string.Join(" and ", filters) : null;
        using var reader = con.ExecuteReader(
            $"select * from reading {where} order by serial_number,timestamp",
            new
            {
                opts.SerialNumber,
                Start = Utils.ToTimestamp(period?.Start),
                End = Utils.ToTimestamp(period?.End)
            });

        var names = reader.GetSchemaTable().Rows.Cast<DataRow>().Select(row => (string)row["ColumnName"]).ToList();

        switch (opts.Format)
        {
            case Format.CSV:
                Csv(reader, names);
                break;
            case Format.YAML:
                Yaml(reader, names);
                break;
            case Format.JSON:
                Json(reader, names);
                break;
            case Format.XML:
                Xml(reader, names);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return 0;
    }

    private static void Csv(IDataReader reader, ICollection<string> names)
    {
        using var csv = new CsvWriter(Console.Out, CultureInfo.InvariantCulture, true);

        foreach (var name in names)
        {
            csv.WriteField(name);
        }

        csv.NextRecord();

        var values = new object[names.Count];
        while (reader.Read())
        {
            reader.GetValues(values);
            foreach (var value in values)
            {
                csv.WriteField(value);
            }

            csv.NextRecord();
        }
    }

    private static void Yaml(IDataReader reader, IList<string> names)
    {
        var values = new object[names.Count];

        var yaml = new Emitter(Console.Out);
        yaml.Emit(new StreamStart());
        yaml.Emit(new DocumentStart());
        yaml.Emit(new SequenceStart(null, null, false, SequenceStyle.Any));
        while (reader.Read())
        {
            yaml.Emit(new MappingStart());
            reader.GetValues(values);
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] is not DBNull)
                {
                    yaml.Emit(new Scalar(names[i]));
                    yaml.Emit(new Scalar(Convert.ToString(values[i])));
                }
            }

            yaml.Emit(new MappingEnd());
        }

        yaml.Emit(new SequenceEnd());
        yaml.Emit(new DocumentEnd(false));
        yaml.Emit(new StreamEnd());
    }

    private static void Json(IDataReader reader, IList<string> names)
    {
        var values = new object[names.Count];

        using var stdout = Console.OpenStandardOutput();
        using var json = new Utf8JsonWriter(stdout, new JsonWriterOptions { Indented = true });
        json.WriteStartArray();
        while (reader.Read())
        {
            json.WriteStartObject();
            reader.GetValues(values);
            for (var i = 0; i < values.Length; i++)
            {
                switch (values[i])
                {
                    case string val:
                        json.WriteString(names[i], val);
                        break;
                    case long val:
                        json.WriteNumber(names[i], val);
                        break;
                    case double val:
                        json.WriteNumber(names[i], val);
                        break;
                    case DateTime val:
                        json.WritePropertyName(names[i]);
                        json.WriteRawValue(JsonSerializer.Serialize(val));
                        break;
                }
            }

            json.WriteEndObject();
        }

        json.WriteEndArray();
    }

    private static void Xml(IDataReader reader, IList<string> names)
    {
        var values = new object[names.Count];

        using var xml = new XmlTextWriter(Console.Out);
        xml.Formatting = Formatting.Indented;
        xml.WriteStartDocument();
        xml.WriteStartElement("readings");
        while (reader.Read())
        {
            xml.WriteStartElement("reading");
            reader.GetValues(values);
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] is not DBNull)
                {
                    xml.WriteElementString(names[i], Convert.ToString(values[i]));
                }
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndDocument();
    }
}