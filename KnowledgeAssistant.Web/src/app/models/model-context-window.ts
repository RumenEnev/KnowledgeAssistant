export interface ModelContextWindow {
  id: string;
  name: string;
  size: number;
  parameterSize: string | null;
  contextLength: number | null;
  family: string | null;
  quantizationLevel: string | null;
  internalUseOnly: boolean;
  canCallTools: boolean;
}

export interface UpdateModelContextWindow {
  internalUseOnly: boolean;
  canCallTools: boolean;
}
