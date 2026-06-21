import { HttpClient } from '@angular/common/http';

import { Injectable, inject } from '@angular/core';

import { Observable } from 'rxjs';



import { environment } from '../../../../environments/environment';



export interface CreateTicketRequest {

  projectKey: string;

  title: string;

  description: string;

}



export interface UiTicketResponse {

  issueKey: string;

  issueId: string;

  title: string;

  createdAt: string;

}



export interface RecentTicketItem {

  issueKey: string;

  title: string;

  createdAt: string;

  externalUrl: string | null;

}



export interface RecentTicketsResponse {

  tickets: RecentTicketItem[];

}



export interface JiraProjectOption {

  key: string;

  name: string;

  issueTypeName: string;

}



export interface JiraProjectsResponse {

  projects: JiraProjectOption[];

  selectedProjectKey: string | null;

}



@Injectable({ providedIn: 'root' })

export class TicketService {

  private readonly http = inject(HttpClient);

  private readonly apiBase = `${environment.apiUrl}/ui/tickets`;



  getProjects(): Observable<JiraProjectsResponse> {

    return this.http.get<JiraProjectsResponse>(`${this.apiBase}/projects`);

  }



  createTicket(request: CreateTicketRequest): Observable<UiTicketResponse> {

    return this.http.post<UiTicketResponse>(this.apiBase, {

      projectKey: request.projectKey,

      title: request.title,

      description: request.description

    });

  }



  getRecentTickets(projectKey: string): Observable<RecentTicketsResponse> {
    return this.http.get<RecentTicketsResponse>(`${this.apiBase}/recent`, {
      params: { projectKey }
    });
  }

}

