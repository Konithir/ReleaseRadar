import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, finalize } from 'rxjs';
import { ReleaseRadarApi } from './core/release-radar.api';
import {
  ApplicationDetail,
  ApplicationListItem,
  IntegrationStatus,
  Overview,
} from './core/models';

type ActionType = 'sync' | 'rollback';
interface PendingAction { type: ActionType; application: ApplicationListItem; }

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit, OnDestroy {
  private readonly api = inject(ReleaseRadarApi);

  readonly overview = signal<Overview | null>(null);
  readonly applications = signal<ApplicationListItem[]>([]);
  readonly selected = signal<ApplicationDetail | null>(null);
  readonly integrations = signal<IntegrationStatus[]>([]);
  readonly environment = signal('all');
  readonly health = signal('all');
  readonly query = signal('');
  readonly loading = signal(true);
  readonly selectedLoading = signal(false);
  readonly acting = signal(false);
  readonly pendingAction = signal<PendingAction | null>(null);
  readonly toast = signal<string | null>(null);
  readonly lastSignal = signal<string>('Listening for changes');
  readonly nav = signal('Deployments');

  readonly filteredApplications = computed(() => {
    const environment = this.environment();
    const health = this.health();
    const query = this.query().trim().toLowerCase();
    return this.applications().filter((application) =>
      (environment === 'all' || application.environment === environment) &&
      (health === 'all' || application.health.toLowerCase() === health) &&
      (!query || application.name.toLowerCase().includes(query) || application.team.toLowerCase().includes(query))
    );
  });

  readonly healthyPercent = computed(() => {
    const overview = this.overview();
    return overview?.applications ? Math.round((overview.healthy / overview.applications) * 100) : 0;
  });

  ngOnInit(): void {
    this.refresh();
    void this.api.connect();
    this.api.deploymentChanged.subscribe((event) => {
      this.lastSignal.set(`${event.application.name} · ${event.eventName.replaceAll('-', ' ')}`);
      this.applications.update((items) => items.map((item) => item.name === event.application.name ? event.application : item));
      if (this.selected()?.name === event.application.name) {
        this.loadApplication(event.application.name);
      }
      this.api.getOverview().subscribe((overview) => this.overview.set(overview));
    });
  }

  ngOnDestroy(): void {
    void this.api.disconnect();
  }

  refresh(): void {
    this.loading.set(true);
    forkJoin({
      overview: this.api.getOverview(),
      applications: this.api.getApplications(),
      integrations: this.api.getIntegrations(),
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: ({ overview, applications, integrations }) => {
        this.overview.set(overview);
        this.applications.set(applications);
        this.integrations.set(integrations);
        const selectedName = this.selected()?.name ?? applications[0]?.name;
        if (selectedName) this.loadApplication(selectedName);
      },
      error: () => this.showToast('API is unavailable. Start ReleaseRadar.Api on port 5080.'),
    });
  }

  loadApplication(name: string): void {
    this.selectedLoading.set(true);
    this.api.getApplication(name).pipe(finalize(() => this.selectedLoading.set(false))).subscribe({
      next: (application) => this.selected.set(application),
      error: () => this.showToast(`Could not load ${name}.`),
    });
  }

  requestAction(type: ActionType, application: ApplicationListItem): void {
    this.pendingAction.set({ type, application });
  }

  confirmAction(): void {
    const pending = this.pendingAction();
    if (!pending) return;

    this.acting.set(true);
    const operation = pending.type === 'sync'
      ? this.api.sync(pending.application.name)
      : this.api.rollback(pending.application.name);

    operation.pipe(finalize(() => this.acting.set(false))).subscribe({
      next: (result) => {
        this.pendingAction.set(null);
        this.showToast(result.message);
        this.refresh();
      },
      error: () => this.showToast(`${this.actionLabel(pending.type)} failed.`),
    });
  }

  setEnvironment(value: string): void {
    this.environment.set(value);
  }

  setHealth(value: string): void {
    this.health.set(value);
  }

  statusClass(value: string): string {
    return value.toLowerCase().replaceAll(' ', '-');
  }

  shortRevision(value: string): string {
    return value.length > 8 ? value.slice(0, 8) : value;
  }

  elapsed(value: string): string {
    const seconds = Math.max(0, Math.floor((Date.now() - new Date(value).getTime()) / 1000));
    if (seconds < 60) return `${seconds}s ago`;
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  }

  actionLabel(type: ActionType): string {
    return type === 'sync' ? 'Sync application' : 'Roll back';
  }

  private showToast(message: string): void {
    this.toast.set(message);
    window.setTimeout(() => this.toast.set(null), 4200);
  }
}
