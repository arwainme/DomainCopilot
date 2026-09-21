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
            meaningfulEvidence.Select((x, index) =>
                $"[Evidence {index + 1}]\nSource: {x.DocumentTitle}\nLocation: {x.PageNumber}\n{x.Content}"));

        var procedureText = string.Join(
            Environment.NewLine,
            procedure.Steps.Select(
                (step, index) => $"{index + 1}. {step}"));

        var questionType = DetectQuestionType(query.Situation);

        var prompt = $"""
            You are a government response drafting agent.

            Your task is to answer the citizen's specific question using ONLY
            the supplied government evidence and supported workflow context.

            STRICT RULES:
            - Answer the citizen's actual question directly.
            - Do not use a generic government-service template.
            - Do not answer unrelated topics unless they are necessary to answer the question.
            - Do not copy large passages from the evidence.
            - Summarize the evidence in your own words.
            - Do not invent fees, deadlines, eligibility requirements, documents,
              rights, obligations, or entitlements.
            - When the evidence does not establish a fact, explicitly say that
              the available evidence does not specify or is insufficient.
            - Never make a final eligibility or entitlement decision.
            - For a yes/no question, give the supported yes/no answer first.
            - Keep the answer concise and citizen-facing.
            - End with a short statement that the response requires officer approval.

            Detected question focus:
            {questionType}

            Citizen question:
            {query.Situation}

            Eligibility context:
            Supported: {eligibility.IsSupported}
            Explanation: {eligibility.Explanation}

            Procedure context:
            Supported: {procedure.IsSupported}
            Steps:
            {procedureText}

            Retrieved evidence:
            {evidenceText}

            Draft ONLY the answer to the citizen's question.
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
                eligibility,
                procedure,
                meaningfulEvidence);
        }

        if (string.IsNullOrWhiteSpace(responseText))
        {
            responseText = BuildFallbackResponse(
                query,
                eligibility,
                procedure,
                meaningfulEvidence);
        }

        responseText = EnsureApprovalNotice(responseText);

        var draft = new DraftResponse(
            responseText,
            citations,
            RequiresOfficerApproval: true);

        return new AgentResult<DraftResponse>(
            true,
            draft,
            null);
    }

    private static string BuildFallbackResponse(
        CitizenQuery query,
        EligibilityResult eligibility,
        ProcedureResult procedure,
        IReadOnlyCollection<EvidenceChunk> evidence)
    {
        var combinedEvidence = string.Join(
            " ",
            evidence.Select(x => x.Content));

        var questionType = DetectQuestionType(query.Situation);

        return questionType switch
        {
            "documents" => BuildDocumentsResponse(query, combinedEvidence),
            "fees" => BuildFeeResponse(combinedEvidence),
            "timeline" => BuildTimelineResponse(combinedEvidence),
            "procedure" => BuildProcedureResponse(procedure),
            "eligibility" => BuildEligibilityResponse(eligibility),
            _ => BuildGeneralResponse(combinedEvidence)
        };
    }

    private static string BuildDocumentsResponse(
        CitizenQuery query,
        string content)
    {
        var documents = ExtractRequiredDocuments(content);

        if (documents.Count == 0)
        {
            return "The available evidence does not specify the documents required for this request.";
        }

        var asksAboutIdentification =
            ContainsAny(
                query.Situation,
                "identification",
                "id document",
                "id card",
                "identity document",
                "identification document");

        if (asksAboutIdentification)
        {
            var hasIdentification = documents.Any(document =>
                document.Contains(
                    "identification",
                    StringComparison.OrdinalIgnoreCase));

            return hasIdentification
                ? "Yes. The available evidence states that applicants must provide a valid identification document."
                : "The available evidence does not establish that an identification document is required.";
        }

        var lines = new List<string>
        {
            "The available evidence lists these required documents:"
        };

        lines.AddRange(
            documents.Select(document => $"- {document}"));

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static string BuildFeeResponse(string content)
    {
        var fee = ExtractSentence(
            content,
            "The applicable service fee",
            "Processing Timeline");

        return !string.IsNullOrWhiteSpace(fee)
            ? fee
            : "The available evidence does not specify an exact service fee.";
    }

    private static string BuildTimelineResponse(string content)
    {
        var timeline = ExtractSentence(
            content,
            "The expected processing time",
            "Important");

        return !string.IsNullOrWhiteSpace(timeline)
            ? timeline
            : "The available evidence does not specify an exact processing time.";
    }

    private static string BuildProcedureResponse(
        ProcedureResult procedure)
    {
        if (!procedure.IsSupported ||
            procedure.Steps.Count == 0)
        {
            return "The available evidence is insufficient to provide the procedure steps.";
        }

        var lines = new List<string>
        {
            "The available procedure is:"
        };

        lines.AddRange(
            procedure.Steps.Select(
                (step, index) => $"{index + 1}. {step}"));

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static string BuildEligibilityResponse(
        EligibilityResult eligibility)
    {
        if (string.IsNullOrWhiteSpace(
                eligibility.Explanation))
        {
            return "The available evidence is insufficient to determine eligibility.";
        }

        return eligibility.Explanation;
    }

    private static string BuildGeneralResponse(
        string content)
    {
        var documents = ExtractRequiredDocuments(content);

        var fee = ExtractSentence(
            content,
            "The applicable service fee",
            "Processing Timeline");

        var timeline = ExtractSentence(
            content,
            "The expected processing time",
            "Important");

        var lines = new List<string>
        {
            "The available government-service evidence provides the following information:"
        };

        if (documents.Count > 0)
        {
            lines.Add("Required documents:");

            lines.AddRange(
                documents.Select(
                    document => $"- {document}"));
        }

        if (!string.IsNullOrWhiteSpace(fee))
        {
            lines.Add($"Fees: {fee}");
        }

        if (!string.IsNullOrWhiteSpace(timeline))
        {
            lines.Add($"Processing time: {timeline}");
        }

        if (lines.Count == 1)
        {
            lines.Add(
                "The available evidence does not provide enough specific information to answer the request.");
        }

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static List<string> ExtractRequiredDocuments(
        string content)
    {
        var documents = new List<string>();

        AddIfPresent(
            documents,
            content,
            "Valid identification document");

        AddIfPresent(
            documents,
            content,
            "Completed service request form");

        AddIfPresent(
            documents,
            content,
            "Supporting documents relevant to the requested service");

        return documents
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddIfPresent(
        ICollection<string> results,
        string content,
        string value)
    {
        if (content.Contains(
                value,
                StringComparison.OrdinalIgnoreCase))
        {
            results.Add(value);
        }
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

        var result = content
            .Substring(start, length)
            .Trim();

        return string.IsNullOrWhiteSpace(result)
            ? null
            : result;
    }

    private static string DetectQuestionType(
        string question)
    {
        if (ContainsAny(
                question,
                "document",
                "documents",
                "paper",
                "papers",
                "paperwork",
                "identification",
                "id document",
                "form"))
        {
            return "documents";
        }

        if (ContainsAny(
                question,
                "fee",
                "fees",
                "cost",
                "price",
                "charge",
                "pay",
                "payment"))
        {
            return "fees";
        }

        if (ContainsAny(
                question,
                "how long",
                "processing time",
                "processing timeline",
                "deadline",
                "duration",
                "how many days",
                "take"))
        {
            return "timeline";
        }

        if (ContainsAny(
                question,
                "step",
                "steps",
                "procedure",
                "process",
                "apply",
                "application",
                "submit",
                "how do i"))
        {
            return "procedure";
        }

        if (ContainsAny(
                question,
                "eligible",
                "eligibility",
                "who can apply",
                "can i apply",
                "qualify",
                "qualification"))
        {
            return "eligibility";
        }

        return "general";
    }

    private static bool ContainsAny(
        string text,
        params string[] values)
    {
        return values.Any(value =>
            text.Contains(
                value,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string EnsureApprovalNotice(
        string response)
    {
        const string approvalNotice =
            "This response is a draft and requires officer approval before being provided as an official response.";

        if (response.Contains(
                "requires officer approval",
                StringComparison.OrdinalIgnoreCase))
        {
            return response.Trim();
        }

        return $"{response.Trim()}\n\n{approvalNotice}";
    }
}
