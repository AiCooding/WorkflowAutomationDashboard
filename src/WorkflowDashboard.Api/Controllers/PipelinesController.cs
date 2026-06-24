using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/pipelines")]
public class PipelinesController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<PipelinesController> _logger;

    public PipelinesController(WorkflowDbContext db, ILogger<PipelinesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<Pipeline>>> GetAll()
    {
        return await _db.Pipelines.OrderByDescending(p => p.UpdatedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Pipeline>> GetById(string id)
    {
        var pipeline = await _db.Pipelines.FindAsync(id);
        if (pipeline is null) return NotFound();
        return pipeline;
    }

    [HttpPost]
    public async Task<ActionResult<Pipeline>> Create([FromBody] CreatePipelineBody body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest("Pipeline name is required.");

        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = body.Name.Trim(),
            Description = body.Description,
            StepsJson = string.IsNullOrWhiteSpace(body.StepsJson) ? "{}" : body.StepsJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Pipelines.Add(pipeline);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created pipeline {PipelineId} named {PipelineName}.", pipeline.Id, pipeline.Name);
        return CreatedAtAction(nameof(GetById), new { id = pipeline.Id }, pipeline);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Pipeline>> Update(string id, [FromBody] CreatePipelineBody body)
    {
        var pipeline = await _db.Pipelines.FindAsync(id);
        if (pipeline is null) return NotFound();
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest("Pipeline name is required.");

        pipeline.Name = body.Name.Trim();
        pipeline.Description = body.Description;
        pipeline.StepsJson = body.StepsJson ?? pipeline.StepsJson;
        pipeline.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated pipeline {PipelineId}.", id);
        return pipeline;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var pipeline = await _db.Pipelines.FindAsync(id);
        if (pipeline is null) return NotFound();
        _db.Pipelines.Remove(pipeline);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted pipeline {PipelineId}.", id);
        return NoContent();
    }

    [HttpGet("{id}/export")]
    public async Task<IActionResult> Export(string id)
    {
        var pipeline = await _db.Pipelines.FindAsync(id);
        if (pipeline is null) return NotFound();

        var export = new PipelineExportDto(
            pipeline.Name,
            pipeline.Description,
            pipeline.StepsJson);

        _logger.LogDebug("Exported pipeline {PipelineId}.", id);
        return new JsonResult(export);
    }

    [HttpPost("import")]
    public async Task<ActionResult<Pipeline>> Import([FromBody] PipelineExportDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Pipeline name is required.");

        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = dto.Name.Trim(),
            Description = dto.Description,
            StepsJson = string.IsNullOrWhiteSpace(dto.StepsJson) ? "{}" : dto.StepsJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Pipelines.Add(pipeline);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Imported pipeline {PipelineId} named {PipelineName}.", pipeline.Id, pipeline.Name);
        return CreatedAtAction(nameof(GetById), new { id = pipeline.Id }, pipeline);
    }
}

public record CreatePipelineBody(string Name, string? Description, string? StepsJson);
public record PipelineExportDto(string Name, string? Description, string? StepsJson);
