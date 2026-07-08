import { Injectable, InjectionToken, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AgentEvent } from './agent-events';

/** Base URL of the .NET host, e.g. 'http://localhost:5000'. Defaults to same-origin. */
export const AGENT_API_BASE_URL = new InjectionToken<string>('AGENT_API_BASE_URL', {
  factory: () => '',
});

/**
 * Streams agent events from the dotnet/streaming block's endpoint.
 * Uses fetch + a stream reader because EventSource cannot send a POST body.
 */
@Injectable({ providedIn: 'root' })
export class AgentChatService {
  private readonly baseUrl = inject(AGENT_API_BASE_URL);

  streamChat(agentName: string, message: string, conversationId?: string | null): Observable<AgentEvent> {
    return new Observable<AgentEvent>((subscriber) => {
      const abort = new AbortController();

      (async () => {
        const response = await fetch(`${this.baseUrl}/agents/${encodeURIComponent(agentName)}/chat`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ message, conversationId: conversationId ?? null }),
          signal: abort.signal,
        });
        if (!response.ok || !response.body) {
          throw new Error(`Chat request failed: ${response.status} ${response.statusText}`);
        }

        const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
        let buffer = '';
        for (;;) {
          const { done, value } = await reader.read();
          if (done) {
            break;
          }
          buffer += value;

          // SSE messages are separated by a blank line; data lines start with "data:".
          let separatorIndex: number;
          while ((separatorIndex = buffer.indexOf('\n\n')) >= 0) {
            const rawMessage = buffer.slice(0, separatorIndex);
            buffer = buffer.slice(separatorIndex + 2);

            const data = rawMessage
              .split('\n')
              .filter((line) => line.startsWith('data:'))
              .map((line) => line.slice(5).trim())
              .join('\n');
            if (data) {
              subscriber.next(JSON.parse(data) as AgentEvent);
            }
          }
        }
        subscriber.complete();
      })().catch((err) => {
        if (!abort.signal.aborted) {
          subscriber.error(err);
        }
      });

      return () => abort.abort();
    });
  }
}
