import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import { ToolCall } from './tool-call';

/**
 * Collapsed-by-default panel listing the tool calls behind an assistant response:
 * which tools ran, with what arguments, and what came back.
 */
@Component({
  selector: 'hw-tool-trace',
  templateUrl: './tool-trace.component.html',
  styleUrl: './tool-trace.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToolTraceComponent {
  readonly toolCalls = input.required<ToolCall[]>();

  readonly expanded = signal(false);

  toggle(): void {
    this.expanded.update((value) => !value);
  }

  prettyJson(json: string | undefined): string {
    if (!json) {
      return '';
    }
    try {
      return JSON.stringify(JSON.parse(json), null, 2);
    } catch {
      return json;
    }
  }
}
