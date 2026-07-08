/** One tool invocation within an assistant response, built up from agent events. */
export interface ToolCall {
  callId: string;
  name: string;
  argumentsJson: string;
  resultJson?: string;
  isError?: boolean;
}
