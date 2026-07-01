export const SseEvents = {
  ConversationCreated: 'conversation-created',
  ConversationUpdated: 'conversation-updated',
  MessageCreated: 'message-created',
  MessageCompleted: 'message-completed',
  Token: 'token',
  Done: 'done',
  Error: 'error'
} as const;

export type SseEventType = typeof SseEvents[keyof typeof SseEvents];