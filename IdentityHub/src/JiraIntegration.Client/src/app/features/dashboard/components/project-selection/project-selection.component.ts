import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ProjectSelectionService } from '../../services/project-selection.service';
import { ProjectApiKeyComponent } from '../project-api-key/project-api-key.component';

@Component({
  selector: 'app-project-selection',
  imports: [FormsModule, ProjectApiKeyComponent],
  templateUrl: './project-selection.component.html',
  styleUrl: './project-selection.component.scss'
})
export class ProjectSelectionComponent implements OnInit {
  protected readonly projectSelection = inject(ProjectSelectionService);

  ngOnInit(): void {
    this.projectSelection.loadProjects();
  }

  protected onProjectKeyChange(projectKey: string): void {
    if (projectKey) {
      this.projectSelection.selectProject(projectKey);
    }
  }
}
