export type FeatureStatus =
  | 'backlog'
  | 'planning'
  | 'in_progress'
  | 'review'
  | 'done'
  | 'cancelled';

export type WorkflowStatus =
  | 'pending'
  | 'queued'
  | 'running'
  | 'completed'
  | 'failed'
  | 'cancelled'
  | 'broken';

export type WorkflowType = string;

export type AgentStatus = 'idle' | 'running' | 'waiting_input' | 'completed' | 'failed';

export type AgentType =
  | 'orchestrator'
  | 'architect'
  | 'developer'
  | 'code-review'
  | 'pm'
  | 'plan-reviewer';

export type InputRequestStatus = 'pending' | 'answered' | 'expired';

export type EventType =
  | 'state_change'
  | 'log'
  | 'error'
  | 'input_requested';

export interface Feature {
  id: string;
  name: string;
  description?: string | null;
  status: FeatureStatus;
  derivedStatus?: FeatureStatus | string;
  priority: number;
  specFolder?: string | null;
  repositoryId?: string | null;
  createdAt: string;
  updatedAt: string;
  deleted?: boolean;
}

export interface Workflow {
  id: string;
  featureId?: string | null;
  repositoryId: string | null;
  catalogSlug: string;
  status: WorkflowStatus;
  launchPayloadJson?: string | null;
  processId?: number | null;
  startedAt?: string | null;
  completedAt?: string | null;
  errorMessage?: string | null;
  queuedAt?: string | null;
  queueReason?: string | null;
  brokenReason?: string | null;
  createdAt: string;
  feature?: Feature | null;
  agents?: Agent[];
  inputRequests?: InputRequest[];
  catalogDisplayName?: string | null;
  featureName?: string | null;
  repositoryPath?: string | null;
  repositoryName?: string | null;
}

export interface LaunchWorkflowBody {
  catalogSlug: string;
  repositoryId: string;
  featureId?: string | null;
  payload?: unknown;
}

export interface WorkflowLogLine {
  workflowId: string;
  stream: 'stdout' | 'stderr';
  line: string;
  ts: string;
}

export interface Agent {
  id: string;
  workflowId: string;
  agentType: AgentType | string;
  status: AgentStatus;
  currentTask?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  sessionId?: string | null;
}

export interface InputRequest {
  id: string;
  workflowId: string;
  agentId: string;
  question: string;
  optionsJson?: string | null;
  response?: string | null;
  status: InputRequestStatus;
  createdAt: string;
  answeredAt?: string | null;
}

export interface WorkflowEvent {
  id: number;
  workflowId?: string | null;
  agentId?: string | null;
  eventType: EventType | string;
  message?: string | null;
  metadataJson?: string | null;
  createdAt: string;
}

export interface DashboardSummary {
  runningPipelineRuns: number;
  waitingApprovalRuns: number;
  completedPipelineRuns: number;
  failedPipelineRuns: number;
  pendingApprovals: number;
  totalFeatures: number;
  featuresInProgress: number;
  totalRepositories: number;
  totalPipelines: number;
  brokenRepositories: number;
  totalCatalogEntries: number;
  brokenCatalogEntries: number;
}

export interface WorkflowStatusUpdate {
  status: WorkflowStatus;
  errorMessage?: string | null;
  featureId?: string | null;
}

export interface FeatureSpec {
  featureId: string;
  specPath: string;
  content: string;
  fullPath: string;
}

export interface SpecManifestFile {
  exists: boolean;
  content?: string | null;
}

export interface SpecManifest {
  folder: string;
  repositoryId: string;
  proposal: SpecManifestFile;
  design: SpecManifestFile;
  tasks: SpecManifestFile;
}

export interface SpecFolderRow {
  slug: string;
  path: string;
  hasProposal: boolean;
  hasDesign: boolean;
  hasTasks: boolean;
  mtime: string;
}

export interface NewFeatureBody {
  repositoryId: string;
  name: string;
  description?: string | null;
  mode: 'link' | 'stub' | 'inline';
  specFolderSlug?: string;
  specSlug?: string;
  specBody?: string;
}

export interface FeatureSpecUpdate {
  content: string;
  fileName?: string | null;
}

export interface AgentUpdate {
  status?: AgentStatus | null;
  currentTask?: string | null;
}

export interface InputAnswer {
  response: string;
}

export interface Repository {
  id: string;
  path: string;
  name: string;
  isBroken: boolean;
  featureCount: number;
  createdAt: string;
  updatedAt: string;
  deleted?: boolean;
}

export interface RepositoryCreate {
  path: string;
  name?: string;
}

export interface RepositoryUpdate {
  name?: string;
  path?: string;
}

export interface CatalogEntry {
  slug: string;
  kind: 'workflow' | 'agent' | string;
  displayName: string;
  description?: string | null;
  sourcePath: string;
  isBroken: boolean;
  brokenReason?: string | null;
  loadedAt: string;
}

export interface CatalogEntryDetail {
  entry: CatalogEntry;
  markdownSource: string;
  renderedHtml: string;
}

export interface CatalogRefreshResult {
  workflowCount: number;
  agentCount: number;
  brokenCount: number;
}

export interface Pipeline {
  id: string;
  name: string;
  description?: string | null;
  stepsJson: string;
  steps?: PipelineStepDef[];
  createdAt: string;
  updatedAt: string;
}

export interface PipelineStepDef {
  id: string;
  type: 'agent' | 'userApproval';
  name: string;
  agentSlug?: string | null;
  canGiveFeedback: boolean;
  returnTo?: string | null;
}

export type PipelineRunStatus = 'pending' | 'running' | 'waiting_approval' | 'completed' | 'failed' | 'cancelled' | 'queued';

export interface PipelineRun {
  id: string;
  pipelineId: string;
  featureId?: string | null;
  repositoryId: string;
  status: PipelineRunStatus;
  currentStepId?: string | null;
  createdAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  errorMessage?: string | null;
  ticketNumber: string;
  branchPrefix?: string | null;
  defaultBranch?: string | null;
  featureSlug: string;
  branchName?: string; // computed by frontend
  pipelineName?: string | null;
  featureName?: string | null;
  repositoryPath?: string | null;
  repositoryName?: string | null;
  stepRuns?: PipelineStepRun[];
  pendingApproval?: ApprovalRequest | null;
}

export type StepRunStatus = 'pending' | 'running' | 'waiting_approval' | 'completed' | 'failed' | 'skipped';

export interface PipelineStepRun {
  id: string;
  pipelineRunId: string;
  stepId: string;
  stepType: string;
  agentSlug?: string | null;
  attemptNumber: number;
  status: StepRunStatus;
  processId?: number | null;
  startedAt?: string | null;
  completedAt?: string | null;
  errorMessage?: string | null;
}

export interface ApprovalRequest {
  id: string;
  pipelineRunId: string;
  stepRunId: string;
  stepId: string;
  status: 'pending' | 'approved' | 'rejected';
  feedbackText?: string | null;
  createdAt: string;
  decidedAt?: string | null;
  stepName?: string | null;
}

export interface StartPipelineRunBody {
  pipelineId: string;
  repositoryId: string;
  featureId?: string | null;
  ticketNumber: string;
  branchPrefix?: string | null;
  conflictResolution?: 'use-existing' | 'rename' | null;
  overrideTicketNumber?: string | null;
  overrideBranchPrefix?: string | null;
  initialInstructions?: string | null;
}

export function computeBranchName(ticketNumber: string, branchPrefix?: string | null): string {
  if (!branchPrefix?.trim()) return ticketNumber;
  return `${branchPrefix.replace(/\/$/, '')}/${ticketNumber}`;
}

export interface ApprovalDecisionBody {
  decision: 'approved' | 'rejected';
  feedbackText?: string | null;
}
