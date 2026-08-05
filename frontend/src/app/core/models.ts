export interface Overview {
  applications: number;
  healthy: number;
  progressing: number;
  degraded: number;
  outOfSync: number;
  productionApplications: number;
  generatedAt: string;
  clusters: Cluster[];
  recentActivity: AuditEvent[];
}

export interface ApplicationListItem {
  id: string;
  name: string;
  team: string;
  environment: string;
  cluster: string;
  namespace: string;
  desiredRevision: string;
  liveRevision: string;
  health: string;
  syncStatus: string;
  readyReplicas: number;
  desiredReplicas: number;
  lastDeploymentAt: string;
  cpuPercent: number;
  memoryPercent: number;
}

export interface ApplicationDetail extends ApplicationListItem {
  description: string;
  repository: string;
  image: string;
  previousRevision: string;
  timeline: AuditEvent[];
}

export interface Cluster {
  name: string;
  region: string;
  provider: string;
  version: string;
  health: string;
  nodes: number;
  workloads: number;
  cpuPercent: number;
  memoryPercent: number;
}

export interface AuditEvent {
  id: string;
  application: string;
  action: string;
  status: string;
  revision: string;
  actor: string;
  message: string;
  occurredAt: string;
}

export interface DeploymentActionResult {
  accepted: boolean;
  application: string;
  action: string;
  message: string;
  state: ApplicationListItem;
}

export interface IntegrationStatus {
  name: string;
  mode: string;
  status: string;
  endpoint: string;
}

export interface DeploymentChangedEvent {
  eventName: string;
  application: ApplicationListItem;
  occurredAt: string;
}
