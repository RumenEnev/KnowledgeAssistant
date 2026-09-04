import { Message } from './message';

export interface Conversation {
  id: string;
  title: string;
  messages: Message[];
  selectedProvider?: string;
  selectedModel?: string;
  topicId?: number;
  topic?: string;
}