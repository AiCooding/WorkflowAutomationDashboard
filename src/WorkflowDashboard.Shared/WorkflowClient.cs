using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace WorkflowDashboard.Shared;

/// <summary>
/// Slim HTTP client for PM workflows and other agents to communicate with the
/// Workflow Dashboard API.
///
/// Surface (Phase 7):
///   • <see cref="CreateFeatureWithSpecAsync"/> — called by the PM workflow when the user approves the draft.
///   • <see cref="RequestInputAsync"/>          — ask the user a question; returns the request ID.
///   • <see cref="PollForAnswerAsync"/>         — wait up to 5 minutes for the user's answer.
///   • <see cref="LogEventAsync"/>              — emit a structured event to the dashboard.
///
/// Legacy methods (<c>PollCommands</c>, <c>MarkCommandProcessed</c>, <c>RegisterAgent</c>,
/// <c>UpdateAgentStatus</c>, <c>UpdateWorkflowStatus</c>) are deleted; use the runner's
/// injected-instructions mechanism instead (see §1.7 of the architecture plan).
/// </summary>
public class WorkflowClient
{
    private readonly HttpClient _http;

    public WorkflowClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public WorkflowClient(string baseUrl = "http://localhost:5080")
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    // -------------------------------------------------------------------------
    // Features
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by the PM workflow on approval. Creates a feature with an inline spec body.
    /// Maps to <c>POST /api/features</c> with <c>mode:"inline"</c> and an optional
    /// <c>?workflowId=</c> query parameter that links the originating workflow.
    /// </summary>
    public async Task<FeatureDto> CreateFeatureWithSpecAsync(
        string repositoryId,
        string name,
        string description,
        string specSlug,
        string proposalMarkdownBody,
        string? workflowId = null)
    {
        var url = workflowId is not null
            ? $"/api/features?workflowId={Uri.EscapeDataString(workflowId)}"
            : "/api/features";

        var body = new
        {
            repositoryId,
            name,
            description,
            mode = "inline",
            specSlug,
            specBody = proposalMarkdownBody,
        };

        var response = await _http.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FeatureDto>())!;
    }

    // -------------------------------------------------------------------------
    // Input requests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Posts an input request to the dashboard and returns the new request ID.
    /// Maps to <c>POST /api/input-requests</c>.
    /// Use <see cref="PollForAnswerAsync"/> with the returned ID to wait for the answer.
    /// </summary>
    public async Task<string> RequestInputAsync(
        string workflowId,
        string question,
        string[]? options = null)
    {
        var body = new
        {
            workflowId,
            agentId = string.Empty,
            question,
            optionsJson = options is not null ? JsonSerializer.Serialize(options) : null,
        };

        var response = await _http.PostAsJsonAsync("/api/input-requests", body);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<InputRequestDto>())!;
        return result.Id;
    }

    /// <summary>
    /// Polls <c>GET /api/input-requests/{inputRequestId}</c> every 2 seconds until
    /// <c>status == "answered"</c>. Throws <see cref="TimeoutException"/> after 5 minutes.
    /// </summary>
    public async Task<string> PollForAnswerAsync(string workflowId, string inputRequestId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var dto = await _http.GetFromJsonAsync<InputRequestDto>(
                    $"/api/input-requests/{inputRequestId}", cts.Token);

                if (dto?.Status == "answered" && dto.Response is not null)
                    return dto.Response;

                await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                break;
            }
        }
        throw new TimeoutException(
            $"No answer received for input request '{inputRequestId}' within 5 minutes.");
    }

    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Logs a structured event to the workflow event stream.
    /// Maps to <c>POST /api/events</c>.
    /// </summary>
    public async Task LogEventAsync(
        string workflowId,
        string eventType,
        string message,
        object? metadata = null)
    {
        var body = new
        {
            workflowId,
            eventType,
            message,
            metadataJson = metadata is not null ? JsonSerializer.Serialize(metadata) : null,
        };
        await _http.PostAsJsonAsync("/api/events", body);
    }
}

// -------------------------------------------------------------------------
// DTOs
// -------------------------------------------------------------------------

/// <summary>Feature returned by <see cref="WorkflowClient.CreateFeatureWithSpecAsync"/>.</summary>
public class FeatureDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "backlog";
    public int Priority { get; set; }
    public string? SpecFolder { get; set; }
    public string? RepositoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Input-request DTO for <see cref="WorkflowClient.RequestInputAsync"/> and polling.</summary>
public class InputRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string? OptionsJson { get; set; }
    public string? Response { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }
}

// -------------------------------------------------------------------------
// DI registration
// -------------------------------------------------------------------------

/// <summary>DI registration helpers for <see cref="WorkflowClient"/>.</summary>
public static class WorkflowClientExtensions
{
    /// <summary>
    /// Registers a named <see cref="HttpClient"/> for <see cref="WorkflowClient"/>
    /// and adds <see cref="WorkflowClient"/> as a typed transient service.
    /// </summary>
    public static IServiceCollection AddWorkflowClient(
        this IServiceCollection services,
        string baseUrl)
    {
        services.AddHttpClient<WorkflowClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });
        return services;
    }
}
