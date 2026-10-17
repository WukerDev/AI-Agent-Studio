import * as signalR from '@microsoft/signalr';
import { useConfigStore } from './config';

class EventsService {
    private connection: signalR.HubConnection | null = null;

    public async connect() {
        const configStore = useConfigStore();
        const eventsUrl = configStore.getEventsUrl;

        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(eventsUrl)
            .withAutomaticReconnect()
            .build();

        this.connection.on('ReceiveEvent', (eventData: any) => {
            console.log('Event received:', eventData);
        });

        try {
            await this.connection.start();
            console.log('SignalR Connected.');
        } catch (err) {
            console.error('SignalR Connection Error: ', err);
            setTimeout(() => this.connect(), 5000);
        }
    }

    public async disconnect() {
        if (this.connection) {
            await this.connection.stop();
            console.log('SignalR Disconnected.');
        }
    }
}

export const eventsService = new EventsService();