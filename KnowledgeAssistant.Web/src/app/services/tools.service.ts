import { Injectable } from '@angular/core';
import { ToolModel } from '../models/tool-model';
export type ToolDto = ToolModel;

@Injectable({
  providedIn: 'root'
})
export class ToolsService {
  private baseUrl = '';

  private async assertOk(response: Response): Promise<void> {
    if (response.ok) {
      return;
    }

    let message = `Request failed with status ${response.status}.`;
    const text = await response.text().catch(() => '');
    if (text) {
      try {
        const body = JSON.parse(text);
        message = body?.detail ?? body?.title ?? text;
      } catch {
        message = text;
      }
    }

    throw new Error(message);
  }

  async getTools(): Promise<ToolDto[]> {
    const response = await fetch(`${this.baseUrl}/api/tools?source=Web`);
    await this.assertOk(response);
    return response.json();
  }

  async createTool(tool: Omit<ToolDto, 'id'>): Promise<ToolDto> {
    const response = await fetch(`${this.baseUrl}/api/tools`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(tool)
    });
    await this.assertOk(response);
    return response.json();
  }

  async updateTool(id: string, tool: ToolDto): Promise<ToolDto> {
    const response = await fetch(`${this.baseUrl}/api/tools/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(tool)
    });
    await this.assertOk(response);
    return response.json();
  }

  async deleteTool(id: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/tools/${id}`, {
      method: 'DELETE'
    });
    await this.assertOk(response);
  }
}