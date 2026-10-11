using System.Drawing;

namespace SAPS.DDMS.API.Services
{
    public sealed class ReportPdfRequest
    {
        public required string Key { get; init; }
        public required string Title { get; init; }
        public required string Category { get; init; }
        public required string GeneratedBy { get; init; }
        public required string GeneratedWhere { get; init; }
        public required DateTime GeneratedAt { get; init; }
        public required IReadOnlyDictionary<string, string> Criteria { get; init; }
        public required IReadOnlyList<string> Columns { get; init; }
        public required IReadOnlyList<Dictionary<string, object?>> Rows { get; init; }
        public required IReadOnlyDictionary<string, object?> Totals { get; init; }
        public byte[] ChartImage { get; init; } = Array.Empty<byte>();
        public string? ChartTitle { get; init; }
        public bool Landscape { get; init; } = true;
    }

    public sealed record ReportMetricCard(string Label, string Value);

    public sealed record ReportPdfChartPoint(string Label, double Value);

    public static class ReportPdfTheme
    {
        public const string Navy = "#002B4D";
        public const string Gold = "#FFB81C";
        public const string LightBlue = "#EAF3FA";
        public const string Border = "#CBD8E5";
        public const string MainText = "#102A43";
        public const string MutedText = "#62758A";
        public const string Positive = "#1F7A4D";
        public const string PositiveLight = "#E8F5EE";
        public const string Pending = "#B7791F";
        public const string PendingLight = "#FFF5D6";
        public const string Risk = "#B42318";
        public const string RiskLight = "#FDECEA";
        public const string Neutral = "#486581";
        public const string NeutralLight = "#F8FBFF";
        public const string White = "#FFFFFF";

        public static Color ToColor(string hex) => ColorTranslator.FromHtml(hex);
    }
}
