import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';
import { Agent, Command, InputRequest, Workflow, WorkflowEvent } from '../models';

const HUB_EVENTS = {
  WorkflowUpdated: 'WorkflowUpdated',
  AgentUpdated: 'AgentUpdated',
  InputRequested: 'InputRequested',
  InputAnswered: 'InputAnswered',
  CommandIssued: 'CommandIssued',
  EventLogged: 'EventLogged',
} as const;

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private readonly destroyRef = inject(DestroyRef);
  private connection?: HubConnection;

  readonly connected$ = new BehaviorSubject<boolean>(false);

  readonly workflowUpdated$ = new Subject<Workflow>();
  readonly agentUpdated$ = new Subject<Agent>();
  readonly inputRequested$ = new Subject<InputRequest>();
  readonly inputAnswered$ = new Subject<InputRequest>();
  readonly commandIssued$ = new Subject<Command>();
  readonly eventLogged$ = new Subject<WorkflowEvent>();

  constructor() {
    this.start();
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
    this.connection.on(HUB_EVENTS.CommandIssued, (c: Command) => this.commandIssued$.next(c));
    this.connection.on(HUB_EVENTS.EventLogged, (e: WorkflowEvent) => this.eventLogged$.next(e));

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
