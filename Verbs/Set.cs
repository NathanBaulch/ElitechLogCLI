using CommandLine;
using ElitechLog.Consts;
using ElitechLog.Models;

namespace ElitechLogCLI.Verbs;

public static class Set
{
    [Verb("set", HelpText = "Set parameters on the connected device")]
    public class Options
    {
        private TimeSpan? _delayTime;
        private TimeSpan? _interval;
        private TimeSpan? _intervalShortened;
        private string _travelDesc;
        private float? _lowerLimitTemp;
        private float? _upperLimitTemp;
        private float? _regulateTemp;
        private float? _lowerLimitHumi;
        private float? _upperLimitHumi;
        private float? _regulateHumi;

        [Option('k', "keep_listening", HelpText = "Continue listening for another device on disconnect")]
        public bool KeepListening { get; set; }

        [Option('y', "yes", HelpText = "Suppress confirmation prompt")]
        public bool Yes { get; set; }

        [Option("alarm_tone_beeps")] public byte? AlarmToneBeeps { get; set; }
        [Option("alarm_tone_interval")] public TimeSpan? AlarmToneInterval { get; set; }
        [Option("button_stop_allow")] public bool? ButtonStopAllow { get; set; }

        [Option("delay_time")]
        public TimeSpan? DelayTime
        {
            get => _delayTime;
            set
            {
                if (value != null && (value < TimeSpan.Zero || value >= TimeSpan.FromHours(16))) throw new ArgumentOutOfRangeException(null, "Must be between 0 and 16 hours exclusive");
                _delayTime = value;
            }
        }

        [Option("device_address")] public byte? DeviceAddress { get; set; }
        [Option("display_time")] public TimeSpan? DisplayTime { get; set; }

        [Option("interval")]
        public TimeSpan? Interval
        {
            get => _interval;
            set
            {
                if (value != null && (value.Value < TimeSpan.FromSeconds(10) || value.Value > TimeSpan.FromDays(1))) throw new ArgumentOutOfRangeException(null, "Must be between 10 seconds and 1 day");
                _interval = value;
            }
        }

        [Option("interval_shortened")]
        public TimeSpan? IntervalShortened
        {
            get => _intervalShortened;
            set
            {
                if (value != null && (value.Value < TimeSpan.Zero || value.Value > TimeSpan.FromMinutes(5))) throw new ArgumentOutOfRangeException(null, "Must be between 0 and 5 minutes");
                _intervalShortened = value;
            }
        }

        [Option("key_tone_allow")] public bool? KeyToneAllow { get; set; }

        [Option("storage_model")] public bool? StorageModel { get; set; }
        [Option("temp_unit")] public TempUnit? TempUnit { get; set; }

        [Option("travel_desc")]
        public string TravelDesc
        {
            get => _travelDesc;
            set
            {
                if (value?.Length > 100) throw new ArgumentException("Must be 100 characters at most");
                _travelDesc = value;
            }
        }

        [Option("lower_limit_temp")]
        public float? LowerLimitTemp
        {
            get => _lowerLimitTemp;
            set
            {
                if (value != null && (value.Value < -273 || value.Value > 1000)) throw new ArgumentOutOfRangeException(null, "Must be between -273 and 1000");
                _lowerLimitTemp = value;
            }
        }

        [Option("upper_limit_temp")]
        public float? UpperLimitTemp
        {
            get => _upperLimitTemp;
            set
            {
                if (value != null && (value.Value < -273 || value.Value > 1000)) throw new ArgumentOutOfRangeException(null, "Must be between -273 and 1000");
                _upperLimitTemp = value;
            }
        }

        [Option("regulate_temp")]
        public float? RegulateTemp
        {
            get => _regulateTemp;
            set
            {
                if (value != null && (value.Value < -10 || value.Value > 10)) throw new ArgumentOutOfRangeException(null, "Must be between -10 and 10");
                _regulateTemp = value;
            }
        }

        [Option("lower_limit_humi")]
        public float? LowerLimitHumi
        {
            get => _lowerLimitHumi;
            set
            {
                if (value != null && (value.Value < 0 || value.Value > 100)) throw new ArgumentOutOfRangeException(null, "Must be between 0 and 100");
                _lowerLimitHumi = value;
            }
        }

        [Option("upper_limit_humi")]
        public float? UpperLimitHumi
        {
            get => _upperLimitHumi;
            set
            {
                if (value != null && (value.Value < 0 || value.Value > 100)) throw new ArgumentOutOfRangeException(null, "Must be between 0 and 100");
                _upperLimitHumi = value;
            }
        }

        [Option("regulate_humi")]
        public float? RegulateHumi
        {
            get => _regulateHumi;
            set
            {
                if (value != null && (value.Value < -20 || value.Value > 20)) throw new ArgumentOutOfRangeException(null, "Must be between -20 and 20");
                _regulateHumi = value;
            }
        }
    }

    public enum TempUnit
    {
        C,
        F
    }

    public static int Run(Options opts)
    {
        var spinner = Interaction.StartSpinner();

        var monitor = new Monitor();
        monitor.Connected += parms =>
        {
            spinner.Stop();
            Interaction.DisplayDevice(parms);
            Apply(parms, opts);

            if (!Interaction.Confirm(opts.Yes, "set device parameters", parms.RecordsNumberActual))
            {
                monitor.Stop();
                return;
            }

            monitor.UpdateParameters();
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

    private static void Apply(Parameters parms, Options opts)
    {
        // TODO: improve support for USB parameters

        if (opts.StorageModel != null && !parms.StroageModelAvailable) throw new ArgumentException("Storage model not available");
        if (opts.IntervalShortened != null && !parms.IntervalShortenedAvailable) throw new ArgumentException("Interval shortened not available");
        if (opts.DisplayTime != null && !parms.DispalyTimeAvailable) throw new ArgumentException("Display time not available");
        if (opts.AlarmToneInterval != null && !parms.AlarmToneInervalAvailable) throw new ArgumentException("Alarm tone interval not available");
        if (opts.UpperLimitTemp != null && opts.UpperLimitTemp.Value - (opts.LowerLimitTemp ?? parms.LowerLimitTempValue[0]) < 0.5) throw new ArgumentException("Upper limit temp must be greater than lower limit temp");
        if (opts.LowerLimitTemp != null && (opts.UpperLimitTemp ?? parms.UpperLimitTempValue[0]) - opts.LowerLimitTemp.Value < 0.5) throw new ArgumentException("Lower limit temp must be less than upper limit temp");
        if (opts.UpperLimitTemp != null && opts.UpperLimitTemp.Value > parms.AlarmRangeMaxTemp) throw new ArgumentException("Upper limit temp must be less than alarm range max temp");
        if (opts.LowerLimitTemp != null && opts.LowerLimitTemp.Value < parms.AlarmRangeMinTemp) throw new ArgumentException("Lower limit temp must be greater than alarm range min temp");

        if (parms.Sensor2AlarmAviable)
        {
            if (opts.UpperLimitHumi != null && opts.UpperLimitHumi.Value - (opts.LowerLimitHumi ?? parms.LowerLimitHumiValue[0]) < 0.5) throw new ArgumentException("Upper limit humi must be greater than lower limit humi");
            if (opts.LowerLimitHumi != null && (opts.UpperLimitHumi ?? parms.UpperLimitHumiValue[0]) - opts.LowerLimitHumi.Value < 0.5) throw new ArgumentException("Lower limit humi must be less than upper limit humi");
            if (opts.UpperLimitHumi != null && opts.UpperLimitHumi.Value > parms.AlarmRangeMaxHumi) throw new ArgumentException("Upper limit humi must be less than alarm range max humi");
            if (opts.LowerLimitHumi != null && opts.LowerLimitHumi.Value < parms.AlarmRangeMinHumi) throw new ArgumentException("Lower limit humi must be greater than alarm range min humi");
        }
        else
        {
            if (opts.LowerLimitHumi != null) throw new ArgumentException("Lower limit humi not available");
            if (opts.UpperLimitHumi != null) throw new ArgumentException("Upper limit humi not available");
            if (opts.RegulateHumi != null) throw new ArgumentException("Regulate humi not available");
        }

        if (parms is COMParameters)
        {
            parms.SerialNum = string.Empty;
        }

        if (opts.TravelDesc != null) parms.TravelDesc = opts.TravelDesc.Trim();
        if (opts.TempUnit != null) parms.SetTempUnit(opts.TempUnit == TempUnit.C ? Public.TempUnitC : Public.TempUnitF);
        if (opts.Interval != null) parms.IntervalValue = (int)opts.Interval.Value.TotalSeconds;
        if (opts.DelayTime != null) parms.DelayTimeValue = opts.DelayTime.Value.Hours * 16 + (int)(opts.DelayTime.Value.Minutes / 30.0);
        if (opts.ButtonStopAllow != null) parms.ButtonStopAllowValue = opts.ButtonStopAllow.Value ? 19 : 49;
        if (opts.DeviceAddress != null) parms.NewDeviceAddress = opts.DeviceAddress.Value;
        if (opts.KeyToneAllow != null) parms.KeyToneAllowValue = parms is COMParameters ? opts.KeyToneAllow.Value ? 19 : 49 : opts.KeyToneAllow.Value ? 1 : 0;
        if (opts.AlarmToneBeeps != null) parms.AlarmToneAllowValue = opts.AlarmToneBeeps.Value;
        if (opts.AlarmToneInterval != null) parms.SetAlarmToneInerval((int)opts.AlarmToneInterval.Value.TotalMinutes + "M");
        if (opts.StorageModel != null) parms.StroageModelValue = opts.StorageModel.Value ? 19 : 49;

        if (opts.DisplayTime != null)
        {
            if (opts.DisplayTime.Value == TimeSpan.Zero)
            {
                parms.DisplayTimeValue = parms is COMParameters ? 0 : 15;
            }
            else
            {
                parms.SetDisplayTime((int)opts.DisplayTime.Value.TotalSeconds + "S");
            }
        }

        if (opts.IntervalShortened != null)
        {
            if (parms is COMParameters)
            {
                parms.IntervalShortenedValue = opts.IntervalShortened.Value != TimeSpan.Zero ? 19 : 49;
            }
            else if (opts.IntervalShortened.Value == TimeSpan.Zero)
            {
                parms.IntervalShortenedValue = 0;
            }
            else
            {
                parms.SetIntervalShortened((int)opts.DisplayTime.Value.TotalMinutes + "M");
            }
        }

        if (opts.UpperLimitTemp != null) parms.UpperLimitTempValue[0] = Public.TempValueFToC(opts.UpperLimitTemp.Value, parms.SensorTempUnitVAL);
        if (opts.LowerLimitTemp != null) parms.LowerLimitTempValue[0] = Public.TempValueFToC(opts.LowerLimitTemp.Value, parms.SensorTempUnitVAL);
        if (opts.RegulateTemp != null) parms.SetRegulateTValue(opts.RegulateTemp.Value);

        if (parms.Sensor2AlarmAviable)
        {
            if (opts.UpperLimitHumi != null) parms.UpperLimitHumiValue[0] = Public.TempValueFToC(opts.UpperLimitHumi.Value, parms.Sensor2HumiUnitVAL);
            if (opts.LowerLimitHumi != null) parms.LowerLimitHumiValue[0] = Public.TempValueFToC(opts.LowerLimitHumi.Value, parms.Sensor2HumiUnitVAL);
            if (opts.RegulateHumi != null) parms.SetRegulateTValue(opts.RegulateHumi.Value);
        }
    }
}