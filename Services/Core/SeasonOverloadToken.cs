using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Knowledge_Center_API.Services.Core
{
    /*
     * The challenge token handed out with a focus overload warning. It's signed
     * over the season, the exact set of Focus domains and the warning level, so
     * confirming a four-focus lineup can't be replayed to save a six-focus one,
     * and it records when it was issued so the wait at the highest level can be
     * enforced here rather than trusted to the client.
     *
     * Nothing is stored. The signing key is generated per process: a restart
     * only means the next save asks for confirmation again.
     */
    public static class SeasonOverloadToken
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

        private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

        public enum Status { Valid, Invalid, TooEarly }

        public readonly record struct Check(Status Status, DateTime IssuedAtUtc, int WaitRemainingSeconds);

        public static string Issue(int seasonId, IEnumerable<int> focusDomainIds, int level, DateTime utcNow)
        {
            string payload = BuildPayload(seasonId, focusDomainIds, level, new DateTimeOffset(utcNow, TimeSpan.Zero).ToUnixTimeSeconds());
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            return Base64Url.EncodeToString(payloadBytes) + "." + Base64Url.EncodeToString(Sign(payloadBytes));
        }

        public static Check Validate(string? token, int seasonId, IEnumerable<int> focusDomainIds, int level, int waitSeconds, DateTime utcNow)
        {
            var invalid = new Check(Status.Invalid, default, 0);
            if (string.IsNullOrWhiteSpace(token))
                return invalid;

            string[] parts = token.Split('.');
            if (parts.Length != 2)
                return invalid;

            byte[] payloadBytes, signature;
            try
            {
                payloadBytes = Base64Url.DecodeFromChars(parts[0]);
                signature = Base64Url.DecodeFromChars(parts[1]);
            }
            catch (FormatException)
            {
                return invalid;
            }

            if (!CryptographicOperations.FixedTimeEquals(signature, Sign(payloadBytes)))
                return invalid;

            // The lineup and level are part of the payload, so rebuilding it for
            // what's being saved now and comparing covers all of them at once.
            string payload = Encoding.UTF8.GetString(payloadBytes);
            int lastBar = payload.LastIndexOf('|');
            if (lastBar < 0 || !long.TryParse(payload[(lastBar + 1)..], out long issuedUnix))
                return invalid;

            if (payload != BuildPayload(seasonId, focusDomainIds, level, issuedUnix))
                return invalid;

            DateTime issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedUnix).UtcDateTime;
            if (utcNow > issuedAt + Lifetime || utcNow < issuedAt)
                return invalid;

            double remaining = (issuedAt.AddSeconds(waitSeconds) - utcNow).TotalSeconds;
            if (remaining > 0)
                return new Check(Status.TooEarly, issuedAt, (int)Math.Ceiling(remaining));

            return new Check(Status.Valid, issuedAt, 0);
        }

        private static string BuildPayload(int seasonId, IEnumerable<int> focusDomainIds, int level, long issuedUnix) =>
            $"{seasonId}|{string.Join(',', focusDomainIds.Distinct().OrderBy(id => id))}|{level}|{issuedUnix}";

        private static byte[] Sign(byte[] payloadBytes) => HMACSHA256.HashData(Key, payloadBytes);
    }
}
