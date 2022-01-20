using System.Data;
using System.Data.SQLite;
using Dapper;
using ElitechLog.Models;
using YamlDotNet.Serialization.NamingConventions;

namespace ElitechLogCLI;

public static class Database
{
    public static readonly string DefaultFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "elitechlogcli.db");

    public static SQLiteConnection Open(string dbFile)
    {
        if (string.IsNullOrEmpty(dbFile))
        {
            dbFile = DefaultFile;
        }

        var con = new SQLiteConnection($"Data Source={dbFile}");
        con.Open();
        con.Execute(@"
            create table if not exists device (
                serial_number text,
                timestamp integer,
                data_name text,
                travel_number text,
                record_count text,
                read_count integer,
                max_value1 real,
                min_value1 real,
                max_value2 real,
                min_value2 real,
                sensor2_available integer,
                started_at datetime,
                warning integer,
                sensor2_type text,
                unique(data_name)
            )");
        con.Execute(@"
            create table if not exists reading (
                serial_number text,
                timestamp integer,
                unique(serial_number,timestamp)
            )");
        return con;
    }

    public static int Store(SQLiteConnection con, Parameters parms)
    {
        if (string.IsNullOrEmpty(parms.DataName))
        {
            parms.DataName = $"{parms.SerialNum ?? parms.ModelDesc}_{DateTime.Now:yyyyMMddHHmmss}";
        }

        if (string.IsNullOrEmpty(parms.TravelNum))
        {
            parms.TravelNum = string.Empty;
        }

        var dps = new DynamicParameters(parms);
        dps.AddDynamicParams(new
        {
            timestamp = Utils.ToTimestamp(DateTime.Now),
            read_count = parms.RecordDT.Rows.Count,
            sensor2_type = parms.Sensor2AlarmAviable ? parms.SensorTypeValue == 21 ? "Temp" : "Humi" : null
        });
        con.Execute(
            "insert or ignore into device (serial_number,timestamp,data_name,travel_number,record_count,read_count,max_value1,min_value1,max_value2,min_value2,sensor2_available,started_at,warning,sensor2_type) " +
            "values (@SerialNum,@timestamp,@DataName,@TravelNum,@RecordsNumberActual,@read_count,@MaxTemValue,@MinTemValue,@MaxHumiValue,@MinHumiValue,@Sensor2AlarmAviable,@LoggerStartTimeValue,@DeviceAlarmStatusValue,@sensor2_type)",
            dps);

        var existing = con.GetSchema("Columns").Rows.Cast<DataRow>()
            .Where(row => Equals(row["TABLE_NAME"], "reading"))
            .Select(row => row["COLUMN_NAME"])
            .ToHashSet();

        foreach (DataColumn col in parms.RecordDT.Columns)
        {
            var name = UnderscoredNamingConvention.Instance.Apply(col.ColumnName);
            if (existing.Contains(name)) continue;

            string type;
            switch (Type.GetTypeCode(col.DataType))
            {
                case TypeCode.Empty:
                case TypeCode.DBNull:
                    continue;
                case TypeCode.Object:
                    type = col.DataType == typeof(byte[]) || col.DataType == typeof(Guid) ? "blob" : "text";
                    break;
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    type = "integer";
                    break;
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    type = "real";
                    break;
                case TypeCode.DateTime:
                    type = "datetime";
                    break;
                case TypeCode.Char:
                case TypeCode.String:
                    type = "text";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            con.Execute($"alter table reading add column {name} {type}");
        }

        var cols = new[] { "serial_number", "timestamp" }.Concat(parms.RecordDT.Columns.Cast<DataColumn>().Select(col => col.ColumnName)).ToList();
        var sql = $"insert or ignore into reading ({string.Join(",", cols.Select(UnderscoredNamingConvention.Instance.Apply))}) values ({string.Join(",", cols.Select(name => "@" + name))})";
        var inserted = 0;

        foreach (var chunk in parms.RecordDT.Rows.Cast<DataRow>().Chunk(1000))
        {
            using var tx = con.BeginTransaction();
            inserted += con.Execute(sql, chunk.Select(row =>
            {
                dps = new DynamicParameters(new
                {
                    serial_number = parms.SerialNum,
                    timestamp = Utils.ToTimestamp((DateTime)row["Time"])
                });
                foreach (DataColumn col in parms.RecordDT.Columns)
                {
                    dps.Add(col.ColumnName, row[col] is DBNull ? null : row[col]);
                }

                return dps;
            }));
            tx.Commit();
        }

        if (inserted > 0)
        {
            con.Execute("vacuum");
        }

        return inserted;
    }
}