export interface Message {
  role: 'user' | 'assistant';
  content: string;
  streaming?: boolean;
}