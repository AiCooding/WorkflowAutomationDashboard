import { DatePipe } from '@angular/common';
import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class StatusStyle {
  // Map status string -> Material color hint + icon
  workflow(status: string | null | undefined): { color: string; icon: string } {
    switch (status) {
      case 'running': return { color: '#2563eb', icon: 'play_circle' };
      case 'paused': return { color: '#9333ea', icon: 'pause_circle' };
      case 'waiting_input': return { color: '#d97706', icon: 'help' };
      case 'completed': return { color: '#16a34a', icon: 'check_circle' };
      case 'failed': return { color: '#dc2626', icon: 'error' };
      case 'cancelled': return { color: '#6b7280', icon: 'cancel' };
      case 'pending':
      default: return { color: '#6b7280', icon: 'schedule' };
    }
  }

  agent(status: string | null | undefined): { color: string; icon: string } {
    switch (status) {
      case 'running': return { color: '#2563eb', icon: 'sync' };
      case 'waiting_input': return { color: '#d97706', icon: 'help' };
      case 'completed': return { color: '#16a34a', icon: 'check_circle' };
      case 'failed': return { color: '#dc2626', icon: 'error' };
      case 'idle':
      default: return { color: '#6b7280', icon: 'pause' };
    }
  }

  feature(status: string | null | undefined): { color: string; icon: string } {
    switch (status) {
      case 'planning': return { color: '#9333ea', icon: 'edit_note' };
      case 'in_progress': return { color: '#2563eb', icon: 'pending_actions' };
      case 'review': return { color: '#d97706', icon: 'rate_review' };
      case 'done': return { color: '#16a34a', icon: 'task_alt' };
      case 'cancelled': return { color: '#6b7280', icon: 'cancel' };
      case 'backlog':
      default: return { color: '#6b7280', icon: 'inventory_2' };
    }
  }

  event(eventType: string | null | undefined): { color: string; icon: string } {
    switch (eventType) {
      case 'state_change': return { color: '#2563eb', icon: 'swap_horiz' };
      case 'log': return { color: '#6b7280', icon: 'article' };
      case 'error': return { color: '#dc2626', icon: 'error' };
      case 'input_requested': return { color: '#d97706', icon: 'help' };
      case 'command_received': return { color: '#9333ea', icon: 'send' };
      default: return { color: '#6b7280', icon: 'circle' };
    }
  }
}

export function formatDuration(startedAt: string | null | undefined, completedAt?: string | null): string {
  if (!startedAt) return '—';
  const start = new Date(startedAt).getTime();
  const end = completedAt ? new Date(completedAt).getTime() : Date.now();
  const seconds = Math.max(0, Math.floor((end - start) / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ${seconds % 60}s`;
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m`;
}

export const SHARED_DATE_PIPE = DatePipe;
