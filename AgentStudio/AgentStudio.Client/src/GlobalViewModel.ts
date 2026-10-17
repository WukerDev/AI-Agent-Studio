import { DateTime } from 'luxon'
import { useGlobalStore } from './store'
import {
    NodeType,
    ItemType,
    type AgentNode,
    type ExplorerItem
} from './clients/generated/agent_node'

export class GlobalViewModel {
    private globalStore = useGlobalStore()

    public get nodes(): AgentNode[] {
        return this.globalStore.nodes
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

    public async initialize(): Promise<void> {
        await this.globalStore.initializeRealtimeAndGrpc()
    }

    public async executeWorkflow(flowId: string): Promise<void> {
        await this.globalStore.executeWorkflow({ flowId })
    }

    public async loadExplorer(path: string): Promise<void> {
        await this.globalStore.getExplorer({ path })
    }

    public async saveNode(node: AgentNode): Promise<void> {
        await this.globalStore.saveAgentNode({ node })
    }

    public async loadNodes(flowId: string): Promise<void> {
        await this.globalStore.getAgentNodes({ flowId })
    }

    public getNodeColor(type: NodeType): string {
        switch (type) {
            case NodeType.AGENT:
                return 'primary'
            case NodeType.MODEL:
                return 'info'
            case NodeType.PROMPT:
                return 'warning'
            case NodeType.MCP_CONNECTOR:
                return 'success'
            default:
                return 'secondary'
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
}