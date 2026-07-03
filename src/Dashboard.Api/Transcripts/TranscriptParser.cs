using System.Text.Json;

namespace Dashboard.Api.Transcripts;

internal sealed class TranscriptParser : ITranscriptParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public IReadOnlyList<TranscriptLine> Parse(string filePath)
    {
        var result = new List<TranscriptLine>();

        foreach (var rawLine in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            TranscriptLine? line;
            try
            {
                line = JsonSerializer.Deserialize<TranscriptLine>(rawLine, Options);
            }
            catch (JsonException)
            {
                // Malformed / unparseable lines are skipped so the rest of the
                // transcript can still be analyzed (see spec: "malformed
                // lines skipped, totals from remaining").
                continue;
            }

            if (line is not null)
            {
                result.Add(line);
            }
        }

        return result;
    }
}
