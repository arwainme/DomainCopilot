using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Agents;

public sealed class ResponseDrafter : IResponseDrafter
{
    private readonly ILlmProvider _llmProvider;

    public ResponseDrafter(ILlmProvider llmProvider)
    {
        _llmProvider = llmProvider;
    }

    public async Task<AgentResult<DraftResponse>> ExecuteAsync(
        CitizenQuery query,
        EligibilityResult eligibility,
        ProcedureResult procedure,
        IReadOnlyCollection<EvidenceChunk> evidence,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!eligibility.IsSupported)
        {
            return new AgentResult<DraftResponse>(
                false,
                null,
                "A response cannot be drafted because eligibility could not be supported by the available evidence.");
        }

        if (!procedure.IsSupported)
        {
            return new AgentResult<DraftResponse>(
                false,
                null,
                "A response cannot be drafted because the procedure could not be supported by the available evidence.");
        }

        var meaningfulEvidence = evidence
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Content))
            .ToList();

        if (meaningfulEvidence.Count == 0)
        {
            return new AgentResult<DraftResponse>(
                false,
                null,
                "A response cannot be drafted because supporting evidence is insufficient.");
        }

        var citations = meaningfulEvidence
            .Select(x => new Citation(
                x.DocumentId,
                x.ChunkId,
                x.DocumentTitle,
                x.PageNumber))
            .ToArray();

        var evidenceText = string.Join(
            Environment.NewLine + Environment.NewLine,
            meaningfulEvidence.Select(x =>
                $"[Source: {x.DocumentTitle}, Page: {x.PageNumber}]\n{x.Content}"));

        var procedureText = string.Join(
            Environment.NewLine,
            procedure.Steps.Select(
                (step, index) => $"{index + 1}. {step}"));

        var prompt = $"""
            You are a government response drafting agent.

            Your job is ONLY to draft a cautious citizen-facing response
            using the supplied procedure and evidence.

            STRICT RULES:
            - Do not invent fees, deadlines, eligibility requirements,
              documents, rights, obligations, or entitlements.
            - Do not add facts that are not present in the evidence.
            - If the evidence is insufficient for a specific fact, say so.
            - Do not make a final eligibility or entitlement decision.
            - The response must clearly state that it requires officer approval.
            - Keep the response professional and concise.

            Citizen question:
            {query.Situation}

            Eligibility assessment:
            {eligibility.Explanation}

            Supported procedure:
            {procedureText}

            Retrieved evidence:
            {evidenceText}

            Draft the citizen-facing response now.
            """;

        string responseText;

        try
        {
            responseText = await _llmProvider.CompleteAsync(
                prompt,
                cancellationToken);
        }
        catch
        {
            responseText = BuildFallbackResponse(
                query,
                procedure,
                meaningfulEvidence);
        }

        if (string.IsNullOrWhiteSpace(responseText))
        {
            responseText = BuildFallbackResponse(
                query,
                procedure,
                meaningfulEvidence);
        }

        var response = $"""
            {responseText.Trim()}

            This response is a draft and requires officer approval before being
            provided as an official response.
            """;

        var draft = new DraftResponse(
            response,
            citations,
            RequiresOfficerApproval: true);

        return new AgentResult<DraftResponse>(
            true,
            draft,
            null);
    }

    private static string BuildFallbackResponse(
        CitizenQuery query,
        ProcedureResult procedure,
        IReadOnlyCollection<EvidenceChunk> evidence)
    {
        var combinedEvidence = string.Join(
            " ",
            evidence.Select(x => x.Content));

        var documentLines = ExtractRequiredDocuments(combinedEvidence);

        var fee = ExtractSentence(
            combinedEvidence,
            "The applicable service fee",
            "Processing Timeline");

        var timeline = ExtractSentence(
            combinedEvidence,
            "The expected processing time",
            "Important");

        var responseLines = new List<string>
        {
            $"Regarding your request: \"{query.Situation}\"",
            "",
            "Based on the available government-service evidence:"
        };

        if (documentLines.Count > 0)
        {
            responseLines.Add("");
            responseLines.Add("Required documents:");

            foreach (var document in documentLines)
            {
                responseLines.Add($"- {document}");
            }

            responseLines.Add("");
            responseLines.Add(
                "The required documents may vary depending on the requested " +
                "service and the applicant's circumstances.");
        }

        if (!string.IsNullOrWhiteSpace(fee))
        {
            responseLines.Add("");
            responseLines.Add($"Fees: {fee}");
        }

        if (!string.IsNullOrWhiteSpace(timeline))
        {
            responseLines.Add("");
            responseLines.Add($"Processing time: {timeline}");
        }

        if (documentLines.Count == 0 &&
            string.IsNullOrWhiteSpace(fee) &&
            string.IsNullOrWhiteSpace(timeline))
        {
            responseLines.Add("");
            responseLines.Add(
                "The available evidence does not provide sufficient " +
                "specific information to answer the request.");
        }

        return string.Join(Environment.NewLine, responseLines);
    }

    private static List<string> ExtractRequiredDocuments(string content)
    {
        var documents = new List<string>();

        AddIfPresent(
            documents,
            content,
            "Valid identification document",
            "Completed service request form");

        AddIfPresent(
            documents,
            content,
            "Completed service request form",
            "Supporting documents");

        AddIfPresent(
            documents,
            content,
            "Supporting documents relevant to the requested service",
            "Fees");

        return documents
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddIfPresent(
        ICollection<string> results,
        string content,
        string value,
        string nextMarker)
    {
        var start = content.IndexOf(
            value,
            StringComparison.OrdinalIgnoreCase);

        if (start < 0)
        {
            return;
        }

        results.Add(value);
    }

    private static string? ExtractSentence(
        string content,
        string startMarker,
        string endMarker)
    {
        var start = content.IndexOf(
            startMarker,
            StringComparison.OrdinalIgnoreCase);

        if (start < 0)
        {
            return null;
        }

        var end = content.IndexOf(
            endMarker,
            start + startMarker.Length,
            StringComparison.OrdinalIgnoreCase);

        var length = end >= 0
            ? end - start
            : content.Length - start;

        var result = content.Substring(start, length).Trim();

        return string.IsNullOrWhiteSpace(result)
            ? null
            : result;
    }
}