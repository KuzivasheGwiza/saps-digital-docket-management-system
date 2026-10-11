using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SAPS.DDMS.API.Services
{
    public interface IAiService
    {
        Task<string> SummarizeDocketAsync(int docketId, int userId);
        Task<Dictionary<string, object?>> ExtractDocketDataAsync(int docketId, int userId);
        Task<IEnumerable<(int relatedDocketId, double score, string reason)>> SuggestRelatedCasesAsync(int docketId, int maxResults = 5);
        Task<bool> VerifyExtractionsAsync(int docketId, object verifiedValues, int userId);
    }

    // Privacy-preserving local NLP implementation. It performs extractive summarisation
    // and TF-IDF/cosine case similarity without sending police data to a third party.
    public class LocalAiService : IAiService
    {
        private readonly ApplicationDbContext _db;

        public LocalAiService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<string> SummarizeDocketAsync(int docketId, int userId)
        {
            var docket = await DocketQuery()
                .FirstOrDefaultAsync(item => item.DocketID == docketId);
            if (docket == null) return "Docket not found.";

            var summary = BuildExtractiveSummary(docket);

            var ageDays = Math.Max(0, (int)(DateTime.UtcNow - docket.DateCreated).TotalDays);
            var incidentType = docket.Incident?.IncidentType?.TypeName ?? "Unspecified";
            var area = docket.Incident?.GeoLocation?.AreaName ?? "Unspecified";
            var evidenceCount = docket.Evidences?.Count ?? 0;
            var noteCount = docket.Investigation?.InvestigationNotes?.Count ?? 0;

            return $"Docket {docket.DocketNumber} is {docket.DocketStatus?.StatusName ?? "UNSPECIFIED"} " +
                   $"at {docket.Station?.StationName ?? "an unspecified station"}. " +
                   $"Incident: {incidentType}, {docket.Incident?.OccurrenceDate:yyyy-MM-dd HH:mm}, area {area}. " +
                   $"The docket is {ageDays} day(s) old and contains {evidenceCount} evidence item(s) and {noteCount} investigation note(s). " +
                   $"Local NLP extractive summary: {summary} " +
                   "Review this decision-support summary against the source docket before use.";
        }

        public async Task<Dictionary<string, object?>> ExtractDocketDataAsync(int docketId, int userId)
        {
            var docket = await DocketQuery()
                .FirstOrDefaultAsync(item => item.DocketID == docketId);
            if (docket == null) return new Dictionary<string, object?>();

            var extraction = new Dictionary<string, object?>
            {
                ["docketNumber"] = docket.DocketNumber,
                ["obReferenceNumber"] = docket.OBReferenceNumber,
                ["status"] = docket.DocketStatus?.StatusName,
                ["station"] = docket.Station?.StationName,
                ["caseType"] = docket.CaseType,
                ["incidentType"] = docket.Incident?.IncidentType?.TypeName,
                ["occurrenceDate"] = docket.Incident?.OccurrenceDate,
                ["area"] = docket.Incident?.GeoLocation?.AreaName,
                ["locationType"] = docket.Incident?.LocationType,
                ["incidentDescription"] = docket.Incident?.Description,
                ["modusOperandi"] = docket.ModusOperandi,
                ["evidenceItemCount"] = docket.Evidences?.Count ?? 0,
                ["investigationNoteCount"] = docket.Investigation?.InvestigationNotes?.Count ?? 0,
                ["source"] = "DDMS structured-record extraction",
                ["nlpModel"] = "Local TF-IDF and extractive summarisation v1",
                ["requiresHumanVerification"] = true
            };

            _db.AIExtractions.Add(new AIExtraction
            {
                FK_DocketID = docketId,
                ExtractedData = JsonSerializer.Serialize(extraction),
                IsVerified = false,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return extraction;
        }

        public async Task<IEnumerable<(int relatedDocketId, double score, string reason)>> SuggestRelatedCasesAsync(int docketId, int maxResults = 5)
        {
            maxResults = Math.Clamp(maxResults, 1, 20);
            var baseDocket = await _db.Dockets
                .Include(item => item.Incident).ThenInclude(incident => incident.IncidentType)
                .Include(item => item.Incident).ThenInclude(incident => incident.GeoLocation)
                .FirstOrDefaultAsync(item => item.DocketID == docketId);
            if (baseDocket == null)
                return Enumerable.Empty<(int relatedDocketId, double score, string reason)>();

            var candidates = await _db.Dockets
                .Include(item => item.Incident).ThenInclude(incident => incident.IncidentType)
                .Include(item => item.Incident).ThenInclude(incident => incident.GeoLocation)
                .Where(item => item.DocketID != docketId && item.FK_StationID == baseDocket.FK_StationID)
                .OrderByDescending(item => item.IncidentDate)
                .Take(100)
                .ToListAsync();

            var semanticScores = CalculateSemanticSimilarities(baseDocket, candidates);
            return candidates.Select(candidate => ScoreCandidate(
                    baseDocket,
                    candidate,
                    semanticScores.GetValueOrDefault(candidate.DocketID)))
                .Where(item => item.score >= 0.35)
                .OrderByDescending(item => item.score)
                .ThenBy(item => item.relatedDocketId)
                .Take(maxResults)
                .ToList();
        }

        public async Task<bool> VerifyExtractionsAsync(int docketId, object verifiedValues, int userId)
        {
            var extraction = await _db.AIExtractions
                .Where(item => item.FK_DocketID == docketId && !item.IsVerified)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync();
            if (extraction == null) return false;

            extraction.ExtractedData = JsonSerializer.Serialize(verifiedValues);
            extraction.IsVerified = true;
            await _db.SaveChangesAsync();
            return true;
        }

        private IQueryable<Docket> DocketQuery()
        {
            return _db.Dockets
                .Include(item => item.DocketStatus)
                .Include(item => item.Station)
                .Include(item => item.Incident).ThenInclude(incident => incident.IncidentType)
                .Include(item => item.Incident).ThenInclude(incident => incident.GeoLocation)
                .Include(item => item.Evidences)
                .Include(item => item.Investigation).ThenInclude(investigation => investigation.InvestigationNotes);
        }

        private static (int relatedDocketId, double score, string reason) ScoreCandidate(
            Docket source,
            Docket candidate,
            double semanticSimilarity)
        {
            var score = 0.35;
            var reasons = new List<string> { "same station" };

            if (source.Incident?.FK_IncidentTypeID.HasValue == true &&
                source.Incident.FK_IncidentTypeID == candidate.Incident?.FK_IncidentTypeID)
            {
                score += 0.3;
                reasons.Add("same incident type");
            }

            var sourceArea = source.Incident?.GeoLocation?.AreaName;
            var candidateArea = candidate.Incident?.GeoLocation?.AreaName;
            if (!string.IsNullOrWhiteSpace(sourceArea) &&
                string.Equals(sourceArea, candidateArea, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.2;
                reasons.Add("same area");
            }

            var dayDifference = Math.Abs((source.IncidentDate - candidate.IncidentDate).TotalDays);
            if (dayDifference <= 30)
            {
                score += 0.15;
                reasons.Add("incident dates within 30 days");
            }
            else if (dayDifference <= 90)
            {
                score += 0.08;
                reasons.Add("incident dates within 90 days");
            }

            if (semanticSimilarity > 0)
            {
                score += semanticSimilarity * 0.3;
                reasons.Add($"TF-IDF narrative similarity {semanticSimilarity:P0}");
            }

            return (candidate.DocketID, Math.Round(Math.Min(1, score), 2), string.Join(", ", reasons));
        }

        private static Dictionary<int, double> CalculateSemanticSimilarities(
            Docket source,
            IReadOnlyCollection<Docket> candidates)
        {
            var documents = new List<(int Id, List<string> Tokens)>
            {
                (source.DocketID, Tokens(CompositeNarrative(source)))
            };
            documents.AddRange(candidates.Select(candidate =>
                (candidate.DocketID, Tokens(CompositeNarrative(candidate)))));

            var documentFrequency = documents
                .SelectMany(document => document.Tokens.Distinct())
                .GroupBy(token => token)
                .ToDictionary(group => group.Key, group => group.Count());
            var vectors = documents.ToDictionary(
                document => document.Id,
                document => TfIdfVector(document.Tokens, documentFrequency, documents.Count));
            var sourceVector = vectors[source.DocketID];

            return candidates.ToDictionary(
                candidate => candidate.DocketID,
                candidate => Math.Round(Cosine(sourceVector, vectors[candidate.DocketID]), 4));
        }

        private static Dictionary<string, double> TfIdfVector(
            IReadOnlyCollection<string> tokens,
            IReadOnlyDictionary<string, int> documentFrequency,
            int documentCount)
        {
            if (tokens.Count == 0) return new Dictionary<string, double>();
            return tokens.GroupBy(token => token).ToDictionary(
                group => group.Key,
                group =>
                {
                    var termFrequency = (double)group.Count() / tokens.Count;
                    var inverseDocumentFrequency = Math.Log(
                        (documentCount + 1d) / (documentFrequency[group.Key] + 1d)) + 1d;
                    return termFrequency * inverseDocumentFrequency;
                });
        }

        private static double Cosine(
            IReadOnlyDictionary<string, double> left,
            IReadOnlyDictionary<string, double> right)
        {
            if (left.Count == 0 || right.Count == 0) return 0;
            var dotProduct = left.Sum(item => item.Value * right.GetValueOrDefault(item.Key));
            var leftLength = Math.Sqrt(left.Values.Sum(value => value * value));
            var rightLength = Math.Sqrt(right.Values.Sum(value => value * value));
            return leftLength == 0 || rightLength == 0
                ? 0
                : Math.Clamp(dotProduct / (leftLength * rightLength), 0, 1);
        }

        private static string BuildExtractiveSummary(Docket docket)
        {
            var narrative = string.Join(" ", new[]
            {
                docket.DocketSummary,
                docket.Incident?.Description,
                docket.ModusOperandi,
                string.Join(" ", docket.Investigation?.InvestigationNotes?
                    .OrderByDescending(note => note.EntryDate)
                    .Take(5)
                    .Select(note => note.Description) ?? Array.Empty<string>())
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(narrative))
                return "No narrative information has been captured.";

            var sentences = Regex.Split(narrative.Trim(), @"(?<=[.!?])\s+|[\r\n]+")
                .Select((text, index) => new { Text = text.Trim(), Index = index })
                .Where(sentence => sentence.Text.Length >= 12)
                .Take(40)
                .ToList();
            if (sentences.Count == 0)
                return narrative.Length <= 500 ? narrative : narrative[..497] + "...";

            var frequencies = Tokens(narrative)
                .GroupBy(token => token)
                .ToDictionary(group => group.Key, group => group.Count());
            var selected = sentences
                .Select(sentence => new
                {
                    sentence.Text,
                    sentence.Index,
                    Score = Tokens(sentence.Text).Distinct().Sum(token => frequencies.GetValueOrDefault(token))
                })
                .OrderByDescending(sentence => sentence.Score)
                .ThenBy(sentence => sentence.Index)
                .Take(3)
                .OrderBy(sentence => sentence.Index)
                .Select(sentence => sentence.Text);
            var summary = string.Join(" ", selected);
            return summary.Length <= 600 ? summary : summary[..597] + "...";
        }

        private static string CompositeNarrative(Docket docket)
        {
            return string.Join(" ", new[]
            {
                docket.DocketSummary,
                docket.ModusOperandi,
                docket.CaseType,
                docket.Incident?.Description,
                docket.Incident?.IncidentType?.TypeName,
                docket.Incident?.GeoLocation?.AreaName
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static List<string> Tokens(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<string>();
            return Regex.Matches(value.ToLowerInvariant(), @"[a-z0-9]+")
                .Select(match => match.Value)
                .Where(token => token.Length >= 3 && !StopWords.Contains(token))
                .ToList();
        }

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "that", "with", "from", "this", "was", "were", "are", "has",
            "have", "had", "not", "but", "into", "onto", "over", "under", "then", "than", "when",
            "where", "which", "their", "there", "they", "them", "his", "her", "its", "our", "you"
        };
    }
}
