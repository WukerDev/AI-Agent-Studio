import { DateTime } from 'luxon'
import { useGlobalStore, type ExecutionLog } from './store'
import {
    NodeType,
    ItemType,
    type AgentNode,
    type WorkflowEdge,
    type ExplorerItem
} from './clients/generated/agent_node'

export interface HistorySnapshot {
    nodes: AgentNode[]
    edges: WorkflowEdge[]
}

export class GlobalViewModel {
    private globalStore = useGlobalStore()

    private undoStack: HistorySnapshot[] = []
    private redoStack: HistorySnapshot[] = []
    private clipboard: { nodes: AgentNode[], edges: WorkflowEdge[] } | null = null
    private readonly maxHistory = 30

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

    public get canUndo(): boolean {
        return this.undoStack.length > 0
    }

    public get canRedo(): boolean {
        return this.redoStack.length > 0
    }

    public get selectedNodeId(): string | null {
        return this.globalStore.selectedNodeId
    }

    public get isInspectorOpen(): boolean {
        return this.globalStore.isInspectorOpen
    }

    public set isInspectorOpen(val: boolean) {
        this.globalStore.setInspectorOpen(val)
    }

    public get selectedNode(): AgentNode | null {
        if (!this.globalStore.selectedNodeId) return null
        return this.globalStore.nodes.find(n => n.id === this.globalStore.selectedNodeId) || null
    }

    public openInspector(nodeId: string): void {
        this.globalStore.openInspector(nodeId)
    }

    public closeInspector(): void {
        this.globalStore.closeInspector()
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

    public pushHistorySnapshot(): void {
        const snapshot: HistorySnapshot = {
            nodes: JSON.parse(JSON.stringify(this.globalStore.nodes)),
            edges: JSON.parse(JSON.stringify(this.globalStore.edges))
        }
        this.undoStack.push(snapshot)
        if (this.undoStack.length > this.maxHistory) {
            this.undoStack.shift()
        }
        this.redoStack = []
    }

    public async undo(): Promise<void> {
        if (this.undoStack.length === 0) return
        const previous = this.undoStack.pop()!
        const current: HistorySnapshot = {
            nodes: JSON.parse(JSON.stringify(this.globalStore.nodes)),
            edges: JSON.parse(JSON.stringify(this.globalStore.edges))
        }
        this.redoStack.push(current)

        this.globalStore.nodes = previous.nodes
        this.globalStore.edges = previous.edges
        await this.syncCanvasToBackend()
    }

    public async redo(): Promise<void> {
        if (this.redoStack.length === 0) return
        const next = this.redoStack.pop()!
        const current: HistorySnapshot = {
            nodes: JSON.parse(JSON.stringify(this.globalStore.nodes)),
            edges: JSON.parse(JSON.stringify(this.globalStore.edges))
        }
        this.undoStack.push(current)

        this.globalStore.nodes = next.nodes
        this.globalStore.edges = next.edges
        await this.syncCanvasToBackend()
    }

    private async syncCanvasToBackend(): Promise<void> {
        const flowId = this.globalStore.currentFlowId || 'default-flow'
        for (const node of this.globalStore.nodes) {
            await this.saveNode(node)
        }
        await this.saveEdges(this.globalStore.edges, flowId)
    }

    public copySelected(nodeIds: string[]): number {
        if (!nodeIds || nodeIds.length === 0) return 0
        const idSet = new Set(nodeIds)
        const selectedNodes = this.globalStore.nodes.filter(n => idSet.has(n.id))
        const selectedEdges = this.globalStore.edges.filter(
            e => idSet.has(e.sourceNodeId) && idSet.has(e.targetNodeId)
        )
        this.clipboard = {
            nodes: JSON.parse(JSON.stringify(selectedNodes)),
            edges: JSON.parse(JSON.stringify(selectedEdges))
        }
        return selectedNodes.length
    }

    public async cutSelected(nodeIds: string[]): Promise<number> {
        const count = this.copySelected(nodeIds)
        if (count > 0) {
            await this.deleteSelected(nodeIds)
        }
        return count
    }

    public async paste(offset = { x: 30, y: 30 }): Promise<AgentNode[]> {
        if (!this.clipboard || this.clipboard.nodes.length === 0) return []
        this.pushHistorySnapshot()

        const idMap: Record<string, string> = {}
        const newNodes: AgentNode[] = []

        for (const originalNode of this.clipboard.nodes) {
            const newId = `node_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`
            idMap[originalNode.id] = newId

            const cloned: AgentNode = {
                ...JSON.parse(JSON.stringify(originalNode)),
                id: newId,
                name: `${originalNode.name} (Copy)`,
                position: {
                    x: (originalNode.position?.x ?? 0) + offset.x,
                    y: (originalNode.position?.y ?? 0) + offset.y
                },
                flowId: this.globalStore.currentFlowId || 'default-flow'
            }
            newNodes.push(cloned)
        }

        const newEdges: WorkflowEdge[] = []
        for (const originalEdge of this.clipboard.edges) {
            const newSource = idMap[originalEdge.sourceNodeId]
            const newTarget = idMap[originalEdge.targetNodeId]
            if (newSource && newTarget) {
                newEdges.push({
                    ...JSON.parse(JSON.stringify(originalEdge)),
                    id: `e_${newSource}_${newTarget}`,
                    flowId: this.globalStore.currentFlowId || 'default-flow',
                    sourceNodeId: newSource,
                    targetNodeId: newTarget
                })
            }
        }

        this.globalStore.nodes = [...this.globalStore.nodes, ...newNodes]
        this.globalStore.edges = [...this.globalStore.edges, ...newEdges]

        for (const node of newNodes) {
            await this.saveNode(node)
        }
        if (newEdges.length > 0) {
            await this.saveEdges(this.globalStore.edges)
        }

        return newNodes
    }

    public async deleteSelected(nodeIds: string[]): Promise<number> {
        if (!nodeIds || nodeIds.length === 0) return 0
        this.pushHistorySnapshot()

        const idSet = new Set(nodeIds)
        if (this.globalStore.selectedNodeId && idSet.has(this.globalStore.selectedNodeId)) {
            this.closeInspector()
        }
        const count = this.globalStore.nodes.filter(n => idSet.has(n.id)).length
        this.globalStore.nodes = this.globalStore.nodes.filter(n => !idSet.has(n.id))
        this.globalStore.edges = this.globalStore.edges.filter(
            e => !idSet.has(e.sourceNodeId) && !idSet.has(e.targetNodeId)
        )
        await this.saveEdges(this.globalStore.edges)
        return count
    }

    public async moveSelected(nodeIds: string[], dx: number, dy: number): Promise<void> {
        if (!nodeIds || nodeIds.length === 0 || (dx === 0 && dy === 0)) return
        const idSet = new Set(nodeIds)
        for (const node of this.globalStore.nodes) {
            if (idSet.has(node.id)) {
                if (!node.position) node.position = { x: 0, y: 0 }
                node.position.x += dx
                node.position.y += dy
            }
        }
        this.globalStore.nodes = [...this.globalStore.nodes]
        for (const node of this.globalStore.nodes) {
            if (idSet.has(node.id)) {
                await this.saveNode(node)
            }
        }
    }

    public async addNode(type: NodeType, position = { x: 250, y: 150 }, name?: string, customConfig?: Partial<AgentNode>): Promise<void> {
        this.pushHistorySnapshot()

        const defaultNames: Record<NodeType, string> = {
            [NodeType.UNSPECIFIED]: 'New Node',
            [NodeType.AGENT]: 'AI Agent',
            [NodeType.MODEL]: 'LLM Model',
            [NodeType.PROMPT]: 'System Prompt',
            [NodeType.MCP_CONNECTOR]: 'MCP Tools Connector',
            [NodeType.NOTE]: 'Sticky Note',
            [NodeType.LABEL]: 'Section Label'
        }

        let defaultPrompt = ''
        if (type === NodeType.PROMPT) defaultPrompt = 'You are an intelligent AI Agent.'
        if (type === NodeType.NOTE) defaultPrompt = 'Notes or workflow annotations...'

        let defaultEndpoint = ''
        if (type === NodeType.NOTE) defaultEndpoint = '#F59E0B'
        if (type === NodeType.LABEL) defaultEndpoint = 'medium'

        let defaultModelConfig = undefined
        if (type === NodeType.MODEL) {
            defaultModelConfig = {
                provider: 'OpenAI',
                modelName: 'gpt-4o',
                temperature: 0.7
            }
        } else if (type === NodeType.LABEL) {
            defaultModelConfig = {
                provider: '#8B5CF6',
                modelName: '',
                temperature: 0
            }
        }

        const newNode: AgentNode = {
            id: `node_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`,
            name: name || defaultNames[type] || 'New Node',
            type,
            position,
            flowId: this.globalStore.currentFlowId || 'default-flow',
            systemPrompt: customConfig?.systemPrompt ?? defaultPrompt,
            endpoint: customConfig?.endpoint ?? defaultEndpoint,
            modelConfig: customConfig?.modelConfig ?? defaultModelConfig,
            mcpConfig: customConfig?.mcpConfig ?? (type === NodeType.MCP_CONNECTOR ? {
                serverUrl: 'http://localhost:8000/mcp',
                authToken: '',
                enabledTools: []
            } : undefined),
            ...customConfig
        }
        newNode.position = position
        newNode.type = type
        if (name) newNode.name = name

        this.globalStore.nodes = [...this.globalStore.nodes, newNode]
        await this.saveNode(newNode)
    }

    public async removeNode(nodeId: string): Promise<void> {
        this.pushHistorySnapshot()
        if (this.globalStore.selectedNodeId === nodeId) {
            this.closeInspector()
        }
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
            case NodeType.NOTE:
                return '#F59E0B'
            case NodeType.LABEL:
                return '#8B5CF6'
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
            case NodeType.NOTE:
                return 'mdi-note-text-outline'
            case NodeType.LABEL:
                return 'mdi-format-title'
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
