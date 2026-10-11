using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SAPS.DDMS.API.Services
{
    public sealed record ReportPdfChartItem(string Label, double Value);

    public interface IReportPdfService
    {
        byte[] BuildReportPdf(ReportPdfRequest request);

        byte[] BuildTextReport(
            string title,
            string generatedBy,
            IEnumerable<string> lines,
            IEnumerable<ReportPdfChartItem>? chart = null);
    }

    public class ReportPdfService : IReportPdfService
    {
        private readonly byte[]? _logoBytes;
        private readonly ILogger<ReportPdfService> _logger;

        public ReportPdfService(IWebHostEnvironment environment, ILogger<ReportPdfService> logger)
        {
            _logger = logger;

            var logoPath = Path.Combine(environment.WebRootPath ?? "wwwroot", "branding", "saps-badge.jpg");
            if (File.Exists(logoPath))
            {
                _logoBytes = File.ReadAllBytes(logoPath);
            }
            else
            {
                _logger.LogWarning("SAPS report logo could not be found at {LogoPath}. PDF export will fall back to a text-only header.", logoPath);
            }
        }

        public byte[] BuildReportPdf(ReportPdfRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var stream = new MemoryStream();
            var document = new StructuredReportDocument(request, _logoBytes);
            document.GeneratePdf(stream);
            return stream.ToArray();
        }

        public byte[] BuildTextReport(
            string title,
            string generatedBy,
            IEnumerable<string> lines,
            IEnumerable<ReportPdfChartItem>? chart = null)
        {
            var normalizedLines = lines?
                .Select(line => string.IsNullOrWhiteSpace(line) ? "-" : line.Trim())
                .ToList() ?? new List<string>();

            if (normalizedLines.Count == 0)
                normalizedLines.Add("No data available.");

            var request = new ReportPdfRequest
            {
                Key = "text-export",
                Title = title,
                Category = "Operational Export",
                GeneratedBy = string.IsNullOrWhiteSpace(generatedBy) ? "SAPS DDMS user" : generatedBy.Trim(),
                GeneratedWhere = "SAPS DDMS",
                GeneratedAt = DateTime.UtcNow,
                Criteria = new Dictionary<string, string>(),
                Columns = new[] { "Detail" },
                Rows = normalizedLines
                    .Select(line => new Dictionary<string, object?> { ["Detail"] = line })
                    .ToList(),
                Totals = new Dictionary<string, object?> { ["Line Count"] = normalizedLines.Count },
                Landscape = false
            };

            return BuildReportPdf(request);
        }

        private sealed class StructuredReportDocument : IDocument
        {
            private readonly ReportPdfRequest _request;
            private readonly byte[]? _logoBytes;

            public StructuredReportDocument(ReportPdfRequest request, byte[]? logoBytes)
            {
                _request = request;
                _logoBytes = logoBytes;
            }

            public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

            public void Compose(IDocumentContainer container)
            {
                container.Page(page =>
                {
                    page.Size(_request.Landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(TextStyle.Default.FontSize(9).FontColor(ReportPdfTheme.MainText));

                    page.Header().Element(ComposeHeader);
                    page.Content().PaddingTop(14).Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);
                });
            }

            private void ComposeHeader(IContainer container)
            {
                container.Column(column =>
                {
                    column.Item().Background(ReportPdfTheme.White).PaddingVertical(14).PaddingHorizontal(16).Row(row =>
                    {
                        if (_logoBytes is { Length: > 0 })
                        {
                            row.ConstantItem(62).Height(62).AlignMiddle().AlignCenter().Image(_logoBytes);
                        }

                        row.RelativeItem().Column(header =>
                        {
                            header.Spacing(3);
                            header.Item().Text("SAPS DIGITAL DOCKET MANAGEMENT SYSTEM")
                                .Bold()
                                .FontSize(16)
                                .FontColor(ReportPdfTheme.Navy);
                            header.Item().Text(_request.Title)
                                .SemiBold()
                                .FontSize(13)
                                .FontColor(ReportPdfTheme.Navy);
                        });
                    });
                    column.Item().Height(1).Background(ReportPdfTheme.Border);
                });
            }

            private void ComposeContent(IContainer container)
            {
                container.Column(column =>
                {
                    column.Spacing(14);
                    column.Item().ShowEntire().Element(ComposeMetadata);

                    if (GetMetricCards().Count > 0)
                        column.Item().ShowEntire().Element(ComposeMetricCards);

                    if (_request.ChartImage.Length > 0)
                        column.Item().ShowEntire().Element(ComposeChart);

                    column.Item().Element(ComposeMainBody);
                    column.Item().ShowEntire().Element(ComposeTotals);
                });
            }

            private void ComposeMetadata(IContainer container)
            {
                var metadataItems = new List<(string Label, string Value)>
                {
                    ("Generated by", Safe(_request.GeneratedBy)),
                    ("Generated where", Safe(_request.GeneratedWhere)),
                    ("Generated date", _request.GeneratedAt.ToLocalTime().ToString("dd MMM yyyy")),
                    ("Generated time", _request.GeneratedAt.ToLocalTime().ToString("HH:mm:ss")),
                    ("Report category", Safe(_request.Category))
                };

                if (_request.Criteria.Count > 0)
                {
                    metadataItems.AddRange(_request.Criteria
                        .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                        .Select(item => (item.Key, item.Value)));
                }

                container.Border(1).BorderColor(ReportPdfTheme.Border).Background(ReportPdfTheme.NeutralLight).Padding(12).Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text("Report Metadata").Bold().FontSize(11).FontColor(ReportPdfTheme.MainText);

                    column.Item().Grid(grid =>
                    {
                        grid.Columns(2);
                        foreach (var item in metadataItems)
                        {
                            grid.Item().PaddingBottom(6).Column(meta =>
                            {
                                meta.Item().Text(item.Label.ToUpperInvariant())
                                    .FontSize(8)
                                    .SemiBold()
                                    .FontColor(ReportPdfTheme.MutedText);
                                meta.Item().Text(Safe(item.Value))
                                    .FontSize(10)
                                    .FontColor(ReportPdfTheme.MainText);
                            });
                        }
                    });
                });
            }

            private void ComposeMetricCards(IContainer container)
            {
                var cards = GetMetricCards();
                container.Row(row =>
                {
                    row.Spacing(10);
                    foreach (var card in cards)
                    {
                        row.RelativeItem().Border(1).BorderColor(ReportPdfTheme.Border).Background(ReportPdfTheme.White).Padding(12).Column(column =>
                        {
                            column.Spacing(4);
                            column.Item().Text(card.Label.ToUpperInvariant())
                                .FontSize(8)
                                .SemiBold()
                                .FontColor(ReportPdfTheme.MutedText);
                            column.Item().Text(card.Value)
                                .FontSize(16)
                                .Bold()
                                .FontColor(ReportPdfTheme.MainText);
                        });
                    }
                });
            }

            private void ComposeChart(IContainer container)
            {
                container.Border(1).BorderColor(ReportPdfTheme.Border).Background(Colors.White).Padding(12).Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text(_request.ChartTitle ?? "Operational Summary")
                        .Bold()
                        .FontSize(11)
                        .FontColor(ReportPdfTheme.MainText);
                    column.Item().Height(240).AlignCenter().AlignMiddle().Image(_request.ChartImage, ImageScaling.FitArea);
                });
            }

            private void ComposeMainBody(IContainer container)
            {
                if (_request.Key.Equals("station-workload", StringComparison.OrdinalIgnoreCase))
                {
                    ComposeStationWorkload(container);
                    return;
                }

                if (_request.Key.Equals("crime-trend", StringComparison.OrdinalIgnoreCase))
                {
                    ComposeCrimeTrendTransactionReport(container);
                    return;
                }

                if (_request.Key.Equals("custody-summary", StringComparison.OrdinalIgnoreCase))
                {
                    ComposeCustodyTransactionReport(container);
                    return;
                }

                ComposeStandardTable(container, _request.Columns, _request.Rows);
            }

            private void ComposeStationWorkload(IContainer container)
            {
                var groupedByProvince = _request.Rows
                    .GroupBy(row => Safe(GetValue(row, "Province")))
                    .OrderBy(group => group.Key)
                    .ToList();

                if (groupedByProvince.Count == 0)
                {
                    container.Element(ComposeNoDataState);
                    return;
                }

                container.Column(column =>
                {
                    column.Spacing(12);

                    foreach (var province in groupedByProvince)
                    {
                        var provinceTotal = province.Sum(row => ToNumber(GetValue(row, "Count")));

                        column.Item().Border(1).BorderColor(ReportPdfTheme.Border).Column(group =>
                        {
                            group.Item().Background(ReportPdfTheme.White).BorderBottom(1).BorderColor(ReportPdfTheme.Border).Padding(10).Row(row =>
                            {
                                row.RelativeItem().Text($"Province: {province.Key}")
                                    .Bold()
                                    .FontSize(11)
                                    .FontColor(ReportPdfTheme.Navy);
                                row.ConstantItem(120).AlignRight().Text($"Subtotal: {provinceTotal:0.##}")
                                    .Bold()
                                    .FontColor(ReportPdfTheme.Navy);
                            });

                            group.Item().Padding(10).Column(stations =>
                            {
                                stations.Spacing(10);
                                foreach (var station in province.GroupBy(row => Safe(GetValue(row, "Station"))).OrderBy(grouping => grouping.Key))
                                {
                                    var stationSubtotal = station.Sum(row => ToNumber(GetValue(row, "Count")));

                                    stations.Item().Border(1).BorderColor(ReportPdfTheme.Border).Column(stationColumn =>
                                    {
                                        stationColumn.Item().Background(ReportPdfTheme.NeutralLight).Padding(8).Row(row =>
                                        {
                                            row.RelativeItem().Text($"Station: {station.Key}")
                                                .SemiBold()
                                                .FontColor(ReportPdfTheme.MainText);
                                            row.ConstantItem(120).AlignRight().Text($"Subtotal: {stationSubtotal:0.##}")
                                                .SemiBold()
                                                .FontColor(ReportPdfTheme.MainText);
                                        });

                                        stationColumn.Item().Element(body =>
                                            ComposeStandardTable(body, new[] { "Status", "Count" },
                                                station.Select(row => new Dictionary<string, object?>
                                                {
                                                    ["Status"] = GetValue(row, "Status"),
                                                    ["Count"] = GetValue(row, "Count")
                                                }).ToList(),
                                                compact: true));
                                    });
                                }
                            });

                            group.Item().AlignRight().Padding(10).Text($"Province subtotal: {provinceTotal:0.##}")
                                .SemiBold()
                                .FontColor(ReportPdfTheme.MainText);
                        });
                    }
                });
            }

            private void ComposeCrimeTrendTransactionReport(IContainer container)
            {
                var months = _request.Rows
                    .GroupBy(row => Safe(GetValue(row, "Month")))
                    .OrderBy(group => group.Key)
                    .ToList();

                if (months.Count == 0)
                {
                    container.Element(ComposeNoDataState);
                    return;
                }

                container.Column(column =>
                {
                    column.Spacing(12);
                    foreach (var month in months)
                    {
                        var monthTotal = month.Sum(row => ToNumber(GetValue(row, "Count")));
                        column.Item().Border(1).BorderColor(ReportPdfTheme.Border).Column(monthSection =>
                        {
                            monthSection.Item().Background(ReportPdfTheme.White).BorderBottom(1).BorderColor(ReportPdfTheme.Border).Padding(10).Row(row =>
                            {
                                row.RelativeItem().Text($"Control break 1 — Month: {month.Key}")
                                    .Bold().FontSize(11).FontColor(ReportPdfTheme.Navy);
                                row.ConstantItem(140).AlignRight().Text($"Month subtotal: {monthTotal:0}")
                                    .Bold().FontColor(ReportPdfTheme.Navy);
                            });

                            monthSection.Item().Padding(10).Column(categories =>
                            {
                                categories.Spacing(8);
                                foreach (var category in month
                                             .GroupBy(row => Safe(GetValue(row, "CrimeCategory")))
                                             .OrderBy(group => group.Key))
                                {
                                    var categoryTotal = category.Sum(row => ToNumber(GetValue(row, "Count")));
                                    categories.Item().Border(1).BorderColor(ReportPdfTheme.Border).Column(categorySection =>
                                    {
                                        categorySection.Item().Background(ReportPdfTheme.NeutralLight).Padding(8).Row(row =>
                                        {
                                            row.RelativeItem().Text($"Control break 2 — Crime type: {category.Key}")
                                                .SemiBold().FontColor(ReportPdfTheme.MainText);
                                            row.ConstantItem(150).AlignRight().Text($"Crime subtotal: {categoryTotal:0}")
                                                .SemiBold().FontColor(ReportPdfTheme.MainText);
                                        });

                                        categorySection.Item().Element(body => ComposeStandardTable(
                                            body,
                                            new[] { "Month", "CrimeCategory", "Count" },
                                            category.ToList(),
                                            compact: true));
                                    });
                                }
                            });

                            monthSection.Item().AlignRight().Padding(10)
                                .Text($"Month subtotal — {month.Key}: {monthTotal:0}")
                                .SemiBold().FontColor(ReportPdfTheme.MainText);
                        });
                    }

                    column.Item().Background(ReportPdfTheme.White).BorderTop(1).BorderBottom(1).BorderColor(ReportPdfTheme.Border).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("Grand total — cases in selected period")
                            .Bold().FontColor(ReportPdfTheme.Navy);
                        row.ConstantItem(140).AlignRight()
                            .Text(months.Sum(group => group.Sum(item => ToNumber(GetValue(item, "Count")))).ToString("0"))
                            .Bold().FontColor(ReportPdfTheme.Navy);
                    });
                });
            }

            private void ComposeCustodyTransactionReport(IContainer container)
            {
                var dockets = _request.Rows
                    .GroupBy(row => Safe(GetValue(row, "Docket")))
                    .OrderBy(group => group.Key)
                    .ToList();

                if (dockets.Count == 0)
                {
                    container.Element(ComposeNoDataState);
                    return;
                }

                container.Column(column =>
                {
                    column.Spacing(12);
                    foreach (var docket in dockets)
                    {
                        var docketTotal = docket.Count();
                        column.Item().Border(1).BorderColor(ReportPdfTheme.Border).Column(docketSection =>
                        {
                            docketSection.Item().Background(ReportPdfTheme.White).BorderBottom(1).BorderColor(ReportPdfTheme.Border).Padding(10).Row(row =>
                            {
                                row.RelativeItem().Text($"Control break 1 — Docket: {docket.Key}")
                                    .Bold().FontSize(11).FontColor(ReportPdfTheme.Navy);
                                row.ConstantItem(150).AlignRight().Text($"Docket subtotal: {docketTotal}")
                                    .Bold().FontColor(ReportPdfTheme.Navy);
                            });

                            docketSection.Item().Padding(10).Column(evidenceItems =>
                            {
                                evidenceItems.Spacing(8);
                                foreach (var evidence in docket
                                             .GroupBy(row => Safe(GetValue(row, "Evidence")))
                                             .OrderBy(group => group.Key))
                                {
                                    var evidenceTotal = evidence.Count();
                                    evidenceItems.Item().Border(1).BorderColor(ReportPdfTheme.Border).Column(evidenceSection =>
                                    {
                                        evidenceSection.Item().Background(ReportPdfTheme.NeutralLight).Padding(8).Row(row =>
                                        {
                                            row.RelativeItem().Text($"Control break 2 — Evidence: {evidence.Key}")
                                                .SemiBold().FontColor(ReportPdfTheme.MainText);
                                            row.ConstantItem(160).AlignRight().Text($"Evidence subtotal: {evidenceTotal}")
                                                .SemiBold().FontColor(ReportPdfTheme.MainText);
                                        });

                                        evidenceSection.Item().Element(body => ComposeStandardTable(
                                            body,
                                            new[] { "TransferredAt", "From", "To", "Reason" },
                                            evidence.ToList(),
                                            compact: true));
                                    });
                                }
                            });

                            docketSection.Item().AlignRight().Padding(10)
                                .Text($"Docket subtotal — {docket.Key}: {docketTotal} transfer(s)")
                                .SemiBold().FontColor(ReportPdfTheme.MainText);
                        });
                    }

                    column.Item().Background(ReportPdfTheme.White).BorderTop(1).BorderBottom(1).BorderColor(ReportPdfTheme.Border).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("Grand total — custody transfers")
                            .Bold().FontColor(ReportPdfTheme.Navy);
                        row.ConstantItem(150).AlignRight().Text(_request.Rows.Count.ToString())
                            .Bold().FontColor(ReportPdfTheme.Navy);
                    });
                });
            }

            private void ComposeStandardTable(IContainer container, IReadOnlyList<string> columns, IReadOnlyList<Dictionary<string, object?>> rows, bool compact = false)
            {
                container.Border(1).BorderColor(ReportPdfTheme.Border).Table(table =>
                {
                    table.ColumnsDefinition(definition =>
                    {
                        foreach (var column in columns)
                        {
                            definition.RelativeColumn(ColumnWeight(column));
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var column in columns)
                        {
                            header.Cell().Element(cell => HeaderCellStyle(cell, compact)).Text(DisplayColumn(column))
                                .FontColor(ReportPdfTheme.Navy)
                                .SemiBold();
                        }
                    });

                    if (rows.Count == 0)
                    {
                        table.Cell().ColumnSpan((uint)columns.Count).Element(cell => BodyCellStyle(cell, false, compact))
                            .Text("No report data matched the selected criteria.");
                        return;
                    }

                    for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                    {
                        var row = rows[rowIndex];
                        var alternate = rowIndex % 2 == 1;
                        foreach (var column in columns)
                        {
                            table.Cell().Element(cell => BodyCellStyle(cell, alternate, compact))
                                .Element(inner => ComposeCell(inner, column, row.TryGetValue(column, out var value) ? value : null));
                        }
                    }
                });
            }

            private void ComposeCell(IContainer container, string column, object? value)
            {
                var text = FormatValue(column, value);
                if (column.Contains("Status", StringComparison.OrdinalIgnoreCase))
                {
                    container.Text(text).SemiBold().FontColor(ReportPdfTheme.MainText);
                    return;
                }
                var badge = GetBadge(column, text);

                if (badge == null)
                {
                    container.Text(text).FontColor(ReportPdfTheme.MainText);
                    return;
                }

                container.AlignLeft().PaddingVertical(2).Element(cell =>
                {
                    cell.Background(badge.Value.Background)
                        .Border(1)
                        .BorderColor(badge.Value.Border)
                        .PaddingVertical(4)
                        .PaddingHorizontal(6)
                        .Text(text)
                        .SemiBold()
                        .FontColor(badge.Value.Text);
                });
            }

            private void ComposeTotals(IContainer container)
            {
                if (_request.Totals.Count == 0)
                    return;

                container.Background(ReportPdfTheme.White).Border(1).BorderColor(ReportPdfTheme.Border).Padding(12).Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text("Report Totals").Bold().FontSize(11).FontColor(ReportPdfTheme.MainText);
                    column.Item().Grid(grid =>
                    {
                        grid.Columns(2);
                        foreach (var total in _request.Totals)
                        {
                            grid.Item().Row(row =>
                            {
                                row.RelativeItem().Text(total.Key).SemiBold().FontColor(ReportPdfTheme.MainText);
                                row.RelativeItem().AlignRight().Text(FormatValue(total.Key, total.Value)).FontColor(ReportPdfTheme.MainText);
                            });
                        }
                    });
                });
            }

            private void ComposeFooter(IContainer container)
            {
                container.BorderTop(1).BorderColor(ReportPdfTheme.Border).PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text("SAPS Digital Docket Management System | Confidential operational report")
                        .FontSize(8)
                        .FontColor(ReportPdfTheme.MutedText);

                    row.ConstantItem(120).AlignRight().Text(text =>
                    {
                        text.Span("Page ").FontSize(8).FontColor(ReportPdfTheme.MutedText);
                        text.CurrentPageNumber().FontSize(8).FontColor(ReportPdfTheme.MutedText);
                        text.Span(" of ").FontSize(8).FontColor(ReportPdfTheme.MutedText);
                        text.TotalPages().FontSize(8).FontColor(ReportPdfTheme.MutedText);
                    });
                });
            }

            private void ComposeNoDataState(IContainer container)
            {
                container.Border(1).BorderColor(ReportPdfTheme.Border).Background(ReportPdfTheme.NeutralLight).Padding(16).Text("No report data matched the selected criteria.")
                    .FontColor(ReportPdfTheme.MainText);
            }

            private List<ReportMetricCard> GetMetricCards()
            {
                return _request.Totals
                    .Take(4)
                    .Select(total => new ReportMetricCard(total.Key, FormatValue(total.Key, total.Value)))
                    .ToList();
            }

            private static IContainer HeaderCellStyle(IContainer container, bool compact)
            {
                return container.Background(ReportPdfTheme.White)
                    .BorderBottom(1)
                    .BorderColor(ReportPdfTheme.Navy)
                    .PaddingVertical(compact ? 6 : 8)
                    .PaddingHorizontal(6);
            }

            private static IContainer BodyCellStyle(IContainer container, bool alternate, bool compact)
            {
                return container.Background(alternate ? ReportPdfTheme.NeutralLight : ReportPdfTheme.White)
                    .BorderBottom(1)
                    .BorderColor(ReportPdfTheme.Border)
                    .PaddingVertical(compact ? 4 : 6)
                    .PaddingHorizontal(6);
            }

            private static string DisplayColumn(string column) => column switch
            {
                "DocketNumber" => "Docket Number",
                "OBReferenceNumber" => "OB Reference",
                "DateCreated" => "Date Created",
                "DateCollected" => "Date Collected",
                "TransferredAt" => "Transferred At",
                "ClearanceRate" => "Clearance Rate",
                "AgeDays" => "Age Days",
                "AgeBand" => "Age Band",
                "TotalCases" => "Total Cases",
                "ClearedCases" => "Cleared Cases",
                "StationTotal" => "Station Total",
                "CrimeCategory" => "Crime Category",
                _ => column.Replace('_', ' ')
            };

            private static float ColumnWeight(string column) => column switch
            {
                "Description" or "Reason" => 2.2f,
                "DocketNumber" or "Reference" or "OBReferenceNumber" or "OB" => 1.6f,
                "Area" or "CrimeCategory" or "Station" or "Officer" => 1.5f,
                "DateCreated" or "DateCollected" or "TransferredAt" => 1.4f,
                _ => 1f
            };

            private static (string Background, string Border, string Text)? GetBadge(string column, string value)
            {
                if (column.Contains("Priority", StringComparison.OrdinalIgnoreCase))
                {
                    return value.ToUpperInvariant() switch
                    {
                        "HIGH" => (ReportPdfTheme.RiskLight, ReportPdfTheme.Risk, ReportPdfTheme.Risk),
                        "MEDIUM" => (ReportPdfTheme.PendingLight, ReportPdfTheme.Pending, ReportPdfTheme.Pending),
                        "LOW" or "NORMAL" => (ReportPdfTheme.PositiveLight, ReportPdfTheme.Positive, ReportPdfTheme.Positive),
                        _ => (ReportPdfTheme.NeutralLight, ReportPdfTheme.Neutral, ReportPdfTheme.Neutral)
                    };
                }

                return null;
            }

            private static string FormatValue(string key, object? value)
            {
                if (value == null)
                    return "-";

                return value switch
                {
                    DateTime date => date.ToLocalTime().ToString("dd MMM yyyy HH:mm"),
                    bool flag => flag ? "Yes" : "No",
                    decimal number when key.Contains("Rate", StringComparison.OrdinalIgnoreCase) => $"{number:0.0}%",
                    double number when key.Contains("Rate", StringComparison.OrdinalIgnoreCase) => $"{number:0.0}%",
                    float number when key.Contains("Rate", StringComparison.OrdinalIgnoreCase) => $"{number:0.0}%",
                    decimal number => $"{number:0.##}",
                    double number => $"{number:0.##}",
                    float number => $"{number:0.##}",
                    _ => string.IsNullOrWhiteSpace(value.ToString()) ? "-" : value.ToString()!
                };
            }

            private static object? GetValue(IReadOnlyDictionary<string, object?> row, string key)
            {
                return row.TryGetValue(key, out var value) ? value : null;
            }

            private static string Safe(object? value) => string.IsNullOrWhiteSpace(value?.ToString()) ? "-" : value!.ToString()!;

            private static double ToNumber(object? value)
            {
                return value switch
                {
                    byte number => number,
                    short number => number,
                    int number => number,
                    long number => number,
                    float number => number,
                    double number => number,
                    decimal number => (double)number,
                    _ when double.TryParse(value?.ToString(), out var parsed) => parsed,
                    _ => 0d
                };
            }
        }
    }
}
