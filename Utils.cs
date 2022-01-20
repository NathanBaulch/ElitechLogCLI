namespace ElitechLogCLI;

public static class Utils
{
    public static DateTime FromTimestamp(long ts) => new(ts * TimeSpan.TicksPerSecond);
    public static long ToTimestamp(DateTime? value) => value?.Ticks / TimeSpan.TicksPerSecond ?? 0;
}