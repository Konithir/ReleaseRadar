import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { App } from './app';
import { ReleaseRadarApi } from './core/release-radar.api';
import { DeploymentChangedEvent } from './core/models';

class ApiStub {
  deploymentChanged = new Subject<DeploymentChangedEvent>();
  connect = () => Promise.resolve();
  disconnect = () => Promise.resolve();
  getOverview = () => of({ applications: 0, healthy: 0, progressing: 0, degraded: 0, outOfSync: 0, productionApplications: 0, generatedAt: new Date().toISOString(), clusters: [], recentActivity: [] });
  getApplications = () => of([]);
  getIntegrations = () => of([]);
  getApplication = () => of(null);
}

describe('App', () => {
  let fixture: ComponentFixture<App>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [{ provide: ReleaseRadarApi, useClass: ApiStub }],
    }).compileComponents();
    fixture = TestBed.createComponent(App);
    fixture.detectChanges();
  });

  it('renders the deployment heading', () => {
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Deployments');
  });

  it('starts with all environments visible', () => {
    expect(fixture.componentInstance.environment()).toBe('all');
  });
});
