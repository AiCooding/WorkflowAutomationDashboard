import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./pages/home/home').then((m) => m.HomePage),
    title: 'Dashboard — Workflow Dashboard',
  },
  {
    path: 'features',
    loadComponent: () => import('./pages/features/features').then((m) => m.FeaturesPage),
    title: 'Features — Workflow Dashboard',
  },
  {
    path: 'workflows',
    loadComponent: () => import('./pages/workflows/workflows').then((m) => m.WorkflowsPage),
    title: 'Workflows — Workflow Dashboard',
  },
  {
    path: 'agents',
    loadComponent: () => import('./pages/agents/agents').then((m) => m.AgentsPage),
    title: 'Agents — Workflow Dashboard',
  },
  {
    path: 'inputs',
    loadComponent: () => import('./pages/inputs/inputs').then((m) => m.InputsPage),
    title: 'Inputs — Workflow Dashboard',
  },
  {
    path: 'control',
    loadComponent: () => import('./pages/control/control').then((m) => m.ControlPage),
    title: 'Control — Workflow Dashboard',
  },
  {
    path: 'events',
    loadComponent: () => import('./pages/events/events').then((m) => m.EventsPage),
    title: 'Events — Workflow Dashboard',
  },
  { path: '**', redirectTo: '' },
];
