import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { AGENT_API_BASE_URL } from './blocks/chat/agent-chat.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    { provide: AGENT_API_BASE_URL, useValue: 'http://localhost:5203' },
  ],
};
