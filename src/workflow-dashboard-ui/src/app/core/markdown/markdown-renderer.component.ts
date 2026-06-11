import { Component, Input } from '@angular/core';

/**
 * Renders a server-sanitised HTML string via [innerHTML].
 * Angular's DomSanitizer acts as a second line of defence on top of
 * server-side Ganss.Xss / Markdig sanitisation.
 */
@Component({
  selector: 'app-markdown-renderer',
  standalone: true,
  template: `<div class="markdown-body" [innerHTML]="html"></div>`,
  styles: [
    `
      :host { display: block; }
      .markdown-body {
        font-size: 14px;
        line-height: 1.6;
      }
      .markdown-body :first-child { margin-top: 0; }
      .markdown-body h1, .markdown-body h2, .markdown-body h3 {
        margin-top: 24px;
        font-weight: 500;
      }
      .markdown-body pre {
        background: rgba(0, 0, 0, 0.04);
        padding: 12px;
        border-radius: 4px;
        overflow-x: auto;
      }
      .markdown-body code {
        background: rgba(0, 0, 0, 0.05);
        padding: 1px 4px;
        border-radius: 3px;
        font-family: 'Consolas', 'Monaco', monospace;
      }
      .markdown-body pre code {
        background: none;
        padding: 0;
      }
      .markdown-body table {
        border-collapse: collapse;
        margin: 12px 0;
      }
      .markdown-body th, .markdown-body td {
        border: 1px solid rgba(0, 0, 0, 0.12);
        padding: 6px 10px;
      }
      .markdown-body blockquote {
        border-left: 4px solid rgba(0, 0, 0, 0.12);
        padding-left: 12px;
        margin-left: 0;
        color: rgba(0, 0, 0, 0.7);
      }
    `,
  ],
})
export class MarkdownRendererComponent {
  @Input() html = '';
}
