import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./pages/home/home').then(m => m.HomePage), title: 'Dashboard — Workflow Dashboard' },
  { path: 'features', loadComponent: () => import('./pages/features/features').then(m => m.FeaturesPage), title: 'Features — Workflow Dashboard' },
  { path: 'repositories', loadComponent: () => import('./pages/repositories/repositories').then(m => m.RepositoriesPage), title: 'Repositories — Workflow Dashboard' },
  { path: 'pipelines', loadComponent: () => import('./pages/pipelines/pipelines').then(m => m.PipelinesPage), title: 'Pipelines — Workflow Dashboard' },
  { path: 'pipelines/designer', loadComponent: () => import('./pages/pipelines/pipeline-designer').then(m => m.PipelineDesignerPage), title: 'Pipeline Designer — Workflow Dashboard' },
  { path: 'pipelines/designer/:id', loadComponent: () => import('./pages/pipelines/pipeline-designer').then(m => m.PipelineDesignerPage), title: 'Pipeline Designer — Workflow Dashboard' },
  { path: 'agents', loadComponent: () => import('./pages/agents/agents').then(m => m.AgentsPage), title: 'Agents — Workflow Dashboard' },
  { path: 'control', loadComponent: () => import('./pages/control/control').then(m => m.ControlPage), title: 'Control — Workflow Dashboard' },
  { path: '**', redirectTo: '' },
];
