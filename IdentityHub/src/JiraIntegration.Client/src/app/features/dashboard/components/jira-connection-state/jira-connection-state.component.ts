import { Component, inject, OnInit } from '@angular/core';

import { JiraConnectionService } from '../../services/jira-connection.service';

@Component({
  selector: 'app-jira-connection-state',
  templateUrl: './jira-connection-state.component.html',
  styleUrl: './jira-connection-state.component.scss'
})
export class JiraConnectionStateComponent implements OnInit {
  protected readonly jiraConnectionService = inject(JiraConnectionService);

  ngOnInit(): void {
    if (!this.jiraConnectionService.statusKnown()) {
      this.jiraConnectionService.checkConnectionStatus().subscribe();
    }
  }

  protected connect(): void {
    this.jiraConnectionService.connect();
  }
}
