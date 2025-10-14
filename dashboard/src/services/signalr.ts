import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { SIGNALR_BASE_URL, SIGNALR_HUB_PATH } from './constants';
import { AuthService } from './auth';

export class SignalRService {
  private connection: HubConnection | null = null;

  async start(): Promise<void> {
    const token = AuthService.getToken() || '';
    const hubUrl = `${SIGNALR_BASE_URL}/${SIGNALR_HUB_PATH}`;
    console.log('[SignalR] starting, hubUrl =', hubUrl, 'tokenPresent =', !!token);
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();
    // Register default handlers for server-side calls
    this.connection.on('connected', () => {
      console.log('[SignalR] server acknowledged connection');
    });

    try {
      await this.connection.start();
      console.log('[SignalR] connected');
    } catch (e) {
      console.warn('[SignalR] connection failed', e);
      throw e;
    }
  }

  stop(): Promise<void> {
    return this.connection ? this.connection.stop() : Promise.resolve();
  }

  on(event: string, cb: (...args: any[]) => void) {
    console.log('[SignalR] register handler for', event);
    this.connection?.on(event, cb);
  }

  onReconnected(cb: () => void) {
    this.connection?.onreconnected(cb);
  }

  onReconnecting(cb: () => void) {
    this.connection?.onreconnecting(() => cb());
  }

  onClose(cb: () => void) {
    this.connection?.onclose(() => cb());
  }
}


