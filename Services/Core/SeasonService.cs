using Knowledge_Center_API.DataAccess;
using Knowledge_Center_API.Models.Seasons;
using Knowledge_Center_API.Services.Validation;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Knowledge_Center_API.Services.Core
{
    /*
     * Seasons: time-boxed focus periods over existing Domains. Validation
     * problems throw ArgumentException (400); asking for something the season's
     * state doesn't allow throws InvalidOperationException (409).
     */
    public class SeasonService
    {
        public static readonly HashSet<string> Statuses = new() { "Planned", "Active", "Ended" };
        public static readonly HashSet<string> Roles = new() { "Focus", "Passive" };

        private const int MaxIntentionLength = 2000;
        private const int MaxReflectionLength = 10_000;
        private const int MaxOverloadReasonLength = 2000;

        private readonly Database _database;

        public SeasonService(Database database)
        {
            _database = database;
        }

        // Result of SetDomains: Warning is set when nothing was saved because the
        // lineup needs a focus overload confirmation first.
        public sealed record SetDomainsResult(SeasonOverloadWarningDto? Warning);

        /* ===================== SEASONS ===================== */

        // === CREATE ===
        public int CreateSeason(SeasonCreateDto dto)
        {
            FieldValidator.ValidateRequiredString(dto.Name, "Name", 100);
            FieldValidator.ValidateOptionalString(dto.Intention, "Intention", MaxIntentionLength);
            ValidatePlannedRange(0, dto.PlannedStart, dto.PlannedEnd);

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Name", NpgsqlDbType.Varchar, 100) { Value = dto.Name.Trim() },
                new NpgsqlParameter("@Intention", NpgsqlDbType.Text) { Value = NullIfBlank(dto.Intention) },
                new NpgsqlParameter("@PlannedStart", NpgsqlDbType.Date) { Value = dto.PlannedStart },
                new NpgsqlParameter("@PlannedEnd", NpgsqlDbType.Date) { Value = dto.PlannedEnd },
                new NpgsqlParameter("@Now", NpgsqlDbType.Timestamp) { Value = AppClock.Now }
            };

            return _database.ExecuteScalar<int>(SeasonQueries.InsertSeason, parameters);
        }

        // === READ ===
        public List<Season> GetSeasons(string? status = null)
        {
            if (status != null)
                FieldValidator.ValidateEnumValue(status, "Status", Statuses);

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@Status", NpgsqlDbType.Varchar, 20) { Value = (object?)status ?? DBNull.Value }
            };

            return _database.ExecuteQuery(SeasonQueries.GetSeasons, parameters)
                .Select(ConvertDBRowToSeason)
                .ToList();
        }

        public Season? GetSeasonById(int seasonId)
        {
            FieldValidator.ValidateId(seasonId, "Season ID");

            var rows = _database.ExecuteQuery(SeasonQueries.GetSeasonById, SeasonIdParam(seasonId));
            return rows.Count == 0 ? null : ConvertDBRowToSeason(rows[0]);
        }

        public SeasonDetailsDto? GetSeasonDetails(int seasonId)
        {
            var season = GetSeasonById(seasonId);
            if (season == null) return null;

            var details = ToDetails(season);
            details.Domains = GetSeasonDomains(seasonId);
            // An ended season shows the stats it ended with; one ended before
            // snapshots existed falls back to working them out now.
            details.Stats = (season.Status == "Ended" ? GetStatsSnapshot(seasonId) : null)
                ?? GetStats(details, details.EndedAt ?? AppClock.Now);
            return details;
        }

        public SeasonDetailsDto? GetCurrentSeasonDetails()
        {
            int activeId = _database.ExecuteScalar<int>(SeasonQueries.GetActiveSeasonId, null);
            return activeId > 0 ? GetSeasonDetails(activeId) : null;
        }

        public List<SeasonDomain> GetSeasonDomains(int seasonId)
        {
            return _database.ExecuteQuery(SeasonQueries.GetSeasonDomains, SeasonIdParam(seasonId))
                .Select(row => new SeasonDomain
                {
                    DomainId = Convert.ToInt32(row["DomainId"]),
                    DomainName = row["DomainName"].ToString(),
                    Role = row["Role"].ToString(),
                    Note = row["Note"] == DBNull.Value ? null : row["Note"].ToString(),
                    IsArchived = Convert.ToBoolean(row["IsArchived"])
                })
                .ToList();
        }

        // === UPDATE ===
        public bool UpdateSeason(int seasonId, SeasonUpdateDto dto)
        {
            var existing = GetSeasonById(seasonId);
            if (existing == null) return false;

            // Merge-style: only supplied fields replace what's stored. Empty
            // Intention or Reflection clears it.
            string newName = dto.Name != null ? dto.Name.Trim() : existing.Name;
            string? newIntention = dto.Intention != null ? NullIfBlankString(dto.Intention) : existing.Intention;
            string? newReflection = dto.Reflection != null ? NullIfBlankString(dto.Reflection) : existing.Reflection;
            DateOnly newStart = dto.PlannedStart ?? existing.PlannedStart;
            DateOnly newEnd = dto.PlannedEnd ?? existing.PlannedEnd;

            FieldValidator.ValidateRequiredString(newName, "Name", 100);
            FieldValidator.ValidateOptionalString(newIntention, "Intention", MaxIntentionLength);
            FieldValidator.ValidateOptionalString(newReflection, "Reflection", MaxReflectionLength);

            bool datesChanged = newStart != existing.PlannedStart || newEnd != existing.PlannedEnd;
            if (datesChanged)
            {
                if (existing.Status == "Ended")
                    throw new InvalidOperationException("This season has ended, so its planned dates are locked.");

                ValidatePlannedRange(seasonId, newStart, newEnd);
            }

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@SeasonId", NpgsqlDbType.Integer) { Value = seasonId },
                new NpgsqlParameter("@Name", NpgsqlDbType.Varchar, 100) { Value = newName },
                new NpgsqlParameter("@Intention", NpgsqlDbType.Text) { Value = (object?)newIntention ?? DBNull.Value },
                new NpgsqlParameter("@PlannedStart", NpgsqlDbType.Date) { Value = newStart },
                new NpgsqlParameter("@PlannedEnd", NpgsqlDbType.Date) { Value = newEnd },
                new NpgsqlParameter("@Reflection", NpgsqlDbType.Text) { Value = (object?)newReflection ?? DBNull.Value },
                new NpgsqlParameter("@Now", NpgsqlDbType.Timestamp) { Value = AppClock.Now }
            };

            return _database.ExecuteNonQuery(SeasonQueries.UpdateSeason, parameters) > 0;
        }

        // === DELETE ===
        public bool DeleteSeason(int seasonId)
        {
            FieldValidator.ValidateId(seasonId, "Season ID");
            return _database.ExecuteNonQuery(SeasonQueries.DeleteSeason, SeasonIdParam(seasonId)) > 0;
        }

        /* ===================== DOMAINS ===================== */

        /*
         * Replaces the season's lineup. Returns null when the season doesn't
         * exist. Adding a Focus domain that takes the season past
         * SeasonOverloadPolicy.MaxFocusDomains needs a confirmed overload warning:
         * without a valid ack nothing is saved and the warning comes back
         * instead. Keeping or removing focus domains never asks again.
         */
        public SetDomainsResult? SetDomains(int seasonId, SeasonDomainsSetDto dto, DateTime utcNow)
        {
            var season = GetSeasonById(seasonId);
            if (season == null) return null;

            if (season.Status == "Ended")
                throw new InvalidOperationException("This season has ended, so its lineup is locked.");

            var lineup = dto.Domains ?? new List<SeasonDomainInputDto>();
            var current = GetSeasonDomains(seasonId);
            var names = ValidateLineup(lineup, current);

            var focusIds = lineup.Where(d => d.Role == "Focus").Select(d => d.DomainId).ToList();
            var currentFocusIds = current.Where(d => d.Role == "Focus").Select(d => d.DomainId).ToHashSet();

            string? overloadReason = focusIds.Count > SeasonOverloadPolicy.MaxFocusDomains ? season.OverloadReason : null;

            bool addsFocus = focusIds.Any(id => !currentFocusIds.Contains(id));
            var warning = addsFocus ? SeasonOverloadPolicy.BuildWarning(focusIds.Select(id => names[id]).ToList()) : null;

            if (warning != null)
            {
                var ack = dto.OverloadAck;
                if (ack == null)
                    return new SetDomainsResult(WithChallenge(warning, seasonId, focusIds, utcNow));

                var check = SeasonOverloadToken.Validate(ack.ChallengeToken, seasonId, focusIds, warning.Level, warning.Requires.WaitSeconds, utcNow);
                if (check.Status == SeasonOverloadToken.Status.Invalid)
                {
                    WithChallenge(warning, seasonId, focusIds, utcNow);
                    warning.AckError = "That confirmation was for a different lineup or has expired. Read the warning again and confirm.";
                    return new SetDomainsResult(warning);
                }

                // From here on the same token is handed back, so a mistyped
                // phrase doesn't restart the wait.
                warning.ChallengeToken = ack.ChallengeToken;
                warning.ChallengeExpiresAt = ToAppTime(check.IssuedAtUtc + SeasonOverloadToken.Lifetime);
                warning.Requires.WaitRemainingSeconds = check.WaitRemainingSeconds;

                if (check.Status == SeasonOverloadToken.Status.TooEarly)
                {
                    warning.AckError = $"Take a moment with this. You can confirm in {check.WaitRemainingSeconds} seconds.";
                    return new SetDomainsResult(warning);
                }

                string? ackError = SeasonOverloadPolicy.CheckAck(warning, ack);
                if (ackError != null)
                {
                    warning.AckError = ackError;
                    return new SetDomainsResult(warning);
                }

                if (warning.Requires.ReasonMinLength > 0)
                {
                    overloadReason = ack.Reason!.Trim();
                    FieldValidator.ValidateOptionalString(overloadReason, "Reason", MaxOverloadReasonLength);
                }
            }

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@SeasonId", NpgsqlDbType.Integer) { Value = seasonId },
                new NpgsqlParameter("@DomainIds", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = lineup.Select(d => d.DomainId).ToArray() },
                new NpgsqlParameter("@Roles", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = lineup.Select(d => d.Role).ToArray() },
                new NpgsqlParameter("@Notes", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = lineup.Select(d => NullIfBlankString(d.Note)).ToArray() },
                new NpgsqlParameter("@OverloadReason", NpgsqlDbType.Text) { Value = (object?)overloadReason ?? DBNull.Value },
                new NpgsqlParameter("@Now", NpgsqlDbType.Timestamp) { Value = AppClock.Now }
            };

            _database.ExecuteNonQuery(SeasonQueries.ReplaceSeasonDomains, parameters);
            return new SetDomainsResult(null);
        }

        /* ===================== LIFECYCLE ===================== */

        // Planned -> Active. Returns false when the season doesn't exist.
        public bool StartSeason(int seasonId)
        {
            var season = GetSeasonById(seasonId);
            if (season == null) return false;

            EnsureStartable(season);

            int activeId = _database.ExecuteScalar<int>(SeasonQueries.GetActiveSeasonId, null);
            if (activeId > 0)
                throw new InvalidOperationException("Another season is already active. End it first, or rotate to this one.");

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@SeasonId", NpgsqlDbType.Integer) { Value = seasonId },
                new NpgsqlParameter("@Now", NpgsqlDbType.Timestamp) { Value = AppClock.Now }
            };

            try
            {
                if (_database.ExecuteNonQuery(SeasonQueries.StartSeason, parameters) == 0)
                    throw new InvalidOperationException("Only a planned season can be started.");
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ExclusionViolation)
            {
                throw new InvalidOperationException("Another season is already active. End it first, or rotate to this one.");
            }

            return true;
        }

        // Active -> Ended. Returns false when the season doesn't exist.
        public bool EndSeason(int seasonId, string? reflection)
        {
            var season = GetSeasonById(seasonId);
            if (season == null) return false;

            if (season.Status != "Active")
                throw new InvalidOperationException("Only the active season can be ended.");

            FieldValidator.ValidateOptionalString(reflection, "Reflection", MaxReflectionLength);

            DateTime now = AppClock.Now;
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@SeasonId", NpgsqlDbType.Integer) { Value = seasonId },
                new NpgsqlParameter("@Reflection", NpgsqlDbType.Text) { Value = NullIfBlank(reflection) },
                new NpgsqlParameter("@Now", NpgsqlDbType.Timestamp) { Value = now },
                StatsSnapshotParam(season, now)
            };

            if (_database.ExecuteNonQuery(SeasonQueries.EndSeason, parameters) == 0)
                throw new InvalidOperationException("Only the active season can be ended.");

            return true;
        }

        /*
         * Ends the active season (with an optional reflection) and starts this
         * planned one, atomically. Returns false when the season doesn't exist.
         */
        public bool RotateTo(int seasonId, string? reflection)
        {
            var season = GetSeasonById(seasonId);
            if (season == null) return false;

            EnsureStartable(season);
            FieldValidator.ValidateOptionalString(reflection, "Reflection", MaxReflectionLength);

            int activeId = _database.ExecuteScalar<int>(SeasonQueries.GetActiveSeasonId, null);
            if (activeId <= 0)
                throw new InvalidOperationException("No season is active to rotate out of. Start this one instead.");

            DateTime now = AppClock.Now;
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@SeasonId", NpgsqlDbType.Integer) { Value = seasonId },
                new NpgsqlParameter("@Reflection", NpgsqlDbType.Text) { Value = NullIfBlank(reflection) },
                new NpgsqlParameter("@Now", NpgsqlDbType.Timestamp) { Value = now },
                // The snapshot is for the season being ended, not the one starting.
                StatsSnapshotParam(GetSeasonById(activeId)!, now)
            };

            if (_database.ExecuteNonQuery(SeasonQueries.RotateSeason, parameters) != 2)
                throw new InvalidOperationException("The seasons changed while rotating. Reload and try again.");

            return true;
        }

        /* ===================== STATS ===================== */

        // Live stats from StartedAt to `to`; null until the season has started.
        private SeasonStatsDto? GetStats(SeasonDetailsDto season, DateTime to)
        {
            if (season.StartedAt == null) return null;

            DateTime from = season.StartedAt.Value;

            List<NpgsqlParameter> WindowParams() => new()
            {
                new NpgsqlParameter("@From", NpgsqlDbType.Timestamp) { Value = from },
                new NpgsqlParameter("@To", NpgsqlDbType.Timestamp) { Value = to }
            };

            var nodes = _database.ExecuteQuery(SeasonQueries.GetNodeActivity, WindowParams())
                .Select(row => new SeasonStatsBuilder.NodeActivity(
                    Convert.ToInt32(row["NodeId"]),
                    row["Title"].ToString(),
                    Convert.ToInt32(row["DomainId"]),
                    row["DomainName"].ToString(),
                    Convert.ToInt32(row["Logs"]),
                    Convert.ToInt32(row["ActionsCreated"]),
                    Convert.ToInt32(row["ActionsCompleted"]),
                    Convert.ToInt32(row["LearnEntries"])))
                .ToList();

            int activeDays = _database.ExecuteScalar<int>(SeasonQueries.CountActiveDays, WindowParams());
            var learn = _database.ExecuteQuery(SeasonQueries.GetLearnSummary, WindowParams())[0];

            return SeasonStatsBuilder.Build(
                from, to, nodes, season.Domains, activeDays,
                Convert.ToInt32(learn["LearnSessions"]),
                Convert.ToInt32(learn["UnfiledLearnEntries"]));
        }

        private SeasonStatsDto? GetStatsSnapshot(int seasonId)
        {
            var rows = _database.ExecuteQuery(SeasonQueries.GetStatsSnapshot, SeasonIdParam(seasonId));
            if (rows.Count == 0 || rows[0]["StatsSnapshot"] == DBNull.Value) return null;

            return JsonSerializer.Deserialize<SeasonStatsDto>(rows[0]["StatsSnapshot"].ToString()!);
        }

        // The stats a season ends with, as of the same moment written to EndedAt.
        private NpgsqlParameter StatsSnapshotParam(Season season, DateTime endedAt)
        {
            var details = ToDetails(season);
            details.Domains = GetSeasonDomains(season.SeasonId);
            var stats = GetStats(details, endedAt);

            return new NpgsqlParameter("@StatsSnapshot", NpgsqlDbType.Jsonb)
            {
                Value = stats == null ? DBNull.Value : JsonSerializer.Serialize(stats)
            };
        }

        /* ===================== HELPERS ===================== */

        // Returns the name of every domain in the lineup, keyed by id.
        private Dictionary<int, string> ValidateLineup(List<SeasonDomainInputDto> lineup, List<SeasonDomain> current)
        {
            foreach (var item in lineup)
            {
                FieldValidator.ValidateId(item.DomainId, "Domain ID");
                FieldValidator.ValidateEnumValue(item.Role, "Role", Roles);
                FieldValidator.ValidateOptionalString(item.Note, "Note", 300);
            }

            var duplicate = lineup.GroupBy(d => d.DomainId).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
                throw new ArgumentException($"Domain {duplicate.Key} is listed more than once.");

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@DomainIds", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = lineup.Select(d => d.DomainId).ToArray() }
            };

            var found = _database.ExecuteQuery(SeasonQueries.GetDomainsByIds, parameters)
                .ToDictionary(row => Convert.ToInt32(row["DomainId"]));

            var currentIds = current.Select(d => d.DomainId).ToHashSet();
            foreach (var item in lineup)
            {
                if (!found.TryGetValue(item.DomainId, out var row))
                    throw new ArgumentException($"Domain {item.DomainId} does not exist.");

                // A domain archived mid-season can stay; it just can't be added.
                if (Convert.ToBoolean(row["IsArchived"]) && !currentIds.Contains(item.DomainId))
                    throw new ArgumentException($"Domain '{row["DomainName"]}' is archived. Unarchive it before adding it to a season.");
            }

            return found.ToDictionary(kv => kv.Key, kv => kv.Value["DomainName"].ToString() ?? string.Empty);
        }

        private void ValidatePlannedRange(int seasonId, DateOnly plannedStart, DateOnly plannedEnd)
        {
            if (plannedStart == default)
                throw new ArgumentException("Planned start is required.");
            if (plannedEnd == default)
                throw new ArgumentException("Planned end is required.");
            if (plannedEnd <= plannedStart)
                throw new ArgumentException("Planned end must be after planned start.");

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@SeasonId", NpgsqlDbType.Integer) { Value = seasonId },
                new NpgsqlParameter("@PlannedStart", NpgsqlDbType.Date) { Value = plannedStart },
                new NpgsqlParameter("@PlannedEnd", NpgsqlDbType.Date) { Value = plannedEnd }
            };

            if (_database.ExecuteScalar<int>(SeasonQueries.CountOverlappingSeasons, parameters) > 0)
                throw new ArgumentException("These dates overlap another season that hasn't ended. Seasons can't overlap.");
        }

        private static void EnsureStartable(Season season)
        {
            if (season.Status != "Planned")
                throw new InvalidOperationException("Only a planned season can be started.");
            if (season.FocusCount == 0)
                throw new InvalidOperationException("Add at least one Focus domain before starting the season.");
        }

        private static SeasonOverloadWarningDto WithChallenge(SeasonOverloadWarningDto warning, int seasonId, List<int> focusIds, DateTime utcNow)
        {
            warning.ChallengeToken = SeasonOverloadToken.Issue(seasonId, focusIds, warning.Level, utcNow);
            warning.ChallengeExpiresAt = ToAppTime(utcNow + SeasonOverloadToken.Lifetime);
            return warning;
        }

        // Every other timestamp the API returns is wall-clock time in AppClock's zone.
        private static DateTime ToAppTime(DateTime utc) =>
            DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), AppClock.TimeZone),
                DateTimeKind.Unspecified);

        private static List<NpgsqlParameter> SeasonIdParam(int seasonId) => new()
        {
            new NpgsqlParameter("@SeasonId", NpgsqlDbType.Integer) { Value = seasonId }
        };

        private static object NullIfBlank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

        private static string? NullIfBlankString(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /* ===================== DATA TYPE CONVERTERS (MAPPERS) ===================== */

        public static void ApplyDerivedFields(Season season, DateOnly today)
        {
            season.DaysPlanned = season.PlannedEnd.DayNumber - season.PlannedStart.DayNumber;

            if (season.StartedAt.HasValue)
            {
                DateOnly startDay = DateOnly.FromDateTime(season.StartedAt.Value);
                DateOnly lastDay = season.EndedAt.HasValue ? DateOnly.FromDateTime(season.EndedAt.Value) : today;
                season.DaysElapsed = Math.Max(0, lastDay.DayNumber - startDay.DayNumber);
            }

            if (season.Status == "Active")
            {
                season.DaysRemaining = Math.Max(0, season.PlannedEnd.DayNumber - today.DayNumber);
                season.IsOverdue = today > season.PlannedEnd;
            }
        }

        private static SeasonDetailsDto ToDetails(Season s) => new()
        {
            SeasonId = s.SeasonId,
            Name = s.Name,
            Intention = s.Intention,
            Status = s.Status,
            PlannedStart = s.PlannedStart,
            PlannedEnd = s.PlannedEnd,
            StartedAt = s.StartedAt,
            EndedAt = s.EndedAt,
            Reflection = s.Reflection,
            OverloadReason = s.OverloadReason,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            FocusCount = s.FocusCount,
            PassiveCount = s.PassiveCount,
            DaysPlanned = s.DaysPlanned,
            DaysElapsed = s.DaysElapsed,
            DaysRemaining = s.DaysRemaining,
            IsOverdue = s.IsOverdue
        };

        private static Season ConvertDBRowToSeason(Dictionary<string, object> row)
        {
            var season = new Season
            {
                SeasonId = Convert.ToInt32(row["SeasonId"]),
                Name = row["Name"].ToString(),
                Intention = row["Intention"] == DBNull.Value ? null : row["Intention"].ToString(),
                Status = row["Status"].ToString(),
                PlannedStart = ToDateOnly(row["PlannedStart"]),
                PlannedEnd = ToDateOnly(row["PlannedEnd"]),
                StartedAt = row["StartedAt"] == DBNull.Value ? null : Convert.ToDateTime(row["StartedAt"]),
                EndedAt = row["EndedAt"] == DBNull.Value ? null : Convert.ToDateTime(row["EndedAt"]),
                Reflection = row["Reflection"] == DBNull.Value ? null : row["Reflection"].ToString(),
                OverloadReason = row["OverloadReason"] == DBNull.Value ? null : row["OverloadReason"].ToString(),
                CreatedAt = Convert.ToDateTime(row["CreatedAt"]),
                UpdatedAt = Convert.ToDateTime(row["UpdatedAt"]),
                FocusCount = Convert.ToInt32(row["FocusCount"]),
                PassiveCount = Convert.ToInt32(row["PassiveCount"])
            };

            ApplyDerivedFields(season, AppClock.Today);
            return season;
        }

        // Npgsql hands DATE columns back as DateTime through GetValue.
        private static DateOnly ToDateOnly(object value) =>
            value is DateOnly date ? date : DateOnly.FromDateTime(Convert.ToDateTime(value));
    }
}
