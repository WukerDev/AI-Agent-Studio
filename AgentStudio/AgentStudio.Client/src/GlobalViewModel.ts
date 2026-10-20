import { DateTime } from 'luxon'
import { useGlobalStore, type ExecutionLog } from './store'
import {
    NodeType,
    ItemType,
    type AgentNode,
    type WorkflowEdge,
    type ExplorerItem
} from './clients/generated/agent_node'

export class GlobalViewModel {
    private globalStore = useGlobalStore()

    public get nodes(): AgentNode[] {
        return this.globalStore.nodes
    }

    public get edges(): WorkflowEdge[] {
        return this.globalStore.edges
    }

    public get explorerItems(): ExplorerItem[] {
        return this.globalStore.explorerItems
    }

    public get currentFlowId(): string {
        return this.globalStore.currentFlowId
    }

    public get currentExplorerPath(): string {
        return this.globalStore.currentExplorerPath
    }

    public get activeExecutionId(): string {
        return this.globalStore.activeExecutionId
    }

    public get isGrpcLoading(): boolean {
        return this.globalStore.isGrpcLoading
    }

    public get isSignalRConnected(): boolean {
        return this.globalStore.isSignalRConnected
    }

    public get errorKey(): string {
        return this.globalStore.errorKey
    }

    public get executionLogs(): ExecutionLog[] {
        return this.globalStore.executionLogs
    }

    public get executionStatus(): string {
        return this.globalStore.executionStatus
    }

    public get executionOutput(): string {
        return this.globalStore.executionOutput
    }

    public async initialize(): Promise<void> {
        await this.globalStore.initializeRealtimeAndGrpc()
    }

    public async executeWorkflow(flowId?: string, inputMessage?: string): Promise<void> {
        const targetFlowId = flowId || this.globalStore.currentFlowId || 'default-flow'
        await this.globalStore.executeWorkflow({ flowId: targetFlowId, inputMessage: inputMessage ?? '' })
    }

    public async loadExplorer(path: string): Promise<void> {
        await this.globalStore.getExplorer({ path })
    }

    public createExplorerItem(name: string, type: ItemType, parentPath = '/'): void {
        const cleanParent = parentPath.endsWith('/') && parentPath.length > 1 ? parentPath.slice(0, -1) : parentPath
        const item: ExplorerItem = {
            name,
            path: cleanParent === '/' ? `/${name}` : `${cleanParent}/${name}`,
            type
        }
        this.globalStore.addExplorerItem(item)
    }

    public reorderExplorerItems(fromIndex: number, toIndex: number): void {
        this.globalStore.reorderExplorerItems(fromIndex, toIndex)
    }

    public moveExplorerItem(sourceIndex: number, targetFolder: ExplorerItem): void {
        this.globalStore.moveExplorerItem(sourceIndex, targetFolder)
    }

    public async saveNode(node: AgentNode): Promise<void> {
        await this.globalStore.saveAgentNode({
            node,
            flowId: this.globalStore.currentFlowId || 'default-flow'
        })
    }

    public async loadNodes(flowId: string): Promise<void> {
        await this.globalStore.getAgentNodes({ flowId })
    }

    public async saveEdges(edges: WorkflowEdge[], flowId?: string): Promise<void> {
        const targetFlowId = flowId || this.globalStore.currentFlowId || 'default-flow'
        await this.globalStore.saveWorkflowEdges({ flowId: targetFlowId, edges })
    }

    public async loadEdges(flowId: string): Promise<void> {
        await this.globalStore.getWorkflowEdges({ flowId })
    }

    public async addNode(type: NodeType, position = { x: 250, y: 150 }, name?: string, customConfig?: Partial<AgentNode>): Promise<void> {
        const defaultNames: Record<NodeType, string> = {
            [NodeType.UNSPECIFIED]: 'New Node',
            [NodeType.AGENT]: 'AI Agent',
            [NodeType.MODEL]: 'LLM Model',
            [NodeType.PROMPT]: 'System Prompt',
            [NodeType.MCP_CONNECTOR]: 'MCP Tools Connector'
        }

        const newNode: AgentNode = {
            id: `node_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`,
            name: name || defaultNames[type] || 'New Node',
            type,
            position,
            flowId: this.globalStore.currentFlowId || 'default-flow',
            systemPrompt: customConfig?.systemPrompt ?? (type === NodeType.PROMPT ? 'You are an intelligent AI Agent.' : ''),
            endpoint: customConfig?.endpoint ?? '',
            modelConfig: customConfig?.modelConfig ?? (type === NodeType.MODEL ? {
                provider: 'OpenAI',
                modelName: 'gpt-4o',
                temperature: 0.7
            } : undefined),
            mcpConfig: customConfig?.mcpConfig ?? (type === NodeType.MCP_CONNECTOR ? {
                serverUrl: 'http://localhost:8000/mcp',
                authToken: '',
                enabledTools: []
            } : undefined),
            ...customConfig
        }
        newNode.id = `node_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`
        newNode.position = position
        newNode.type = type
        if (name) newNode.name = name

        this.globalStore.nodes = [...this.globalStore.nodes, newNode]
        await this.saveNode(newNode)
    }

    public async removeNode(nodeId: string): Promise<void> {
        this.globalStore.nodes = this.globalStore.nodes.filter(n => n.id !== nodeId)
        this.globalStore.edges = this.globalStore.edges.filter(
            e => e.sourceNodeId !== nodeId && e.targetNodeId !== nodeId
        )
        await this.saveEdges(this.globalStore.edges)
    }

    public getNodeColor(type: NodeType): string {
        switch (type) {
            case NodeType.AGENT:
                return '#E63946'
            case NodeType.MODEL:
                return '#2196F3'
            case NodeType.PROMPT:
                return '#FB8C00'
            case NodeType.MCP_CONNECTOR:
                return '#4CAF50'
            default:
                return '#9E9E9E'
        }
    }

    public getNodeIcon(type: NodeType): string {
        switch (type) {
            case NodeType.AGENT:
                return 'mdi-robot'
            case NodeType.MODEL:
                return 'mdi-brain'
            case NodeType.PROMPT:
                return 'mdi-script-text-outline'
            case NodeType.MCP_CONNECTOR:
                return 'mdi-connection'
            default:
                return 'mdi-shape'
        }
    }

    public getExplorerItemColor(type: ItemType): string {
        switch (type) {
            case ItemType.FOLDER:
                return 'warning'
            case ItemType.FILE:
                return 'info'
            default:
                return 'secondary'
        }
    }

    public getExplorerItemIcon(type: ItemType): string {
        switch (type) {
            case ItemType.FOLDER:
                return 'mdi-folder'
            case ItemType.FILE:
                return 'mdi-file-code-outline'
            default:
                return 'mdi-file-question-outline'
        }
    }

    public getFormattedLastSync(): string {
        if (this.globalStore.lastSyncMillis === 0) {
            return ''
        }

        return DateTime.fromMillis(this.globalStore.lastSyncMillis)
            .setLocale('pl')
            .toLocaleString(DateTime.TIME_WITH_SECONDS)
    }

    public formatTimestamp(isoString: string): string {
        if (!isoString) return ''
        return DateTime.fromISO(isoString)
            .setLocale('pl')
            .toFormat('HH:mm:ss')
    }
}
