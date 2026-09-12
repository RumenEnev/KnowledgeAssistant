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

/**
 * Global chunking/embedding settings, mirroring the WPF "Retrieval Settings" panel which reads/writes
 * `/api/configuration/chunking-settings` (not the per-document retrieval-config endpoint).
 */
export interface ChunkingSettings {
  embeddingModelName: string;
  chunkTargetSizeChars: number;
  chunkOverlapChars: number;
}

export const DEFAULT_CHUNKING_SETTINGS: ChunkingSettings = {
  embeddingModelName: '',
  chunkTargetSizeChars: 1200,
  chunkOverlapChars: 200,
};