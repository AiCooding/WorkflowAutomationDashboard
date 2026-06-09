export type FeatureStatus =
  | 'backlog'
  | 'planning'
  | 'in_progress'
  | 'review'
  | 'done'
  | 'cancelled';

export type WorkflowStatus =
  | 'pending'
  | 'running'
  | 'paused'
  | 'waiting_input'
  | 'completed'
  | 'failed'
  | 'cancelled';

export type WorkflowType = 'full-pipeline' | 'bugfix' | 'review-only' | 'feature-spec' | 'custom';

export type AgentStatus = 'idle' | 'running' | 'waiting_input' | 'completed' | 'failed';

export type AgentType =
  | 'orchestrator'
  | 'architect'
  | 'developer'
  | 'code-review'
  | 'pm'
  | 'plan-reviewer';

export type InputRequestStatus = 'pending' | 'answered' | 'expired';

export type CommandType = 'start' | 'pause' | 'resume' | 'cancel' | 'retry';

export type CommandStatus = 'pending' | 'processing' | 'completed' | 'failed';

export type EventType =
  | 'state_change'
  | 'log'
  | 'error'
  | 'input_requested'
  | 'command_received';

export interface Feature {
  id: string;
  name: string;
  description?: string | null;
  status: FeatureStatus;
  priority: number;
  specPath?: string | null;
  createdAt: string;
  updatedAt: string;
  workflows?: Workflow[];
}

export interface Workflow {
  id: string;
  featureId?: string | null;
  type: WorkflowType | string;
  status: WorkflowStatus;
  startedAt?: string | null;
  completedAt?: string | null;
  errorMessage?: string | null;
  createdAt: string;
  feature?: Feature | null;
  agents?: Agent[];
  inputRequests?: InputRequest[];
  commands?: Command[];
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

export interface Command {
  id: string;
  workflowId?: string | null;
  commandType: CommandType | string;
  payloadJson?: string | null;
  status: CommandStatus;
  createdAt: string;
  processedAt?: string | null;
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
  runningWorkflows: number;
  pausedWorkflows: number;
  waitingInputWorkflows: number;
  completedWorkflows: number;
  failedWorkflows: number;
  activeAgents: number;
  pendingInputRequests: number;
  totalFeatures: number;
  featuresInProgress: number;
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

export interface CommandProcessed {
  status: CommandStatus;
}
