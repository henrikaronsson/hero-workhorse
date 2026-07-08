/**
 * TypeScript mirror of the .NET agent-loop event model.
 * Each SSE `data:` line from the streaming block is one of these, discriminated by `type`.
 */

export interface TextDelta {
  type: 'text-delta';
  text: string;
}

export interface ToolCallStarted {
  type: 'tool-call-started';
  callId: string;
  name: string;
  argumentsJson: string;
}

export interface ToolResult {
  type: 'tool-result';
  callId: string;
  name: string;
  resultJson: string;
  isError: boolean;
}

export interface Completed {
  type: 'completed';
  stopReason: 'done' | 'max-steps' | 'token-budget' | 'cancelled';
  steps: number;
  inputTokens: number | null;
  outputTokens: number | null;
}

export interface AgentError {
  type: 'error';
  message: string;
}

/** Sent first by hosts that persist conversations; echo the id back on follow-up turns. */
export interface ConversationStarted {
  type: 'conversation-started';
  conversationId: string;
}

export type AgentEvent =
  | TextDelta
  | ToolCallStarted
  | ToolResult
  | Completed
  | AgentError
  | ConversationStarted;
