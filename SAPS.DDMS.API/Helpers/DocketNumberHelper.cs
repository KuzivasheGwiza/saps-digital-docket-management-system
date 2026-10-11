using System.Security.Cryptography;

namespace SAPS.DDMS.API.Helpers
{
    public class DocketNumberHelper
    {
        public static string GenerateDocketNumber(int stationCode, DateTime date)
        {
            string datePart = date.ToString("yyyyMMdd");
            string randomPart = RandomNumberGenerator.GetInt32(10000, 100000).ToString();
            return $"SAPS-{stationCode}-{datePart}-{randomPart}";
        }

        public static string GenerateOBReferenceNumber(int stationCode, DateTime date)
            => $"OB-{stationCode}-{date:yyyyMMdd}-{RandomNumberGenerator.GetInt32(100000, 1000000)}";

        public static string GenerateFICReferenceNumber(int stationCode, DateTime date)
            => $"FIC-{stationCode}-{date:yyyyMMdd}-{RandomNumberGenerator.GetInt32(100000, 1000000)}";

        public static string GenerateEvidenceReferenceNumber(string? docketNumber, int sequence)
        {
            var safeDocketNumber = string.IsNullOrWhiteSpace(docketNumber)
                ? "UNKNOWN"
                : docketNumber.Trim().Replace(" ", string.Empty).Replace("/", "-");
            return $"EVID-{safeDocketNumber}-{sequence:D4}";
        }
    }
}
