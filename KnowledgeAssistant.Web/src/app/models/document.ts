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
  candidatePoolSize: number;
  candidateFanout: number;
  maxDistanceThreshold: number;
  rrfK: number;
  targetInjectionFraction: number;
  maxInjectionFraction: number;
}

export const DEFAULT_RETRIEVAL_CONFIG: Omit<DocumentRetrievalConfig, 'documentId'> = {
  chunkSize: 1200,
  chunkOverlap: 200,
  candidatePoolSize: 5,
  candidateFanout: 20,
  maxDistanceThreshold: 0.5,
  rrfK: 60,
  targetInjectionFraction: 0.30,
  maxInjectionFraction: 0.50
};