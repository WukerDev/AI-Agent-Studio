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

    it('E2E Flow 8: Should add Note and Label nodes with proper default properties, colors, and icons', async () => {
        await vm.addNode(NodeType.NOTE, { x: 150, y: 150 }, 'Release Checklist', { systemPrompt: 'Verify prod keys' })
        await vm.addNode(NodeType.LABEL, { x: 350, y: 150 }, 'Agent Operations Section', { endpoint: 'large' })

        expect(vm.nodes).toHaveLength(2)
        const note = vm.nodes[0]
        const label = vm.nodes[1]

        expect(note.type).toBe(NodeType.NOTE)
        expect(note.name).toBe('Release Checklist')
        expect(note.systemPrompt).toBe('Verify prod keys')
        expect(vm.getNodeIcon(NodeType.NOTE)).toBe('mdi-note-text-outline')
        expect(vm.getNodeColor(NodeType.NOTE)).toBe('#F59E0B')

        expect(label.type).toBe(NodeType.LABEL)
        expect(label.name).toBe('Agent Operations Section')
        expect(label.endpoint).toBe('large')
        expect(vm.getNodeIcon(NodeType.LABEL)).toBe('mdi-format-title')
        expect(vm.getNodeColor(NodeType.LABEL)).toBe('#8B5CF6')
    })

    it('E2E Flow 9: Should handle Undo and Redo operations across node additions and removals', async () => {
        expect(vm.canUndo).toBe(false)
        expect(vm.canRedo).toBe(false)

        await vm.addNode(NodeType.AGENT, { x: 100, y: 100 }, 'Agent Alpha')
        expect(vm.nodes).toHaveLength(1)
        expect(vm.canUndo).toBe(true)
        expect(vm.canRedo).toBe(false)

        await vm.addNode(NodeType.NOTE, { x: 200, y: 100 }, 'Note Beta')
        expect(vm.nodes).toHaveLength(2)

        // Undo adding Note Beta
        await vm.undo()
        expect(vm.nodes).toHaveLength(1)
        expect(vm.nodes[0].name).toBe('Agent Alpha')
        expect(vm.canRedo).toBe(true)

        // Redo adding Note Beta
        await vm.redo()
        expect(vm.nodes).toHaveLength(2)
        expect(vm.nodes[1].name).toBe('Note Beta')

        // Undo Alpha and Beta
        await vm.undo()
        await vm.undo()
        expect(vm.nodes).toHaveLength(0)
    })

    it('E2E Flow 10: Should support Copy, Cut, Paste, Delete, and Arrow Key movement for selected nodes', async () => {
        await vm.addNode(NodeType.AGENT, { x: 100, y: 100 }, 'Worker 1')
        await vm.addNode(NodeType.MODEL, { x: 250, y: 100 }, 'Worker 2')

        const node1 = vm.nodes[0]
        const node2 = vm.nodes[1]

        // Move with Arrow Keys (+10px X, -10px Y)
        await vm.moveSelected([node1.id, node2.id], 10, -10)
        expect(vm.nodes[0].position?.x).toBe(110)
        expect(vm.nodes[0].position?.y).toBe(90)
        expect(vm.nodes[1].position?.x).toBe(260)
        expect(vm.nodes[1].position?.y).toBe(90)

        // Copy selected nodes
        const copiedCount = vm.copySelected([node1.id, node2.id])
        expect(copiedCount).toBe(2)

        // Paste copies with offset (+30px)
        const pasted = await vm.paste({ x: 30, y: 30 })
        expect(pasted).toHaveLength(2)
        expect(vm.nodes).toHaveLength(4)
        expect(pasted[0].position?.x).toBe(140)
        expect(pasted[0].name).toContain('(Copy)')

        // Cut first 2 nodes
        const cutCount = await vm.cutSelected([node1.id, node2.id])
        expect(cutCount).toBe(2)
        expect(vm.nodes).toHaveLength(2)

        // Delete remaining nodes
        const remainingIds = vm.nodes.map(n => n.id)
        const deletedCount = await vm.deleteSelected(remainingIds)
        expect(deletedCount).toBe(2)
        expect(vm.nodes).toHaveLength(0)
    })

    it('E2E Flow 11: Should manage Inspector state in shell when clicking, selecting and removing nodes', async () => {
        expect(vm.isInspectorOpen).toBe(false)
        expect(vm.selectedNodeId).toBeNull()
        expect(vm.selectedNode).toBeNull()

        await vm.addNode(NodeType.AGENT, { x: 100, y: 100 }, 'Agent Delta')
        const agentNode = vm.nodes[0]

        // Open inspector for Agent Delta
        vm.openInspector(agentNode.id)
        expect(vm.isInspectorOpen).toBe(true)
        expect(vm.selectedNodeId).toBe(agentNode.id)
        expect(vm.selectedNode?.name).toBe('Agent Delta')

        // Switch to newly created note
        await vm.addNode(NodeType.NOTE, { x: 300, y: 100 }, 'Note Delta')
        const noteNode = vm.nodes[1]
        vm.openInspector(noteNode.id)
        expect(vm.isInspectorOpen).toBe(true)
        expect(vm.selectedNodeId).toBe(noteNode.id)
        expect(vm.selectedNode?.name).toBe('Note Delta')

        // Removing active node automatically closes inspector
        await vm.removeNode(noteNode.id)
        expect(vm.isInspectorOpen).toBe(false)
        expect(vm.selectedNodeId).toBeNull()

        // Re-open and close explicitly
        vm.openInspector(agentNode.id)
        expect(vm.isInspectorOpen).toBe(true)
        vm.closeInspector()
        expect(vm.isInspectorOpen).toBe(false)
        expect(vm.selectedNodeId).toBeNull()
    })

    it('E2E Flow 12: Should switch active workflow and clear canvas state when switching flow', async () => {
        expect(vm.currentFlowId).toBe('default-flow')

        // Open inspector on an existing node
        await vm.addNode(NodeType.AGENT, { x: 50, y: 50 }, 'Flow 1 Agent')
        const node = vm.nodes[0]
        vm.openInspector(node.id)
        expect(vm.isInspectorOpen).toBe(true)

        // Switch to a new workflow
        await vm.switchWorkflow('secondary-flow')
        expect(vm.currentFlowId).toBe('secondary-flow')
        expect(vm.isInspectorOpen).toBe(false)
        expect(vm.selectedNodeId).toBeNull()
        expect(vm.canUndo).toBe(false)
        expect(vm.canRedo).toBe(false)

        // Adding node in new workflow assigns correct flowId
        await vm.addNode(NodeType.MODEL, { x: 120, y: 120 }, 'Secondary Model')
        expect(vm.nodes).toHaveLength(1)
        expect(vm.nodes[0].flowId).toBe('secondary-flow')

        // Switch back to original workflow
        await vm.switchWorkflow('default-flow')
        expect(vm.currentFlowId).toBe('default-flow')
    })
})