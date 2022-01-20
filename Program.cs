using System.Data.SQLite;
using System.Reflection;
using System.Text;
using CommandLine;
using CommandLine.Text;
using Dapper;
using ElitechLog;
using ElitechLogCLI.Verbs;
using NGettext;

namespace ElitechLogCLI;

public static class Program
{
    static Program()
    {
        Console.OutputEncoding = Encoding.UTF8;
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        const string langFile = @"locales\en.mo";
        if (File.Exists(langFile))
        {
            typeof(Db).Assembly.GetType("ElitechLog.T", false, false)
                ?.GetField("_Catalog", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, new Catalog(File.OpenRead(langFile)));
        }
    }

    public static int Main(string[] args)
    {
        var parser = new Parser(cfg =>
        {
            cfg.CaseInsensitiveEnumValues = true;
            cfg.AutoVersion = false;
        });
        var res = parser.ParseArguments<Info.Options, Pull.Options, Chart.Options, Set.Options, Migrate.Options, Export.Options, Reset.Options>(args);
        try
        {
            return res.MapResult<Info.Options, Pull.Options, Chart.Options, Set.Options, Migrate.Options, Export.Options, Reset.Options, int>(
                Info.Run, Pull.Run, Chart.Run, Set.Run, Migrate.Run, Export.Run, Reset.Run, _ =>
                {
                    Console.WriteLine(HelpText.AutoBuild(res, help =>
                    {
                        help.AdditionalNewLineAfterOption = false;
                        help.AutoVersion = false;
                        help.AutoHelp = false;
                        return HelpText.DefaultParsingErrorsHandler(res, help);
                    }, e => e, maxDisplayWidth: parser.Settings.MaximumDisplayWidth));
                    return -1;
                });
        }
        catch (SQLiteException ex)
        {
            var parts = ex.Message.Split(new[] { Environment.NewLine }, 2, StringSplitOptions.None);
            Console.WriteLine("ERROR: " + parts[0] == parts[1] ? parts[0] : string.Join(": ", parts));
            return ex.ErrorCode;
        }
        catch (ApplicationException ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            return ex.HResult;
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            return ex.HResult;
        }
    }
}