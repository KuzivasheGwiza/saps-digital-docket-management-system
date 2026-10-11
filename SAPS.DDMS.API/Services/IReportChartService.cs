using System.Drawing;
using Microsoft.Extensions.Logging;
using ScottPlot;

namespace SAPS.DDMS.API.Services
{
    public interface IReportChartService
    {
        byte[] CreateStatusBarChart(IReadOnlyList<ReportPdfChartPoint> points, string title);
        byte[] CreateStationWorkloadChart(IReadOnlyList<Dictionary<string, object?>> rows, string title);
        byte[] CreateHotspotBarChart(IReadOnlyList<ReportPdfChartPoint> points, string title);
        byte[] CreateClearanceRateChart(IReadOnlyList<Dictionary<string, object?>> rows, string title);
        byte[] CreateCrimeTrendLineChart(IReadOnlyList<Dictionary<string, object?>> rows, string title);
        byte[] CreateOfficerWorkloadChart(IReadOnlyList<ReportPdfChartPoint> points, string title);
    }

    public class ReportChartService : IReportChartService
    {
        private static readonly Color[] SeriesPalette =
        {
            Color.FromArgb(10, 39, 70),
            Color.FromArgb(11, 92, 173),
            Color.FromArgb(31, 122, 77),
            Color.FromArgb(183, 121, 31),
            Color.FromArgb(180, 35, 24),
            Color.FromArgb(72, 101, 129)
        };

        private readonly ILogger<ReportChartService> _logger;

        public ReportChartService(ILogger<ReportChartService> logger)
        {
            _logger = logger;
        }

        public byte[] CreateStatusBarChart(IReadOnlyList<ReportPdfChartPoint> points, string title)
        {
            if (points.Count == 0) return Array.Empty<byte>();

            try
            {
                var ordered = points
                    .OrderByDescending(point => point.Value)
                    .ThenBy(point => point.Label)
                    .Take(12)
                    .Reverse()
                    .ToList();

                var plot = CreatePlot(title, string.Empty, "Docket Count");
                var positions = Enumerable.Range(0, ordered.Count).Select(index => (double)index).ToArray();
                for (var index = 0; index < ordered.Count; index++)
                {
                    var point = ordered[index];
                    var bar = plot.AddBar(new[] { point.Value }, new[] { positions[index] });
                    bar.Orientation = Orientation.Horizontal;
                    bar.FillColor = StatusColor(point.Label);
                    bar.BorderLineWidth = 0;
                }

                plot.YTicks(positions, ordered.Select(point => point.Label).ToArray());
                plot.SetAxisLimits(xMin: 0);
                plot.Layout(left: 190);

                return Export(plot, 1320, 560);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate report status bar chart.");
                return Array.Empty<byte>();
            }
        }

        public byte[] CreateStationWorkloadChart(IReadOnlyList<Dictionary<string, object?>> rows, string title)
        {
            if (rows.Count == 0) return Array.Empty<byte>();

            try
            {
                var plot = CreatePlot(title, "Station", "Docket Count");
                var stationLabels = rows
                    .Select(row => TextValue(row, "Station"))
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(label => label)
                    .ToArray();
                var statusLabels = rows
                    .Select(row => TextValue(row, "Status"))
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(label => label)
                    .ToArray();

                if (stationLabels.Length == 0 || statusLabels.Length == 0)
                    return Array.Empty<byte>();

                var positions = Enumerable.Range(0, stationLabels.Length).Select(index => (double)index).ToArray();
                var groupWidth = 0.8;
                var seriesWidth = Math.Max(0.14, groupWidth / Math.Max(statusLabels.Length, 1));

                for (var statusIndex = 0; statusIndex < statusLabels.Length; statusIndex++)
                {
                    var status = statusLabels[statusIndex];
                    var values = stationLabels
                        .Select(station => rows
                            .Where(row =>
                                string.Equals(TextValue(row, "Station"), station, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(TextValue(row, "Status"), status, StringComparison.OrdinalIgnoreCase))
                            .Sum(row => NumericValue(row, "Count")))
                        .ToArray();

                    var series = plot.AddBar(values, positions);
                    series.BarWidth = seriesWidth;
                    series.PositionOffset = (-groupWidth / 2d) + (seriesWidth / 2d) + (statusIndex * seriesWidth);
                    series.FillColor = StatusColor(status);
                    series.BorderLineWidth = 0;
                    series.Label = status;
                }

                plot.Legend(location: Alignment.UpperRight);
                plot.XTicks(positions, stationLabels);
                RotateXAxisLabels(plot);
                plot.SetAxisLimits(yMin: 0);

                return Export(plot, 1400, 560);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate station workload chart.");
                return Array.Empty<byte>();
            }
        }

        public byte[] CreateHotspotBarChart(IReadOnlyList<ReportPdfChartPoint> points, string title)
        {
            if (points.Count == 0) return Array.Empty<byte>();

            try
            {
                var ordered = points.OrderByDescending(point => point.Value).Take(12).Reverse().ToList();
                var plot = CreatePlot(title, string.Empty, "Incident Count");
                var values = ordered.Select(point => point.Value).ToArray();
                var positions = Enumerable.Range(0, ordered.Count).Select(index => (double)index).ToArray();

                var bar = plot.AddBar(values, positions);
                bar.Orientation = Orientation.Horizontal;
                bar.FillColor = ReportPdfTheme.ToColor(ReportPdfTheme.Navy);
                bar.BorderLineWidth = 0;

                plot.YTicks(positions, ordered.Select(point => point.Label).ToArray());
                plot.SetAxisLimits(xMin: 0);
                plot.Layout(left: 220);

                return Export(plot, 1400, 620);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate hotspot analysis chart.");
                return Array.Empty<byte>();
            }
        }

        public byte[] CreateClearanceRateChart(IReadOnlyList<Dictionary<string, object?>> rows, string title)
        {
            if (rows.Count == 0) return Array.Empty<byte>();

            try
            {
                var plot = CreatePlot(title, "Station", "Clearance Rate (%)");
                var labels = rows.Select(row => TextValue(row, "Station")).ToArray();
                var positions = Enumerable.Range(0, labels.Length).Select(index => (double)index).ToArray();
                var values = rows.Select(row => NumericValue(row, "ClearanceRate")).ToArray();

                var bar = plot.AddBar(values, positions);
                bar.FillColor = ReportPdfTheme.ToColor(ReportPdfTheme.Positive);
                bar.BorderLineWidth = 0;
                bar.ShowValuesAboveBars = true;
                bar.ValueFormatter = value => $"{value:0.0}%";

                plot.XTicks(positions, labels);
                RotateXAxisLabels(plot);
                plot.SetAxisLimits(yMin: 0, yMax: Math.Max(100, values.DefaultIfEmpty(0).Max() + 10));

                return Export(plot, 1200, 520);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate clearance rate chart.");
                return Array.Empty<byte>();
            }
        }

        public byte[] CreateCrimeTrendLineChart(IReadOnlyList<Dictionary<string, object?>> rows, string title)
        {
            if (rows.Count == 0) return Array.Empty<byte>();

            try
            {
                var plot = CreatePlot(title, "Month", "Cases");
                var monthLabels = rows
                    .Select(row => TextValue(row, "Month"))
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(label => label)
                    .ToArray();
                var xPositions = monthLabels.Select((_, index) => (double)index).ToArray();
                var categories = rows
                    .GroupBy(row => TextValue(row, "CrimeCategory"))
                    .OrderByDescending(group => group.Sum(item => NumericValue(item, "Count")))
                    .ThenBy(group => group.Key)
                    .Take(6)
                    .ToList();

                if (monthLabels.Length == 0 || categories.Count == 0)
                    return Array.Empty<byte>();

                for (var index = 0; index < categories.Count; index++)
                {
                    var category = categories[index];
                    var seriesValues = monthLabels
                        .Select(month => category
                            .Where(item => string.Equals(TextValue(item, "Month"), month, StringComparison.OrdinalIgnoreCase))
                            .Sum(item => NumericValue(item, "Count")))
                        .ToArray();

                    var scatter = plot.AddScatter(
                        xPositions,
                        seriesValues,
                        color: SeriesPalette[index % SeriesPalette.Length],
                        markerSize: 6,
                        lineWidth: 2,
                        label: category.Key);
                    scatter.MarkerShape = MarkerShape.filledCircle;
                }

                plot.XTicks(xPositions, monthLabels);
                RotateXAxisLabels(plot);
                plot.Legend(location: Alignment.UpperRight);
                plot.SetAxisLimits(yMin: 0);

                return Export(plot, 1320, 560);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate crime trend chart.");
                return Array.Empty<byte>();
            }
        }

        public byte[] CreateOfficerWorkloadChart(IReadOnlyList<ReportPdfChartPoint> points, string title)
        {
            if (points.Count == 0) return Array.Empty<byte>();

            try
            {
                var ordered = points.OrderByDescending(point => point.Value).Take(12).Reverse().ToList();
                var plot = CreatePlot(title, string.Empty, "Active Cases");
                var positions = Enumerable.Range(0, ordered.Count).Select(index => (double)index).ToArray();
                for (var index = 0; index < ordered.Count; index++)
                {
                    var point = ordered[index];
                    var bar = plot.AddBar(new[] { point.Value }, new[] { positions[index] });
                    bar.Orientation = Orientation.Horizontal;
                    bar.FillColor = string.Equals(point.Label, "Unallocated", StringComparison.OrdinalIgnoreCase)
                        ? ReportPdfTheme.ToColor(ReportPdfTheme.Pending)
                        : ReportPdfTheme.ToColor(ReportPdfTheme.Navy);
                    bar.BorderLineWidth = 0;
                }

                plot.YTicks(positions, ordered.Select(point => point.Label).ToArray());
                plot.SetAxisLimits(xMin: 0);
                plot.Layout(left: 220);

                return Export(plot, 1400, 620);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate officer workload chart.");
                return Array.Empty<byte>();
            }
        }

        private static Plot CreatePlot(string title, string xLabel, string yLabel)
        {
            var plot = new Plot(1200, 520);
            plot.Style(figureBackground: Color.White, dataBackground: Color.White);
            plot.Title(title, size: 20, color: ReportPdfTheme.ToColor(ReportPdfTheme.MainText));
            if (!string.IsNullOrWhiteSpace(xLabel))
                plot.XLabel(xLabel);
            if (!string.IsNullOrWhiteSpace(yLabel))
                plot.YLabel(yLabel);
            plot.XAxis.Color(ReportPdfTheme.ToColor(ReportPdfTheme.MainText));
            plot.YAxis.Color(ReportPdfTheme.ToColor(ReportPdfTheme.MainText));
            return plot;
        }

        private static void RotateXAxisLabels(Plot plot)
        {
            plot.XAxis.TickLabelStyle(rotation: 35);
        }

        private static byte[] Export(Plot plot, int width, int height)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"saps-report-chart-{Guid.NewGuid():N}.png");

            try
            {
                plot.SaveFig(tempFile, width, height);
                return File.ReadAllBytes(tempFile);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        private static string TextValue(IReadOnlyDictionary<string, object?> row, string key)
        {
            return row.TryGetValue(key, out var value)
                ? value?.ToString()?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static double NumericValue(IReadOnlyDictionary<string, object?> row, string key)
        {
            if (!row.TryGetValue(key, out var value) || value == null)
                return 0d;

            return value switch
            {
                byte number => number,
                short number => number,
                int number => number,
                long number => number,
                float number => number,
                double number => number,
                decimal number => (double)number,
                _ when double.TryParse(value.ToString(), out var parsed) => parsed,
                _ => 0d
            };
        }

        private static Color StatusColor(string label)
        {
            return label.ToUpperInvariant() switch
            {
                "CLOSED" or "ARCHIVED" => ReportPdfTheme.ToColor(ReportPdfTheme.Positive),
                "OPEN" or "PENDING_PROSECUTOR" or "REOPENED" => ReportPdfTheme.ToColor(ReportPdfTheme.Pending),
                "ALLOCATED" or "UNDER_INVESTIGATION" => ReportPdfTheme.ToColor("#0B5CAD"),
                _ => ReportPdfTheme.ToColor(ReportPdfTheme.Neutral)
            };
        }
    }
}
