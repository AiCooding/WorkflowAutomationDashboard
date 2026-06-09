using System.Net.Http.Json;

namespace WorkflowDashboard.Shared;

/// <summary>
/// HTTP client for agents to report state to the Workflow Dashboard API.
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

    // Agent operations
    public async Task<AgentDto> RegisterAgent(string workflowId, string agentType, string? sessionId = null)
    {
        var agent = new AgentDto
        {
            WorkflowId = workflowId,
            AgentType = agentType,
            SessionId = sessionId
        };

        var response = await _http.PostAsJsonAsync("/api/agents", agent);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentDto>())!;
    }

    public async Task UpdateAgentStatus(string agentId, string status, string? currentTask = null)
    {
        var update = new { Status = status, CurrentTask = currentTask };
        var response = await _http.PutAsJsonAsync($"/api/agents/{agentId}", update);
        response.EnsureSuccessStatusCode();
    }

    // Workflow operations
    public async Task<WorkflowDto> CreateWorkflow(string type, string? featureId = null)
    {
        var workflow = new WorkflowDto { Type = type, FeatureId = featureId };
        var response = await _http.PostAsJsonAsync("/api/workflows", workflow);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkflowDto>())!;
    }

    public async Task UpdateWorkflowStatus(string workflowId, string status, string? errorMessage = null, string? featureId = null)
    {
        var update = new { Status = status, ErrorMessage = errorMessage, FeatureId = featureId };
        var response = await _http.PutAsJsonAsync($"/api/workflows/{workflowId}/status", update);
        response.EnsureSuccessStatusCode();
    }

    // Features
    public async Task<FeatureDto> CreateFeature(string name, string? description = null, string status = "planning", int priority = 0)
    {
        var feature = new FeatureDto { Name = name, Description = description, Status = status, Priority = priority };
        var response = await _http.PostAsJsonAsync("/api/features", feature);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FeatureDto>())!;
    }

    /// <summary>
    /// Persists the markdown spec for a feature. The dashboard writes the file to its configured spec root
    /// (defaults to docs/features) and updates Feature.SpecPath.
    /// </summary>
    public async Task<FeatureSpecDto> SaveFeatureSpec(string featureId, string content, string? fileName = null)
    {
        var body = new { Content = content, FileName = fileName };
        var response = await _http.PostAsJsonAsync($"/api/features/{featureId}/spec", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FeatureSpecDto>())!;
    }

    // Input requests
    public async Task<InputRequestDto> RequestInput(string workflowId, string agentId, string question, string[]? options = null)
    {
        var request = new InputRequestDto
        {
            WorkflowId = workflowId,
            AgentId = agentId,
            Question = question,
            OptionsJson = options is not null ? System.Text.Json.JsonSerializer.Serialize(options) : null
        };

        var response = await _http.PostAsJsonAsync("/api/input-requests", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InputRequestDto>())!;
    }

    public async Task<InputRequestDto?> PollForAnswer(string inputRequestId)
    {
        var response = await _http.GetFromJsonAsync<InputRequestDto>($"/api/input-requests/{inputRequestId}");
        return response?.Status == "answered" ? response : null;
    }

    // Events
    public async Task LogEvent(string? workflowId, string? agentId, string eventType, string message)
    {
        var evt = new { WorkflowId = workflowId, AgentId = agentId, EventType = eventType, Message = message };
        await _http.PostAsJsonAsync("/api/events", evt);
    }

    // Commands
    public async Task<List<CommandDto>> PollCommands()
    {
        return await _http.GetFromJsonAsync<List<CommandDto>>("/api/commands?status=pending") ?? [];
    }

    public async Task MarkCommandProcessed(string commandId, string status)
    {
        var update = new { Status = status };
        await _http.PutAsJsonAsync($"/api/commands/{commandId}", update);
    }
}

// DTOs
public class AgentDto
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string AgentType { get; set; } = string.Empty;
    public string Status { get; set; } = "idle";
    public string? CurrentTask { get; set; }
    public string? SessionId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class WorkflowDto
{
    public string Id { get; set; } = string.Empty;
    public string? FeatureId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

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

public class CommandDto
{
    public string Id { get; set; } = string.Empty;
    public string? WorkflowId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public class FeatureDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "backlog";
    public int Priority { get; set; }
    public string? SpecPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FeatureSpecDto
{
    public string FeatureId { get; set; } = string.Empty;
    public string SpecPath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
}
