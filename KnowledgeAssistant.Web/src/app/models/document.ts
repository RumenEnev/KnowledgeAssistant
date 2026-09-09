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

export interface DocumentRetrievalConfig {
  documentId: number;
  chunkSize: number;
  chunkOverlap: number;
}

export const DEFAULT_RETRIEVAL_CONFIG: Omit<DocumentRetrievalConfig, 'documentId'> = {
  chunkSize: 1200,
  chunkOverlap: 200,
};