using System.Collections;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using CommandLine;
using ElitechLog.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;
using YamlDotNet.Serialization.TypeInspectors;

namespace ElitechLogCLI.Verbs;

public static class Info
{
    [Verb("info", HelpText = "Display status information about the connected device")]
    public class Options
    {
        [Option('k', "keep_listening", HelpText = "Continue listening for another device on disconnect")]
        public bool KeepListening { get; set; }

        [Option('f', "format", HelpText = "Output format, available options are Text, YAML, JSON, XML", Default = Format.Text)]
        public Format Format { get; set; }
    }

    public enum Format
    {
        Text,
        YAML,
        JSON,
        XML
    }

    public static int Run(Options opts)
    {
        var spinner = Interaction.StartSpinner();

        var monitor = new Monitor();
        monitor.Connected += parms =>
        {
            spinner.Stop();

            switch (opts.Format)
            {
                case Format.Text:
                    Text(parms);
                    break;
                case Format.YAML:
                    Yaml(parms);
                    break;
                case Format.JSON:
                    Json(parms);
                    break;
                case Format.XML:
                    Xml(parms);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Console.WriteLine();

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

    private static void Text(Parameters parms)
    {
        new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .WithTypeInspector(i => new YamlFilteredTypeInspector(i))
            .WithTypeInspector(i => new YamlSortedTypeInspector(i))
            .WithEmissionPhaseObjectGraphVisitor(args => new YamlFilteredEmissionPhaseObjectGraphVisitor(args.InnerVisitor))
            .Build()
            .Serialize(Console.Out, parms);
    }

    private static void Yaml(Parameters parms)
    {
        new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .WithTypeInspector(i => new YamlFilteredTypeInspector(i))
            .Build()
            .Serialize(Console.Out, parms);
    }

    private static void Json(Parameters parms)
    {
        using var stream = Console.OpenStandardOutput();
        var opts = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Converters = { new JsonDataTableConverter() }
        };
        JsonSerializer.Serialize(stream, parms, opts);
    }

    private static void Xml(Parameters parms)
    {
        var overrides = new XmlAttributeOverrides();
        overrides.Add(typeof(Parameters), "RecordDT", new XmlAttributes { XmlIgnore = true });
        overrides.Add(typeof(Parameters), "TotalTimeDesc", new XmlAttributes { XmlIgnore = true });
        overrides.Add(typeof(COMParameters), "TotalTimeDesc", new XmlAttributes { XmlIgnore = true });
        overrides.Add(typeof(USBParameters), "TotalTimeDesc", new XmlAttributes { XmlIgnore = true });
        new XmlSerializer(parms.GetType(), overrides).Serialize(Console.Out, parms);
    }

    private class YamlFilteredTypeInspector : TypeInspectorSkeleton
    {
        private readonly ITypeInspector _inner;

        public YamlFilteredTypeInspector(ITypeInspector inner) => _inner = inner;

        public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container) =>
            _inner.GetProperties(type, container).Where(p => p.Type != typeof(DataTable) && p.Type != typeof(List<string>));
    }

    private class YamlSortedTypeInspector : TypeInspectorSkeleton
    {
        private readonly ITypeInspector _inner;

        public YamlSortedTypeInspector(ITypeInspector inner) => _inner = inner;

        public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container) => _inner.GetProperties(type, container).OrderBy(prop => prop.Name);
    }

    private class YamlFilteredEmissionPhaseObjectGraphVisitor : ChainedObjectGraphVisitor
    {
        public YamlFilteredEmissionPhaseObjectGraphVisitor(IObjectGraphVisitor<IEmitter> inner) : base(inner)
        {
        }

        public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context)
        {
            if (value.Value is IList list)
            {
                var itemType = value.Type.IsGenericType ? value.Type.GetGenericArguments()[0] : value.Type.GetElementType();
                if (itemType != null)
                {
                    var defValue = itemType.IsValueType ? Activator.CreateInstance(itemType) : null;
                    if (list.Cast<object>().All(item => Equals(item, defValue) || IsEmpty(item))) return false;
                }
            }
            else if (IsEmpty(value.Value))
            {
                return false;
            }

            return base.EnterMapping(key, value, context);
        }

        private static bool IsEmpty(object obj) => obj as string is "0" or "0.0" or "???" or "N/A" or "0D 0H 0M 0S" ||
                                                   Equals(obj, new DateTime(2000, 1, 1, 1, 1, 1));
    }

    private class JsonDataTableConverter : JsonConverter<DataTable>
    {
        public override DataTable Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => null;

        public override void Write(Utf8JsonWriter writer, DataTable value, JsonSerializerOptions options)
        {
        }
    }
}