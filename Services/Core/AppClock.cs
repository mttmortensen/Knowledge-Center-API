namespace Knowledge_Center_API.Services.Core
{
    /// <summary>
    /// The application's notion of "now".
    ///
    /// Every timestamp column in the schema is a bare TIMESTAMP (no offset), so a
    /// stored value only means something relative to a known zone. Using
    /// DateTime.Now made that zone "whatever the host happens to be set to" —
    /// MRTN-LAPPS runs Etc/UTC, so evening entries landed on the next calendar
    /// day and showed up one square early on the dashboard heatmaps.
    ///
    /// AppClock pins that zone to a configured one instead, so the API records
    /// and buckets days the same way no matter where it's deployed.
    /// </summary>
    public static class AppClock
    {
        // Mountain time — where the data actually gets entered. Overridable via
        // the "AppTimeZone" config key so a move doesn't need a code change.
        public const string DefaultTimeZoneId = "America/Denver";

        private static TimeZoneInfo _timeZone = Resolve(DefaultTimeZoneId);

        public static TimeZoneInfo TimeZone => _timeZone;

        /// <summary>
        /// Called once at startup. Falls back to the default zone if the
        /// configured id isn't installed on the host rather than failing to boot.
        /// </summary>
        public static void Configure(string? timeZoneId)
        {
            _timeZone = Resolve(string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId);
        }

        /// <summary>
        /// Current wall-clock time in the application's zone, with Kind
        /// Unspecified so it round-trips through a naive TIMESTAMP column and
        /// serializes without a misleading "Z" suffix.
        /// </summary>
        public static DateTime Now =>
            DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone),
                DateTimeKind.Unspecified);

        /// <summary>Today's calendar date in the application's zone.</summary>
        public static DateOnly Today => DateOnly.FromDateTime(Now);

        private static TimeZoneInfo Resolve(string timeZoneId)
        {
            return Find(timeZoneId)
                ?? (timeZoneId == DefaultTimeZoneId ? null : Find(DefaultTimeZoneId))
                ?? TimeZoneInfo.Utc;
        }

        // Linux knows zones by their IANA id and Windows by its own; .NET can
        // usually read either, but translate as a fallback so a Windows dev box
        // and the Linux host both resolve the same configured zone.
        private static TimeZoneInfo? Find(string timeZoneId)
        {
            if (TryFind(timeZoneId, out var zone))
                return zone;

            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId)
                && TryFind(windowsId, out zone))
                return zone;

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var ianaId)
                && TryFind(ianaId, out zone))
                return zone;

            return null;
        }

        private static bool TryFind(string timeZoneId, out TimeZoneInfo? zone)
        {
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return true;
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException || ex is InvalidTimeZoneException)
            {
                zone = null;
                return false;
            }
        }
    }
}
