import { Component, EventEmitter, OnInit, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToolsService } from '../../services/tools.service';
import { ToolModel } from '../../models/tool-model';

@Component({
  selector: 'app-manage-tools',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './manage-tools.component.html',
  styleUrl: './manage-tools.component.css'
})
export class ManageToolsComponent implements OnInit {
  @Output() close = new EventEmitter<void>();

  tools = signal<ToolModel[]>([]);
  isLoading = signal(false);
  loadError = signal('');

  selectedTool = signal<ToolModel | null>(null);

  toolName = signal('');
  description = signal('');
  parametersJsonSchema = signal('');
  isEnabled = signal(true);

  errorMessage = signal('');

  constructor(private toolsService: ToolsService) {}

  ngOnInit(): void {
    this.loadTools();
  }

  async loadTools(): Promise<void> {
    this.isLoading.set(true);
    this.loadError.set('');

    try {
      const tools = await this.toolsService.getTools();
      this.tools.set(tools);
    } catch (err) {
      console.error('Failed to load tools', err);
      this.loadError.set(err instanceof Error ? err.message : 'Failed to load tools.');
    } finally {
      this.isLoading.set(false);
    }
  }

  get hasError(): boolean {
    return this.errorMessage().length > 0;
  }

  get formHeaderText(): string {
    return this.selectedTool() ? 'Edit Tool' : 'New Tool';
  }

  selectTool(tool: ToolModel): void {
    this.selectedTool.set(tool);
    this.toolName.set(tool.name);
    this.description.set(tool.description);
    this.parametersJsonSchema.set(tool.parametersJsonSchema);
    this.isEnabled.set(tool.isEnabled);
    this.errorMessage.set('');
  }

  onNew(): void {
    this.selectedTool.set(null);
    this.toolName.set('');
    this.description.set('');
    this.parametersJsonSchema.set('');
    this.isEnabled.set(true);
    this.errorMessage.set('');
  }

  async onSave(): Promise<void> {
    if (!this.toolName().trim()) {
      this.errorMessage.set('Name is required.');
      return;
    }

    if (this.parametersJsonSchema().trim()) {
      try {
        JSON.parse(this.parametersJsonSchema());
      } catch {
        this.errorMessage.set('Parameters JSON Schema is not valid JSON.');
        return;
      }
    }

    this.errorMessage.set('');
    const current = this.selectedTool();

    const payload = {
      name: this.toolName(),
      description: this.description(),
      parametersJsonSchema: this.parametersJsonSchema(),
      isEnabled: this.isEnabled()
    };

    try {
      if (current) {
        const updated = await this.toolsService.updateTool(current.id, { ...payload, id: current.id });
        this.tools.update(list => list.map(t => (t.id === current.id ? updated : t)));
        this.selectedTool.set(updated);
      } else {
        const created = await this.toolsService.createTool(payload);
        this.tools.update(list => [...list, created]);
        this.selectedTool.set(created);
      }
    } catch (err) {
      console.error('Failed to save tool', err);
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to save tool.');
    }
  }

  async onDelete(): Promise<void> {
    const current = this.selectedTool();
    if (!current) {
      return;
    }

    try {
      await this.toolsService.deleteTool(current.id);
      this.tools.update(list => list.filter(t => t.id !== current.id));
      this.onNew();
    } catch (err) {
      console.error('Failed to delete tool', err);
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to delete tool.');
    }
  }

  onClose(): void {
    this.close.emit();
  }
}