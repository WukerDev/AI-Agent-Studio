<script setup lang="ts">
defineOptions({
  name: 'WorkflowCanvas'
})

import { ref, shallowRef, watch, computed } from 'vue'
import { VueFlow, useVueFlow, type Node, type Edge, type Connection } from '@vue-flow/core'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'
import { MiniMap } from '@vue-flow/minimap'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import '@vue-flow/controls/dist/style.css'
import '@vue-flow/minimap/dist/style.css'
import { GlobalViewModel } from '../../GlobalViewModel'
import { NodeType, type AgentNode as AgentNodeModel } from '../../clients/generated/agent_node'
import AgentNode from './nodes/AgentNode.vue'
import ModelNode from './nodes/ModelNode.vue'
import PromptNode from './nodes/PromptNode.vue'
import McpNode from './nodes/McpNode.vue'
import NodeConfigDialog from './NodeConfigDialog.vue'
import { useNotificationStore } from '../../notifications'

const { onPaneReady, onNodeDragStop, onConnect, fitView } = useVueFlow()
const globalViewModel = new GlobalViewModel()
const notifStore = useNotificationStore()

const nodes = shallowRef<Node[]>([])
const edges = shallowRef<Edge[]>([])

const selectedNodeData = ref<any | null>(null)
const isConfigOpen = ref(false)
const isRunDialogOpen = ref(false)
const workflowInputPrompt = ref('')

function getNodeTypeName(type: NodeType): string {
  switch (type) {
    case NodeType.AGENT:
      return 'agent'
    case NodeType.MODEL:
      return 'model'
    case NodeType.PROMPT:
      return 'prompt'
    case NodeType.MCP_CONNECTOR:
      return 'mcp'
    default:
      return 'default'
  }
}

watch(() => globalViewModel.nodes, (newNodes) => {
  nodes.value = newNodes.map(node => ({
    id: node.id,
    type: getNodeTypeName(node.type),
    label: node.name,
    position: { x: node.position?.x ?? 0, y: node.position?.y ?? 0 },
    data: JSON.parse(JSON.stringify(node))
  }))
}, { immediate: true, deep: true })

watch(() => globalViewModel.edges, (newEdges) => {
  edges.value = newEdges.map(edge => ({
    id: edge.id || `e_${edge.sourceNodeId}_${edge.targetNodeId}`,
    source: edge.sourceNodeId,
    target: edge.targetNodeId,
    sourceHandle: edge.sourceHandle || undefined,
    targetHandle: edge.targetHandle || undefined,
    animated: true,
    style: { stroke: '#E63946', strokeWidth: 2 }
  }))
}, { immediate: true, deep: true })

onNodeDragStop((event) => {
  const draggedNode = event.node
  const originalData = draggedNode.data || {}

  globalViewModel.saveNode({
    id: draggedNode.id,
    name: originalData.name || 'Node',
    type: originalData.type ?? NodeType.AGENT,
    position: { x: draggedNode.position.x, y: draggedNode.position.y },
    modelConfig: originalData.modelConfig,
    systemPrompt: originalData.systemPrompt || '',
    endpoint: originalData.endpoint || '',
    mcpConfig: originalData.mcpConfig,
    flowId: globalViewModel.currentFlowId || 'default-flow'
  })
})

onConnect((params: Connection) => {
  if (!params.source || !params.target) return

  const newEdge = {
    id: `e_${params.source}_${params.target}`,
    flowId: globalViewModel.currentFlowId || 'default-flow',
    sourceNodeId: params.source,
    sourceHandle: params.sourceHandle || '',
    targetNodeId: params.target,
    targetHandle: params.targetHandle || ''
  }

  const updatedEdges = [...globalViewModel.edges.filter(e => e.id !== newEdge.id), newEdge]
  globalViewModel.saveEdges(updatedEdges)
})

onPaneReady((vueFlowInstance) => {
  vueFlowInstance.fitView()
  globalViewModel.loadNodes(globalViewModel.currentFlowId || 'default-flow')
  globalViewModel.loadEdges(globalViewModel.currentFlowId || 'default-flow')
})

function openNodeConfig(nodeData: any) {
  selectedNodeData.value = nodeData
  isConfigOpen.value = true
}

function handleNodeSave(updatedData: any) {
  const nodeModel: AgentNodeModel = {
    id: updatedData.id,
    name: updatedData.name,
    type: updatedData.type,
    position: updatedData.position || { x: 0, y: 0 },
    modelConfig: updatedData.modelConfig,
    systemPrompt: updatedData.systemPrompt || '',
    endpoint: updatedData.endpoint || '',
    mcpConfig: updatedData.mcpConfig,
    flowId: globalViewModel.currentFlowId || 'default-flow'
  }

  globalViewModel.saveNode(nodeModel)
}

function handleNodeDelete(nodeId: string) {
  globalViewModel.removeNode(nodeId)
  notifStore.info(`Node ${nodeId} deleted`)
}

function handleAddNode(type: NodeType) {
  const offset = nodes.value.length * 30
  globalViewModel.addNode(type, { x: 200 + offset, y: 150 + offset })
}

function handleExecuteWorkflow() {
  globalViewModel.executeWorkflow(undefined, workflowInputPrompt.value)
  isRunDialogOpen.value = false
}

async function handleSaveAll() {
  const flowId = globalViewModel.currentFlowId || 'default-flow'
  for (const node of globalViewModel.nodes) {
    await globalViewModel.saveNode(node)
  }
  await globalViewModel.saveEdges(globalViewModel.edges, flowId)
  notifStore.success('Entire workflow saved to database')
}

function handleClearCanvas() {
  for (const node of [...globalViewModel.nodes]) {
    globalViewModel.removeNode(node.id)
  }
  notifStore.info('Canvas cleared')
}

const executionStatusColor = computed(() => {
  switch (globalViewModel.executionStatus) {
    case 'Running':
    case 'Starting':
      return 'info'
    case 'Completed':
      return 'success'
    case 'Failed':
      return 'error'
    default:
      return 'grey'
  }
})
</script>

<template>
  <div class="canvas-container">
    <div class="floating-toolbar d-flex align-center gap-2 pa-2">
      <v-menu offset-y>
        <template v-slot:activator="{ props: menuProps }">
          <v-btn
            color="primary"
            prepend-icon="mdi-plus"
            variant="flat"
            size="small"
            v-bind="menuProps"
          >
            Add Node
          </v-btn>
        </template>
        <v-list density="compact">
          <v-list-item prepend-icon="mdi-robot" @click="handleAddNode(NodeType.AGENT)">
            <v-list-item-title>AI Agent</v-list-item-title>
          </v-list-item>
          <v-list-item prepend-icon="mdi-brain" @click="handleAddNode(NodeType.MODEL)">
            <v-list-item-title>LLM Model</v-list-item-title>
          </v-list-item>
          <v-list-item prepend-icon="mdi-script-text-outline" @click="handleAddNode(NodeType.PROMPT)">
            <v-list-item-title>System Prompt</v-list-item-title>
          </v-list-item>
          <v-list-item prepend-icon="mdi-connection" @click="handleAddNode(NodeType.MCP_CONNECTOR)">
            <v-list-item-title>MCP Tools Connector</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>

      <v-btn
        prepend-icon="mdi-content-save-outline"
        variant="tonal"
        size="small"
        @click="handleSaveAll"
        :loading="globalViewModel.isGrpcLoading"
      >
        Save All
      </v-btn>

      <v-btn
        color="success"
        prepend-icon="mdi-play"
        variant="flat"
        size="small"
        :loading="globalViewModel.executionStatus === 'Running'"
        @click="isRunDialogOpen = true"
      >
        Run Flow...
      </v-btn>

      <v-chip size="small" :color="executionStatusColor" variant="tonal" class="ml-2 font-weight-medium">
        {{ globalViewModel.executionStatus }}
      </v-chip>

      <v-spacer />

      <v-btn
        icon="mdi-delete-sweep-outline"
        variant="text"
        size="small"
        color="grey"
        title="Clear Canvas"
        @click="handleClearCanvas"
      />

      <v-btn
        icon="mdi-fit-to-screen-outline"
        variant="tonal"
        size="x-small"
        title="Fit View"
        @click="fitView"
      />
    </div>

    <VueFlow
      :nodes="nodes"
      :edges="edges"
      :default-viewport="{ zoom: 1 }"
      :min-zoom="0.2"
      :max-zoom="4"
      fit-view-on-init
    >
      <Background pattern-color="#334155" :gap="16" />
      <Controls />
      <MiniMap />

      <template #node-agent="nodeProps">
        <AgentNode v-bind="nodeProps" @configure="openNodeConfig" @delete="handleNodeDelete" />
      </template>

      <template #node-model="nodeProps">
        <ModelNode v-bind="nodeProps" @configure="openNodeConfig" @delete="handleNodeDelete" />
      </template>

      <template #node-prompt="nodeProps">
        <PromptNode v-bind="nodeProps" @configure="openNodeConfig" @delete="handleNodeDelete" />
      </template>

      <template #node-mcp="nodeProps">
        <McpNode v-bind="nodeProps" @configure="openNodeConfig" @delete="handleNodeDelete" />
      </template>
    </VueFlow>

    <NodeConfigDialog
      v-model="isConfigOpen"
      :node-data="selectedNodeData"
      @save="handleNodeSave"
    />

    <v-dialog v-model="isRunDialogOpen" max-width="500">
      <v-card color="surface" rounded="lg">
        <v-card-title class="d-flex align-center">
          <v-icon icon="mdi-play-circle" color="success" class="mr-2" />
          <span>Execute Multi-Agent Workflow</span>
        </v-card-title>

        <v-card-text>
          <p class="text-caption text-grey mb-3">
            Provide an initial query or message for the autonomous agents to execute, or run with base instructions:
          </p>

          <v-textarea
            v-model="workflowInputPrompt"
            placeholder="e.g. Plan a microservice architecture and list required tools via MCP..."
            rows="3"
            variant="outlined"
            density="compact"
            auto-grow
          />
        </v-card-text>

        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn variant="text" size="small" @click="isRunDialogOpen = false">Cancel</v-btn>
          <v-btn
            color="success"
            variant="flat"
            size="small"
            prepend-icon="mdi-play"
            @click="handleExecuteWorkflow"
          >
            Execute
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.canvas-container {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

.floating-toolbar {
  position: absolute;
  top: 16px;
  left: 16px;
  right: 16px;
  z-index: 10;
  background-color: rgba(23, 33, 54, 0.85);
  backdrop-filter: blur(8px);
  border-radius: 8px;
  border: 1px solid rgba(255, 255, 255, 0.08);
}
</style>
