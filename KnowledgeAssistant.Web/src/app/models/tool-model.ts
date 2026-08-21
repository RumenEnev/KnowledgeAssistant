export interface ToolModel {
  id: string;
  name: string;
  description: string;
  parametersJsonSchema: string;
  isEnabled: boolean;
  path?: string | null;
}