import { defineStore } from 'pinia'
import * as signalR from '@microsoft/signalr'
import { GrpcWebFetchTransport } from '@protobuf-ts/grpcweb-transport'
import type { RpcOptions } from '@protobuf-ts/runtime-rpc'
import { DateTime } from 'luxon'
import { AgentFlowServiceClient } from './clients/generated/agent_node.client'
import type {
    AgentNode,
    ExplorerItem,
    ExecuteWorkflowRequest,
    GetExplorerRequest,
    SaveAgentNodeRequest,
    GetAgentNodesRequest
} from './clients/generated/agent_node'
import { useConfig } from './config'
import { useAuth } from './auth'

let hubConnection: signalR.HubConnection | null = null
let grpcClient: AgentFlowServiceClient | null = null

export const useGlobalStore = defineStore('global', {
    state: (): GlobalStoreState => ({
        nodes: [],
        explorerItems: [],
        currentFlowId: '',
        currentExplorerPath: '',
        activeExecutionId: '',
        lastSavedNodeId: '',
        isGrpcLoading: false,
        isSignalRConnected: false,
        errorKey: '',
        lastSyncMillis: 0
    }),
    actions: {
        async initializeRealtimeAndGrpc(): Promise<void> {
            this.setupGrpcClient()
            await this.connectSignalR()
        },
        async executeWorkflow(request: ExecuteWorkflowRequest): Promise<void> {
            await this.runGrpcCommand(async (client, options) => {
                const call = await client.executeWorkflow(request, options)
                if (!call.response.success) {
                    this.errorKey = 'error.grpc.execute_workflow_failed'
                }
            })
        },
        async getExplorer(request: GetExplorerRequest): Promise<void> {
            await this.runGrpcCommand(async (client, options) => {
                const call = await client.getExplorer(request, options)
                if (!call.response.success) {
                    this.errorKey = 'error.grpc.get_explorer_failed'
                }
            })
        },
        async saveAgentNode(request: SaveAgentNodeRequest): Promise<void> {
            await this.runGrpcCommand(async (client, options) => {
                const call = await client.saveAgentNode(request, options)
                if (!call.response.success) {
                    this.errorKey = 'error.grpc.save_node_failed'
                }
            })
        },
        async getAgentNodes(request: GetAgentNodesRequest): Promise<void> {
            await this.runGrpcCommand(async (client, options) => {
                const call = await client.getAgentNodes(request, options)
                if (!call.response.success) {
                    this.errorKey = 'error.grpc.get_nodes_failed'
                }
            })
        },
        setupGrpcClient(): void {
            const configStore = useConfig()
            const transport = new GrpcWebFetchTransport({
                baseUrl: configStore.getGrpcUrl as string
            })
            grpcClient = new AgentFlowServiceClient(transport)
        },
        async connectSignalR(): Promise<void> {
            const configStore = useConfig()
            const authStore = useAuth()

            hubConnection = new signalR.HubConnectionBuilder()
                .withUrl(configStore.getEventsUrl as string, {

                    accessTokenFactory: () => authStore.token.value
                })
                .withAutomaticReconnect()
                .build()

            this.registerSignalREvents()

            try {
                await hubConnection.start()
                this.isSignalRConnected = true
            } catch {
                this.isSignalRConnected = false
                this.errorKey = 'error.signalr.connection_failed'
            }
        },
        registerSignalREvents(): void {
            if (!hubConnection) {
                return
            }

            hubConnection.on('NodeSaved', (payload: NodeSavedEventPayload) => {
                this.lastSavedNodeId = payload.nodeId
                this.updateSyncTimestamp()
            })

            hubConnection.on('AgentNodesLoaded', (payload: AgentNodesLoadedEventPayload) => {
                this.currentFlowId = payload.flowId
                this.nodes = payload.nodes
                this.updateSyncTimestamp()
            })

            hubConnection.on('WorkflowStarted', (payload: WorkflowStartedEventPayload) => {
                this.activeExecutionId = payload.executionId
                this.updateSyncTimestamp()
            })

            hubConnection.on('ExplorerLoaded', (payload: ExplorerLoadedEventPayload) => {
                this.currentExplorerPath = payload.path
                this.explorerItems = payload.items
                this.updateSyncTimestamp()
            })

            hubConnection.onclose(() => {
                this.isSignalRConnected = false
            })

            hubConnection.onreconnected(() => {
                this.isSignalRConnected = true
            })
        },
        async runGrpcCommand(
            action: (client: AgentFlowServiceClient, options: RpcOptions) => Promise<void>
        ): Promise<void> {
            if (!grpcClient) {
                this.setupGrpcClient()
            }
            if (!grpcClient) {
                this.errorKey = 'error.grpc.client_not_initialized'
                return
            }

            this.isGrpcLoading = true
            this.errorKey = ''

            try {
                const options = this.createAuthOptions()
                await action(grpcClient, options)
            } catch {
                this.errorKey = 'error.grpc.request_failed'
            } finally {
                this.isGrpcLoading = false
            }
        },
        createAuthOptions(): RpcOptions {
            const authStore = useAuth()
            return {
                meta: {
                    Authorization: `Bearer ${authStore.token}`
                }
            }
        },
        updateSyncTimestamp(): void {
            this.lastSyncMillis = DateTime.now().toMillis()
        }
    }
})

export interface GlobalStoreState {
    nodes: AgentNode[]
    explorerItems: ExplorerItem[]
    currentFlowId: string
    currentExplorerPath: string
    activeExecutionId: string
    lastSavedNodeId: string
    isGrpcLoading: boolean
    isSignalRConnected: boolean
    errorKey: string
    lastSyncMillis: number
}

interface NodeSavedEventPayload {
    nodeId: string
}

interface AgentNodesLoadedEventPayload {
    flowId: string
    nodes: AgentNode[]
}

interface WorkflowStartedEventPayload {
    executionId: string
}

interface ExplorerLoadedEventPayload {
    path: string
    items: ExplorerItem[]
}