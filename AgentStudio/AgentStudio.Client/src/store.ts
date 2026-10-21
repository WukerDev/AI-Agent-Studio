import { defineStore } from 'pinia'
import * as signalR from '@microsoft/signalr'
import type { RpcOptions } from '@protobuf-ts/runtime-rpc'
import { DateTime } from 'luxon'
import { getGrpcClient, createGrpcOptions } from './clients/grpc'
import { AgentFlowServiceClient } from './clients/generated/agent_node.client'
import type {
    AgentNode,
    ExplorerItem,
    WorkflowEdge,
    ExecuteWorkflowRequest,
    GetExplorerRequest,
    SaveAgentNodeRequest,
    GetAgentNodesRequest,
    SaveWorkflowEdgesRequest,
    GetWorkflowEdgesRequest
} from './clients/generated/agent_node'
import { useConfig } from './config'
import { useAuth } from './auth'
import { useNotificationStore } from './notifications'

let hubConnection: signalR.HubConnection | null = null

export interface ExecutionLog {
    timestamp: string
    level: string
    message: string
    data?: any
}

export interface GlobalStoreState {
    nodes: AgentNode[]
    edges: WorkflowEdge[]
    explorerItems: ExplorerItem[]
    currentFlowId: string
    currentExplorerPath: string
    activeExecutionId: string
    lastSavedNodeId: string
    isGrpcLoading: boolean
    isSignalRConnected: boolean
    errorKey: string
    lastSyncMillis: number
    executionLogs: ExecutionLog[]
    executionStatus: string
    executionOutput: string
    selectedNodeId: string | null
    isInspectorOpen: boolean
}

export const useGlobalStore = defineStore('global', {
    state: (): GlobalStoreState => ({
        nodes: [],
        edges: [],
        explorerItems: [],
        currentFlowId: 'default-flow',
        currentExplorerPath: '/',
        activeExecutionId: '',
        lastSavedNodeId: '',
        isGrpcLoading: false,
        isSignalRConnected: false,
        errorKey: '',
        lastSyncMillis: 0,
        executionLogs: [],
        executionStatus: 'Idle',
        executionOutput: '',
        selectedNodeId: null,
        isInspectorOpen: false
    }),
    actions: {
        openInspector(nodeId: string): void {
            this.selectedNodeId = nodeId
            this.isInspectorOpen = true
        },
        closeInspector(): void {
            this.isInspectorOpen = false
            this.selectedNodeId = null
        },
        setInspectorOpen(isOpen: boolean): void {
            this.isInspectorOpen = isOpen
            if (!isOpen) {
                this.selectedNodeId = null
            }
        },
        async initializeRealtimeAndGrpc(): Promise<void> {
            this.setupGrpcClient()
            await this.connectSignalR()
        },
        async executeWorkflow(request: ExecuteWorkflowRequest): Promise<void> {
            const notif = useNotificationStore()
            this.executionLogs = []
            this.executionStatus = 'Starting'
            this.executionOutput = ''

            await this.runGrpcCommand(async (client, options) => {
                const call = await client.executeWorkflow(request, options)
                if (call.response.success) {
                    this.activeExecutionId = call.response.executionId
                    this.executionStatus = 'Running'
                    notif.info(`Agent flow started: ${call.response.executionId.substring(0, 8)}...`)
                } else {
                    this.errorKey = 'error.grpc.execute_workflow_failed'
                    this.executionStatus = 'Failed'
                    notif.error('Workflow execution trigger failed')
                }
            })
        },
        async getExplorer(request: GetExplorerRequest): Promise<void> {
            this.currentExplorerPath = request.path || '/'
            await this.runGrpcCommand(async (client, options) => {
                const call = await client.getExplorer(request, options)
                if (call.response.success && call.response.items) {
                    this.explorerItems = call.response.items
                } else if (!call.response.success) {
                    this.errorKey = 'error.grpc.get_explorer_failed'
                }
            })
        },
        addExplorerItem(item: ExplorerItem): void {
            this.explorerItems.push(item)
        },
        reorderExplorerItems(fromIndex: number, toIndex: number): void {
            if (fromIndex < 0 || toIndex < 0 || fromIndex >= this.explorerItems.length || toIndex >= this.explorerItems.length) return
            const [moved] = this.explorerItems.splice(fromIndex, 1)
            this.explorerItems.splice(toIndex, 0, moved)
        },
        moveExplorerItem(sourceIndex: number, targetFolder: ExplorerItem): void {
            if (sourceIndex < 0 || sourceIndex >= this.explorerItems.length) return
            const item = this.explorerItems[sourceIndex]
            item.path = `${targetFolder.path.replace(/\/$/, '')}/${item.name}`
        },
        async saveAgentNode(request: SaveAgentNodeRequest): Promise<void> {
            if (request.node) {
                const idx = this.nodes.findIndex(n => n.id === request.node?.id)
                if (idx >= 0) {
                    this.nodes[idx] = request.node
                } else {
                    this.nodes.push(request.node)
                }
            }
            const notif = useNotificationStore()
            await this.runGrpcCommand(async (client, options) => {
                const call = await client.saveAgentNode(request, options)
                if (call.response.success) {
                    this.lastSavedNodeId = call.response.id
                    notif.success(`Node "${request.node?.name || call.response.id}" saved`)
                } else {
                    this.errorKey = 'error.grpc.save_node_failed'
                    notif.error('Failed to save node')
                }
            })
        },
        async getAgentNodes(request: GetAgentNodesRequest): Promise<void> {
            if (request.flowId) {
                this.currentFlowId = request.flowId
            }
            await this.runGrpcCommand(async (client, options) => {
                const call = await client.getAgentNodes(request, options)
                if (call.response.success && call.response.nodes) {
                    this.nodes = call.response.nodes
                } else if (!call.response.success) {
                    this.errorKey = 'error.grpc.get_nodes_failed'
                }
            })
        },
        async saveWorkflowEdges(request: SaveWorkflowEdgesRequest): Promise<void> {
            this.edges = request.edges
            const notif = useNotificationStore()
            await this.runGrpcCommand(async (client, options) => {
                const call = await client.saveWorkflowEdges(request, options)
                if (call.response.success) {
                    notif.success(`Workflow connections saved (${request.edges.length})`)
                } else {
                    this.errorKey = 'error.grpc.save_edges_failed'
                    notif.error('Failed to save connections')
                }
            })
        },
        async getWorkflowEdges(request: GetWorkflowEdgesRequest): Promise<void> {
            if (request.flowId) {
                this.currentFlowId = request.flowId
            }
            await this.runGrpcCommand(async (client, options) => {
                const call = await client.getWorkflowEdges(request, options)
                if (call.response.success && call.response.edges) {
                    this.edges = call.response.edges
                } else if (!call.response.success) {
                    this.errorKey = 'error.grpc.get_edges_failed'
                }
            })
        },
        setCurrentFlowId(flowId: string): void {
            this.currentFlowId = flowId
        },
        async switchFlow(flowId: string): Promise<void> {
            this.currentFlowId = flowId
            this.selectedNodeId = null
            this.isInspectorOpen = false
            this.nodes = []
            this.edges = []
            await this.getAgentNodes({ flowId })
            await this.getWorkflowEdges({ flowId })
        },
        setupGrpcClient(): void {
            getGrpcClient()
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

            const notif = useNotificationStore()

            hubConnection.on('NodeSaved', (payload: NodeSavedEventPayload) => {
                this.lastSavedNodeId = payload.nodeId
                this.updateSyncTimestamp()
            })

            hubConnection.on('AgentNodesLoaded', (payload: AgentNodesLoadedEventPayload) => {
                this.currentFlowId = payload.flowId
                this.nodes = payload.nodes
                this.updateSyncTimestamp()
            })

            hubConnection.on('WorkflowEdgesLoaded', (payload: WorkflowEdgesLoadedEventPayload) => {
                this.currentFlowId = payload.flowId
                this.edges = payload.edges
                this.updateSyncTimestamp()
            })

            hubConnection.on('WorkflowEdgesSaved', () => {
                this.updateSyncTimestamp()
            })

            hubConnection.on('WorkflowStarted', (payload: WorkflowStartedEventPayload) => {
                this.activeExecutionId = payload.executionId
                this.executionStatus = 'Running'
                this.updateSyncTimestamp()
            })

            hubConnection.on('WorkflowExecutionProgress', (payload: WorkflowExecutionProgressPayload) => {
                if (payload.log) {
                    this.executionLogs.push(payload.log)
                }
                this.updateSyncTimestamp()
            })

            hubConnection.on('WorkflowExecutionFinished', (payload: WorkflowExecutionFinishedPayload) => {
                this.executionStatus = payload.success ? 'Completed' : 'Failed'
                if (payload.output) {
                    this.executionOutput = payload.output
                }
                this.updateSyncTimestamp()

                if (payload.success) {
                    notif.success('Agent Workflow completed execution!')
                } else {
                    notif.error(payload.error || 'Agent Workflow failed.')
                }
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
                notif.info('Realtime SignalR reconnected')
            })
        },
        async runGrpcCommand(
            action: (client: AgentFlowServiceClient, options: RpcOptions) => Promise<void>
        ): Promise<void> {
            const client = getGrpcClient()
            const notif = useNotificationStore()

            this.isGrpcLoading = true
            this.errorKey = ''

            try {
                const options = createGrpcOptions()
                await action(client, options)
            } catch (err: any) {
                console.error('[gRPC Call Error]', err)
                this.errorKey = 'error.grpc.request_failed'
                notif.error(`gRPC error: ${err?.message || 'Request failed'}`)
            } finally {
                this.isGrpcLoading = false
            }
        },
        createAuthOptions(): RpcOptions {
            return createGrpcOptions()
        },
        updateSyncTimestamp(): void {
            this.lastSyncMillis = DateTime.now().toMillis()
        }
    }
})

interface NodeSavedEventPayload {
    nodeId: string
    flowId?: string
}

interface AgentNodesLoadedEventPayload {
    flowId: string
    nodes: AgentNode[]
}

interface WorkflowEdgesLoadedEventPayload {
    flowId: string
    edges: WorkflowEdge[]
}

interface WorkflowStartedEventPayload {
    executionId: string
    flowId?: string
}

interface WorkflowExecutionProgressPayload {
    executionId: string
    flowId: string
    log: ExecutionLog
}

interface WorkflowExecutionFinishedPayload {
    executionId: string
    success: boolean
    output?: string
    error?: string
}

interface ExplorerLoadedEventPayload {
    path: string
    items: ExplorerItem[]
}
