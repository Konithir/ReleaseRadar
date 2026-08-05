import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import {
  ApplicationDetail,
  ApplicationListItem,
  DeploymentActionResult,
  DeploymentChangedEvent,
  IntegrationStatus,
  Overview,
} from './models';

@Injectable({ providedIn: 'root' })
export class ReleaseRadarApi {
  private readonly http = inject(HttpClient);
  private hub?: HubConnection;
  readonly deploymentChanged = new Subject<DeploymentChangedEvent>();

  getOverview() {
    return this.http.get<Overview>('/api/overview');
  }

  getApplications() {
    return this.http.get<ApplicationListItem[]>('/api/applications');
  }

  getApplication(name: string) {
    return this.http.get<ApplicationDetail>(`/api/applications/${encodeURIComponent(name)}`);
  }

  getIntegrations() {
    return this.http.get<IntegrationStatus[]>('/api/integrations/status');
  }

  sync(name: string, revision?: string) {
    return this.http.post<DeploymentActionResult>(`/api/applications/${encodeURIComponent(name)}/sync`, {
      revision: revision || null,
      requestedBy: 'release-radar-user',
    });
  }

  rollback(name: string) {
    return this.http.post<DeploymentActionResult>(`/api/applications/${encodeURIComponent(name)}/rollback`, {
      revision: null,
      requestedBy: 'release-radar-user',
    });
  }

  async connect(): Promise<void> {
    if (this.hub) return;

    this.hub = new HubConnectionBuilder()
      .withUrl('/hubs/deployments')
      .configureLogging(LogLevel.Warning)
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    this.hub.on('deploymentChanged', (event: DeploymentChangedEvent) => this.deploymentChanged.next(event));

    try {
      await this.hub.start();
    } catch {
      this.hub = undefined;
    }
  }

  async disconnect(): Promise<void> {
    await this.hub?.stop();
    this.hub = undefined;
  }
}
