import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ToolTraceComponent } from '../tool-trace/tool-trace.component';
import { ToolCall } from '../tool-trace/tool-call';
import { AgentChatService } from './agent-chat.service';

export interface ChatTurn {
  role: 'user' | 'assistant';
  text: string;
  toolCalls: ToolCall[];
  error?: string;
}

/**
 * Message list + input, rendering streamed agent responses as they arrive.
 * Each assistant turn gets a collapsible tool trace (remove the hw-tool-trace
 * element and its import if you copied this block without tool-trace).
 */
@Component({
  selector: 'hw-chat',
  imports: [ToolTraceComponent],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChatComponent {
  readonly agentName = input.required<string>();
  readonly placeholder = input('Ask a question…');

  readonly turns = signal<ChatTurn[]>([]);
  readonly busy = signal(false);
  readonly draft = signal('');

  private readonly chatService = inject(AgentChatService);
  private readonly destroyRef = inject(DestroyRef);
  private conversationId: string | null = null;

  send(): void {
    const message = this.draft().trim();
    if (!message || this.busy()) {
      return;
    }

    this.draft.set('');
    this.busy.set(true);
    this.turns.update((turns) => [
      ...turns,
      { role: 'user', text: message, toolCalls: [] },
      { role: 'assistant', text: '', toolCalls: [] },
    ]);

    this.chatService
      .streamChat(this.agentName(), message, this.conversationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (event) => {
          switch (event.type) {
            case 'text-delta':
              this.updateCurrent((turn) => ({ ...turn, text: turn.text + event.text }));
              break;
            case 'tool-call-started':
              this.updateCurrent((turn) => ({
                ...turn,
                toolCalls: [
                  ...turn.toolCalls,
                  { callId: event.callId, name: event.name, argumentsJson: event.argumentsJson },
                ],
              }));
              break;
            case 'tool-result':
              this.updateCurrent((turn) => ({
                ...turn,
                toolCalls: turn.toolCalls.map((call) =>
                  call.callId === event.callId
                    ? { ...call, resultJson: event.resultJson, isError: event.isError }
                    : call,
                ),
              }));
              break;
            case 'conversation-started':
              this.conversationId = event.conversationId;
              break;
            case 'error':
              this.updateCurrent((turn) => ({ ...turn, error: event.message }));
              break;
            case 'completed':
              if (event.stopReason !== 'done') {
                this.updateCurrent((turn) => ({ ...turn, error: `Stopped: ${event.stopReason}` }));
              }
              break;
          }
        },
        error: (err: unknown) => {
          this.updateCurrent((turn) => ({ ...turn, error: err instanceof Error ? err.message : String(err) }));
          this.busy.set(false);
        },
        complete: () => this.busy.set(false),
      });
  }

  /** Replaces the assistant turn currently being streamed (always the last one). */
  private updateCurrent(update: (turn: ChatTurn) => ChatTurn): void {
    this.turns.update((turns) => {
      const next = [...turns];
      next[next.length - 1] = update(next[next.length - 1]);
      return next;
    });
  }
}
