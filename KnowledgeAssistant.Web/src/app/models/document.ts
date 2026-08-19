export interface DocumentItem {
  id: number;
  title: string;
  originalText: string;
  createdAt: string;
  topics: string[];
}

export interface Topic {
  id: number;
  name: string;
  parentId: number | null;
}