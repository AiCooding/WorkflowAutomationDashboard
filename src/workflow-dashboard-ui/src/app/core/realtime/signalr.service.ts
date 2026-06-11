import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { BehaviorSubject, Observable, Subject, filter, map } from 'rxjs';
import { Agent, ApprovalRequest, CatalogRefreshResult, Feature, InputRequest, PipelineRun, PipelineStepRun, Repository, Workflow, WorkflowEvent, WorkflowLogLine } from '../models';

const HUB_EVENTS = {
  WorkflowUpdated: 'WorkflowUpdated',
  AgentUpdated: 'AgentUpdated',
  InputRequested: 'InputRequested',
  InputAnswered: 'InputAnswered',
  EventLogged: 'EventLogged',
  RepositoryUpdated: 'RepositoryUpdated',
  CatalogRefreshed: 'CatalogRefreshed',
  FeatureUpdated: 'FeatureUpdated',
  WorkflowLog: 'WorkflowLog',
  WorkflowLogTail: 'WorkflowLogTail',
  PipelineRunUpdated: 'PipelineRunUpdated',
  StepRunUpdated: 'StepRunUpdated',
  ApprovalRequested: 'ApprovalRequested',
  ApprovalDecided: 'ApprovalDecided',
  StepLog: 'StepLog',
  StepLogTail: 'StepLogTail',
} as const;

interface WorkflowLogTailPayload {
  workflowId: string;
  lines: Array<{ stream: 'stdout' | 'stderr'; line: string; ts: string }>;
}

interface StepLogLine {
  stepRunId: string;
  stream: 'stdout' | 'stderr';
  line: string;
  ts: string;
}

interface StepLogTailPayload {
  stepRunId: string;
  lines: Array<{ stream: 'stdout' | 'stderr'; line: string; ts: string }>;
}

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private readonly destroyRef = inject(DestroyRef);
  private connection?: HubConnection;
  private readonly startPromise: Promise<void>;

  readonly connected$ = new BehaviorSubject<boolean>(false);

  readonly workflowUpdated$ = new Subject<Workflow>();
  readonly agentUpdated$ = new Subject<Agent>();
  readonly inputRequested$ = new Subject<InputRequest>();
  readonly inputAnswered$ = new Subject<InputRequest>();
  readonly eventLogged$ = new Subject<WorkflowEvent>();
  readonly repositoryUpdated$ = new Subject<Repository>();
  readonly catalogRefreshed$ = new Subject<CatalogRefreshResult>();
  readonly featureUpdated$ = new Subject<Feature>();
  readonly workflowLog$$ = new Subject<WorkflowLogLine>();
  readonly workflowLogTail$$ = new Subject<WorkflowLogTailPayload>();

  readonly pipelineRunUpdated$ = new Subject<PipelineRun>();
  readonly stepRunUpdated$ = new Subject<PipelineStepRun>();
  readonly approvalRequested$ = new Subject<ApprovalRequest>();
  readonly approvalDecided$ = new Subject<ApprovalRequest>();
  readonly stepLog$$ = new Subject<StepLogLine>();
  readonly stepLogTail$$ = new Subject<StepLogTailPayload>();

  constructor() {
    this.startPromise = this.start();
  }

  workflowLog$(workflowId: string): Observable<WorkflowLogLine> {
    return this.workflowLog$$.pipe(filter((l) => l.workflowId === workflowId));
  }

  workflowLogTail$(workflowId: string): Observable<WorkflowLogLine[]> {
    return this.workflowLogTail$$.pipe(
      filter((p) => p.workflowId === workflowId),
      map((p) =>
        p.lines.map((l) => ({
          workflowId: p.workflowId,
          stream: l.stream,
          line: l.line,
          ts: l.ts,
        })),
      ),
    );
  }

  stepLog$(stepRunId: string): Observable<StepLogLine> {
    return this.stepLog$$.pipe(filter((l) => l.stepRunId === stepRunId));
  }

  stepLogTail$(stepRunId: string): Observable<StepLogLine[]> {
    return this.stepLogTail$$.pipe(
      filter((p) => p.stepRunId === stepRunId),
      map((p) =>
        p.lines.map((l) => ({
          stepRunId: p.stepRunId,
          stream: l.stream,
          line: l.line,
          ts: l.ts,
        })),
      ),
    );
  }

  async subscribeToWorkflow(workflowId: string): Promise<void> {
    await this.startPromise;
    if (this.connection?.state !== HubConnectionState.Connected) return;
    try {
      await this.connection.invoke('SubscribeToWorkflow', workflowId);
    } catch (err) {
      console.warn('SubscribeToWorkflow failed:', err);
    }
  }

  async unsubscribeFromWorkflow(workflowId: string): Promise<void> {
    await this.startPromise;
    if (this.connection?.state !== HubConnectionState.Connected) return;
    try {
      await this.connection.invoke('UnsubscribeFromWorkflow', workflowId);
    } catch (err) {
      console.warn('UnsubscribeFromWorkflow failed:', err);
    }
  }

  async subscribeToStepRun(stepRunId: string): Promise<void> {
    await this.startPromise;
    if (this.connection?.state !== HubConnectionState.Connected) return;
    try {
      await this.connection.invoke('SubscribeToStepRun', stepRunId);
    } catch (err) {
      console.warn('SubscribeToStepRun failed:', err);
    }
  }

  async unsubscribeFromStepRun(stepRunId: string): Promise<void> {
    await this.startPromise;
    if (this.connection?.state !== HubConnectionState.Connected) return;
    try {
      await this.connection.invoke('UnsubscribeFromStepRun', stepRunId);
    } catch (err) {
      console.warn('UnsubscribeFromStepRun failed:', err);
    }
  }

  private async start(): Promise<void> {
    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/workflow')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on(HUB_EVENTS.WorkflowUpdated, (w: Workflow) => this.workflowUpdated$.next(w));
    this.connection.on(HUB_EVENTS.AgentUpdated, (a: Agent) => this.agentUpdated$.next(a));
    this.connection.on(HUB_EVENTS.InputRequested, (i: InputRequest) => this.inputRequested$.next(i));
    this.connection.on(HUB_EVENTS.InputAnswered, (i: InputRequest) => this.inputAnswered$.next(i));
    this.connection.on(HUB_EVENTS.EventLogged, (e: WorkflowEvent) => this.eventLogged$.next(e));
    this.connection.on(HUB_EVENTS.RepositoryUpdated, (r: Repository) => this.repositoryUpdated$.next(r));
    this.connection.on(HUB_EVENTS.CatalogRefreshed, (c: CatalogRefreshResult) => this.catalogRefreshed$.next(c));
    this.connection.on(HUB_EVENTS.FeatureUpdated, (payload: Feature | { feature: Feature; deleted: boolean }) => {
      if (payload && typeof payload === 'object' && 'feature' in payload) {
        const env = payload as { feature: Feature; deleted: boolean };
        this.featureUpdated$.next({ ...env.feature, deleted: env.deleted });
      } else {
        this.featureUpdated$.next(payload as Feature);
      }
    });
    this.connection.on(HUB_EVENTS.WorkflowLog, (l: WorkflowLogLine) => this.workflowLog$$.next(l));
    this.connection.on(HUB_EVENTS.WorkflowLogTail, (p: WorkflowLogTailPayload) => this.workflowLogTail$$.next(p));

    this.connection.on(HUB_EVENTS.PipelineRunUpdated, (run: PipelineRun) => this.pipelineRunUpdated$.next(run));
    this.connection.on(HUB_EVENTS.StepRunUpdated, (stepRun: PipelineStepRun) => this.stepRunUpdated$.next(stepRun));
    this.connection.on(HUB_EVENTS.ApprovalRequested, (approval: ApprovalRequest) => this.approvalRequested$.next(approval));
    this.connection.on(HUB_EVENTS.ApprovalDecided, (approval: ApprovalRequest) => this.approvalDecided$.next(approval));
    this.connection.on(HUB_EVENTS.StepLog, (line: StepLogLine) => this.stepLog$$.next(line));
    this.connection.on(HUB_EVENTS.StepLogTail, (payload: StepLogTailPayload) => this.stepLogTail$$.next(payload));

    this.connection.onreconnected(() => this.connected$.next(true));
    this.connection.onreconnecting(() => this.connected$.next(false));
    this.connection.onclose(() => this.connected$.next(false));

    try {
      await this.connection.start();
      this.connected$.next(true);
    } catch (err) {
      console.error('SignalR connection failed:', err);
      this.connected$.next(false);
    }

    this.destroyRef.onDestroy(() => {
      if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
        this.connection.stop().catch(() => undefined);
      }
    });

    this.connected$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }
}
