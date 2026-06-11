using Ganss.Xss;
using Markdig;

namespace WorkflowDashboard.Api.Services.Catalog;

/// <summary>
/// Renders markdown to sanitised HTML using Markdig (advanced extensions) +
/// <see cref="HtmlSanitizer"/> (Ganss.Xss) with the default allow-list.
/// No <c>&lt;script&gt;</c>, no inline event handlers, no <c>javascript:</c> URLs.
/// </summary>
public sealed class MarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;
    private readonly HtmlSanitizer _sanitizer;

    public MarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        // Default allow-list. Strip dangerous schemes implicitly.
        _sanitizer = new HtmlSanitizer();
    }

    public string Render(string markdown)
    {
        var html = Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
        return _sanitizer.Sanitize(html);
    }
}
