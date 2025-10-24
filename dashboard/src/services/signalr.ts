import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { SIGNALR_BASE_URL, SIGNALR_HUB_PATH } from './constants';
import { AuthService } from './auth';

export class SignalRService {
  private connection: HubConnection | null = null;
  private retryIntervals: number[] = [0, 2000, 5000]; // Default intervals

  setRetryIntervals(intervals: number[]) {
    this.retryIntervals = intervals;
  }

  async start(): Promise<void> {
    const token = AuthService.getToken() || '';
    const hubUrl = `${SIGNALR_BASE_URL}/${SIGNALR_HUB_PATH}`;
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => token })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Use configured intervals
          const count = retryContext.previousRetryCount;
          if (count < this.retryIntervals.length) {
            return this.retryIntervals[count];
          }
          // After exhausting intervals, keep using the last one
          return this.retryIntervals[this.retryIntervals.length - 1];
        }
      })
      .configureLogging(LogLevel.Warning) // Only show warnings and errors
      .build();

    // Register handler for server-side 'connected' acknowledgment
    this.connection.on('connected', () => {
      // Backend acknowledges connection - no action needed
    });

    try {
      await this.connection.start();
    } catch (e) {
      console.warn('[SignalR] Connection failed', e);
      throw e;
    }
  }

  stop(): Promise<void> {
    return this.connection ? this.connection.stop() : Promise.resolve();
  }

  on(event: string, cb: (...args: any[]) => void) {
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

  getConnectionState(): string {
    return this.connection?.state || 'Disconnected';
  }
}


