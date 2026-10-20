import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { GlobalViewModel } from './GlobalViewModel'
import { useGlobalStore } from './store'
import { NodeType, ItemType } from './clients/generated/agent_node'
import { useNotificationStore } from './notifications'

describe('End-to-End Frontend Workflow & Canvas State', () => {
    let vm: GlobalViewModel
    let store: ReturnType<typeof useGlobalStore>
    let notifStore: ReturnType<typeof useNotificationStore>

    beforeEach(() => {
        setActivePinia(createPinia())
        vm = new GlobalViewModel()
        store = useGlobalStore()
        notifStore = useNotificationStore()

        store.runGrpcCommand = async (_action: any) => {
            expect(notifStore).toBeDefined()
            return Promise.resolve()
        }
    })

    it('E2E Flow 1: Should add all 4 custom node types to the canvas and maintain correct metadata', async () => {
        // Arrange
        expect(vm.nodes).toHaveLength(0)

        // Act
        await vm.addNode(NodeType.AGENT, { x: 100, y: 100 }, 'Customer Support Agent')
        await vm.addNode(NodeType.MODEL, { x: 300, y: 100 }, 'GPT-4o Model')
        await vm.addNode(NodeType.PROMPT, { x: 100, y: 300 }, 'Helpful Assistant Persona')
        await vm.addNode(NodeType.MCP_CONNECTOR, { x: 300, y: 300 }, 'Web Search MCP')

        // Assert
        expect(vm.nodes).toHaveLength(4)
        expect(vm.nodes[0].name).toBe('Customer Support Agent')
        expect(vm.nodes[0].type).toBe(NodeType.AGENT)
        expect(vm.nodes[1].modelConfig?.modelName).toBe('gpt-4o')
        expect(vm.nodes[2].systemPrompt).toContain('You are an intelligent AI Agent.')
        expect(vm.nodes[3].mcpConfig?.serverUrl).toContain('mcp')
    })

    it('E2E Flow 2: Should connect nodes with edges and properly maintain topology', async () => {
        // Arrange
        await vm.addNode(NodeType.MODEL, { x: 100, y: 100 })
        await vm.addNode(NodeType.AGENT, { x: 400, y: 100 })

        const modelNode = vm.nodes[0]
        const agentNode = vm.nodes[1]

        const edge = {
            id: `edge_${modelNode.id}_${agentNode.id}`,
            flowId: 'default-flow',
            sourceNodeId: modelNode.id,
            sourceHandle: 'output',
            targetNodeId: agentNode.id,
            targetHandle: 'input'
        }

        // Act
        await vm.saveEdges([edge])

        // Assert
        expect(vm.edges).toHaveLength(1)
        expect(vm.edges[0].sourceNodeId).toBe(modelNode.id)
        expect(vm.edges[0].targetNodeId).toBe(agentNode.id)
    })

    it('E2E Flow 3: Should remove a node and cascade-delete connected edges', async () => {
        // Arrange
        await vm.addNode(NodeType.MODEL, { x: 100, y: 100 })
        await vm.addNode(NodeType.AGENT, { x: 400, y: 100 })

        const modelNode = vm.nodes[0]
        const agentNode = vm.nodes[1]

        store.edges = [{
            id: 'edge1',
            flowId: 'default-flow',
            sourceNodeId: modelNode.id,
            sourceHandle: '',
            targetNodeId: agentNode.id,
            targetHandle: ''
        }]

        expect(vm.nodes).toHaveLength(2)
        expect(vm.edges).toHaveLength(1)

        // Act
        await vm.removeNode(modelNode.id)

        // Assert
        expect(vm.nodes).toHaveLength(1)
        expect(vm.nodes[0].id).toBe(agentNode.id)
        expect(vm.edges).toHaveLength(0)
    })

    it('E2E Flow 4: Should handle execution status transitions and log streaming', () => {
        // Arrange
        expect(vm.executionStatus).toBe('Idle')
        expect(vm.executionLogs).toHaveLength(0)

        // Act
        store.executionStatus = 'Running'
        store.activeExecutionId = 'exec-test-123'
        store.executionLogs.push({
            timestamp: new Date().toISOString(),
            level: 'INFO',
            message: 'Initializing ChatClientAgent with OpenAI provider'
        })
        store.executionLogs.push({
            timestamp: new Date().toISOString(),
            level: 'INFO',
            message: 'Connecting to MCP Server at http://localhost:8000/mcp'
        })
        store.executionStatus = 'Completed'
        store.executionOutput = 'Agent completed task successfully.'

        // Assert
        expect(vm.activeExecutionId).toBe('exec-test-123')
        expect(vm.executionLogs).toHaveLength(2)
        expect(vm.executionStatus).toBe('Completed')
        expect(vm.executionOutput).toBe('Agent completed task successfully.')
    })

    it('E2E Flow 5: Should return appropriate icons and theme colors for node and explorer types', () => {
        // Arrange
        const agentColor = vm.getNodeColor(NodeType.AGENT)
        const modelColor = vm.getNodeColor(NodeType.MODEL)
        const promptColor = vm.getNodeColor(NodeType.PROMPT)
        const mcpColor = vm.getNodeColor(NodeType.MCP_CONNECTOR)

        // Act
        const agentIcon = vm.getNodeIcon(NodeType.AGENT)
        const modelIcon = vm.getNodeIcon(NodeType.MODEL)
        const promptIcon = vm.getNodeIcon(NodeType.PROMPT)
        const mcpIcon = vm.getNodeIcon(NodeType.MCP_CONNECTOR)

        // Assert
        expect(agentColor).toBe('#E63946')
        expect(modelColor).toBe('#2196F3')
        expect(promptColor).toBe('#FB8C00')
        expect(mcpColor).toBe('#4CAF50')

        expect(agentIcon).toBe('mdi-robot')
        expect(modelIcon).toBe('mdi-brain')
        expect(promptIcon).toBe('mdi-script-text-outline')
        expect(mcpIcon).toBe('mdi-connection')

        expect(vm.getExplorerItemIcon(ItemType.FILE)).toBe('mdi-file-code-outline')
        expect(vm.getExplorerItemIcon(ItemType.FOLDER)).toBe('mdi-folder')
        expect(vm.getExplorerItemColor(ItemType.FILE)).toBe('info')
        expect(vm.getExplorerItemColor(ItemType.FOLDER)).toBe('warning')
    })

    it('E2E Flow 6: Should create file and folder items and support drag-and-drop reordering and folder nesting', () => {
        // Arrange
        expect(vm.explorerItems).toHaveLength(0)

        // Act
        vm.createExplorerItem('main.agentflow', ItemType.FILE, '/')
        vm.createExplorerItem('config.json', ItemType.FILE, '/')
        vm.createExplorerItem('prompts', ItemType.FOLDER, '/')

        // Assert
        expect(vm.explorerItems).toHaveLength(3)
        expect(vm.explorerItems[0].name).toBe('main.agentflow')
        expect(vm.explorerItems[1].name).toBe('config.json')
        expect(vm.explorerItems[2].name).toBe('prompts')

        // Act
        vm.reorderExplorerItems(0, 1)

        // Assert
        expect(vm.explorerItems[0].name).toBe('config.json')
        expect(vm.explorerItems[1].name).toBe('main.agentflow')

        // Act
        const targetFolder = vm.explorerItems[2]
        vm.moveExplorerItem(1, targetFolder)

        // Assert
        expect(vm.explorerItems[1].path).toBe('/prompts/main.agentflow')
    })

    it('E2E Flow 7: Should add node with custom argument parameters via drag and drop payload', async () => {
        // Arrange
        const initialCount = vm.nodes.length

        // Act
        await vm.addNode(
            NodeType.MODEL,
            { x: 220, y: 180 },
            'OpenAI GPT-4o Preset',
            {
                modelConfig: { provider: 'OpenAI', modelName: 'gpt-4o', temperature: 0.3 }
            }
        )

        await vm.addNode(
            NodeType.PROMPT,
            { x: 400, y: 180 },
            'User Input Argument',
            {
                systemPrompt: 'Argument query: {{input}}'
            }
        )

        // Assert
        expect(vm.nodes).toHaveLength(initialCount + 2)
        const modelNode = vm.nodes.find(n => n.name === 'OpenAI GPT-4o Preset')
        expect(modelNode).toBeDefined()
        expect(modelNode?.modelConfig?.modelName).toBe('gpt-4o')
        expect(modelNode?.modelConfig?.temperature).toBe(0.3)

        const promptNode = vm.nodes.find(n => n.name === 'User Input Argument')
        expect(promptNode).toBeDefined()
        expect(promptNode?.systemPrompt).toBe('Argument query: {{input}}')
    })
})
