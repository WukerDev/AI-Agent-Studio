<script setup lang="ts">
defineOptions({
  name: 'WorkflowCanvas'
})

import { shallowRef, watch, computed, onMounted, onUnmounted, ref } from 'vue'
import { VueFlow, useVueFlow, type Node, type Edge, type Connection } from '@vue-flow/core'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'
import { MiniMap } from '@vue-flow/minimap'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import '@vue-flow/controls/dist/style.css'
import '@vue-flow/minimap/dist/style.css'
import { GlobalViewModel } from '../../GlobalViewModel'
import { NodeType } from '../../clients/generated/agent_node'
import AgentNode from './nodes/AgentNode.vue'
import ModelNode from './nodes/ModelNode.vue'
import PromptNode from './nodes/PromptNode.vue'
import McpNode from './nodes/McpNode.vue'
import NoteNode from './nodes/NoteNode.vue'
import LabelNode from './nodes/LabelNode.vue'
import { useNotificationStore } from '../../notifications'

const {
  onPaneReady,
  onNodeDragStart,
  onNodeDragStop,
  onConnect,
  onNodeClick,
  fitView,
  screenToFlowCoordinate,
  project,
  getSelectedNodes,
  getNodes
} = useVueFlow()

const globalViewModel = new GlobalViewModel()
const notifStore = useNotificationStore()

type CanvasNode = Node & { selected?: boolean }

const nodes = shallowRef<CanvasNode[]>([])
const edges = shallowRef<Edge[]>([])

const isRunDialogOpen = ref(false)
const isShortcutsHelpOpen = ref(false)
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
    case NodeType.NOTE:
      return 'note'
    case NodeType.LABEL:
      return 'label'
    default:
      return 'default'
  }
}

watch(() => globalViewModel.nodes, (newNodes) => {
  const currentSelected = new Set(nodes.value.filter(n => n.selected).map(n => n.id))
  nodes.value = newNodes.map(node => ({
    id: node.id,
    type: getNodeTypeName(node.type),
    label: node.name,
    selected: currentSelected.has(node.id),
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

watch(() => globalViewModel.currentFlowId, () => {
  setTimeout(() => {
    fitView({ padding: 0.2 })
  }, 100)
})

onNodeClick((event) => {
  if (event.node?.id) {
    globalViewModel.openInspector(event.node.id)
  }
})

onNodeDragStart(() => {
  globalViewModel.pushHistorySnapshot()
})

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
  globalViewModel.pushHistorySnapshot()

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
  if (nodeData?.id) {
    globalViewModel.openInspector(nodeData.id)
  }
}

function handleNodeDelete(nodeId: string) {
  globalViewModel.removeNode(nodeId)
  notifStore.info(`Node ${nodeId} deleted`)
}

function handleAddNode(type: NodeType) {
  const count = nodes.value.length
  globalViewModel.addNode(type, {
    x: 200 + (count % 4) * 220,
    y: 150 + Math.floor(count / 4) * 140
  })
}

function getMiniMapNodeColor(node: any): string {
  switch (node.type) {
    case 'agent':
      return '#E63946'
    case 'model':
      return '#2196F3'
    case 'prompt':
      return '#FB8C00'
    case 'mcp':
      return '#4CAF50'
    case 'note':
      return '#F59E0B'
    case 'label':
      return '#8B5CF6'
    default:
      return '#64748B'
  }
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
  globalViewModel.pushHistorySnapshot()
  for (const node of [...globalViewModel.nodes]) {
    globalViewModel.removeNode(node.id)
  }
  notifStore.info('Canvas cleared')
}

// ----------------- History (Undo / Redo) -----------------
async function handleUndo() {
  if (!globalViewModel.canUndo) return
  await globalViewModel.undo()
  notifStore.info('Undo action')
}

async function handleRedo() {
  if (!globalViewModel.canRedo) return
  await globalViewModel.redo()
  notifStore.info('Redo action')
}

// ----------------- Multi-Selection & Shortcuts -----------------
function getCurrentlySelectedNodeIds(): string[] {
  const fromVueFlow = (getSelectedNodes?.value || []).map((n: any) => n.id)
  if (fromVueFlow.length > 0) return fromVueFlow
  return nodes.value.filter(n => n.selected).map(n => n.id)
}

function selectAllNodes() {
  const allNodes = getNodes?.value || nodes.value
  for (const n of allNodes as any[]) {
    n.selected = true
  }
  nodes.value = nodes.value.map(n => ({ ...n, selected: true }))
  notifStore.info(`Selected all ${nodes.value.length} nodes`)
}

function handleCopy() {
  const selectedIds = getCurrentlySelectedNodeIds()
  if (selectedIds.length === 0) return
  const count = globalViewModel.copySelected(selectedIds)
  notifStore.info(`Copied ${count} node(s) to clipboard`)
}

async function handleCut() {
  const selectedIds = getCurrentlySelectedNodeIds()
  if (selectedIds.length === 0) return
  const count = await globalViewModel.cutSelected(selectedIds)
  notifStore.info(`Cut ${count} node(s)`)
}

async function handlePaste() {
  const pastedNodes = await globalViewModel.paste()
  if (pastedNodes.length > 0) {
    const pastedIdSet = new Set(pastedNodes.map(n => n.id))
    nodes.value = nodes.value.map(n => ({
      ...n,
      selected: pastedIdSet.has(n.id)
    }))
    notifStore.info(`Pasted ${pastedNodes.length} node(s)`)
  }
}

async function handleDeleteSelected() {
  const selectedIds = getCurrentlySelectedNodeIds()
  if (selectedIds.length === 0) return
  const count = await globalViewModel.deleteSelected(selectedIds)
  notifStore.info(`Deleted ${count} node(s)`)
}

async function handleArrowMove(key: string, isShift: boolean) {
  const selectedIds = getCurrentlySelectedNodeIds()
  if (selectedIds.length === 0) return

  const delta = isShift ? 40 : 10
  let dx = 0
  let dy = 0
  if (key === 'ArrowUp') dy = -delta
  if (key === 'ArrowDown') dy = delta
  if (key === 'ArrowLeft') dx = -delta
  if (key === 'ArrowRight') dx = delta

  const idSet = new Set(selectedIds)
  for (const node of nodes.value) {
    if (idSet.has(node.id)) {
      node.position = {
        x: node.position.x + dx,
        y: node.position.y + dy
      }
    }
  }

  await globalViewModel.moveSelected(selectedIds, dx, dy)
}

function isEditingInput(target: EventTarget | null): boolean {
  if (!target || !(target instanceof HTMLElement)) return false
  const tagName = target.tagName.toUpperCase()
  return (
    tagName === 'INPUT' ||
    tagName === 'TEXTAREA' ||
    tagName === 'SELECT' ||
    target.isContentEditable ||
    Boolean(target.closest('input, textarea, select, [contenteditable="true"]'))
  )
}

function handleKeyDown(event: KeyboardEvent) {
  if (isRunDialogOpen.value || isShortcutsHelpOpen.value) {
    return
  }
  if (isEditingInput(event.target)) {
    return
  }

  const isCtrlOrMeta = event.ctrlKey || event.metaKey

  // Select all: Ctrl+A
  if (isCtrlOrMeta && event.key.toLowerCase() === 'a') {
    event.preventDefault()
    selectAllNodes()
    return
  }

  // Copy: Ctrl+C
  if (isCtrlOrMeta && event.key.toLowerCase() === 'c') {
    event.preventDefault()
    handleCopy()
    return
  }

  // Cut: Ctrl+X
  if (isCtrlOrMeta && event.key.toLowerCase() === 'x') {
    event.preventDefault()
    handleCut()
    return
  }

  // Paste: Ctrl+V
  if (isCtrlOrMeta && event.key.toLowerCase() === 'v') {
    event.preventDefault()
    handlePaste()
    return
  }

  // Undo: Ctrl+Z (without shift)
  if (isCtrlOrMeta && event.key.toLowerCase() === 'z' && !event.shiftKey) {
    event.preventDefault()
    handleUndo()
    return
  }

  // Redo: Ctrl+Y or Ctrl+Shift+Z
  if (
    (isCtrlOrMeta && event.key.toLowerCase() === 'y') ||
    (isCtrlOrMeta && event.key.toLowerCase() === 'z' && event.shiftKey)
  ) {
    event.preventDefault()
    handleRedo()
    return
  }

  // Delete / Backspace
  if (event.key === 'Delete' || event.key === 'Backspace') {
    const selectedIds = getCurrentlySelectedNodeIds()
    if (selectedIds.length > 0) {
      event.preventDefault()
      handleDeleteSelected()
      return
    }
  }

  // Arrow keys: ArrowUp, ArrowDown, ArrowLeft, ArrowRight
  if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(event.key)) {
    const selectedIds = getCurrentlySelectedNodeIds()
    if (selectedIds.length > 0) {
      event.preventDefault()
      handleArrowMove(event.key, event.shiftKey)
      return
    }
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleKeyDown)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeyDown)
})

function getDropCoordinates(event: DragEvent) {
  const clientPos = { x: event.clientX, y: event.clientY }
  if (typeof screenToFlowCoordinate === 'function') {
    return screenToFlowCoordinate(clientPos)
  }
  if (typeof project === 'function') {
    const canvasBounds = (event.currentTarget as HTMLElement)?.getBoundingClientRect?.()
    if (canvasBounds) {
      return project({
        x: event.clientX - canvasBounds.left,
        y: event.clientY - canvasBounds.top
      })
    }
    return project(clientPos)
  }
  return clientPos
}

function onDragOver(event: DragEvent) {
  event.preventDefault()
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'copy'
  }
}

function onDrop(event: DragEvent) {
  event.preventDefault()
  const rawData = event.dataTransfer?.getData('application/json')
  if (!rawData) return

  try {
    const payload = JSON.parse(rawData)
    const pos = getDropCoordinates(event)

    if (payload.itemType === 'file') {
      const fileName = payload.item?.name || 'File Context'
      globalViewModel.addNode(
        NodeType.PROMPT,
        pos,
        fileName,
        {
          systemPrompt: `Context from ${fileName}:\n[Attach document / prompt data here]`,
          endpoint: payload.item?.path || ''
        }
      )
      notifStore.info(`Added file "${fileName}" as context node`)
      return
    }

    if (payload.type !== undefined) {
      const type = payload.type as NodeType
      const name = payload.name
      const customConfig = payload.customConfig || {}
      globalViewModel.addNode(type, pos, name, customConfig)
      notifStore.info(`Added node "${name || 'New Node'}" to canvas`)
    }
  } catch (err) {
    console.error('Failed to process drop on canvas', err)
  }
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
  <div class="canvas-container" @dragover.prevent="onDragOver" @drop="onDrop">
    <div class="floating-toolbar d-flex align-center gap-2 pa-2">
      <!-- Add Node Menu -->
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
          <v-divider class="my-1" />
          <v-list-item prepend-icon="mdi-note-text-outline" @click="handleAddNode(NodeType.NOTE)">
            <v-list-item-title>Sticky Note</v-list-item-title>
          </v-list-item>
          <v-list-item prepend-icon="mdi-format-title" @click="handleAddNode(NodeType.LABEL)">
            <v-list-item-title>Section Label</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>

      <!-- Quick Add Note & Label Buttons -->
      <v-btn
        prepend-icon="mdi-note-plus-outline"
        variant="tonal"
        size="small"
        color="amber-darken-1"
        title="Add Sticky Note to Canvas"
        @click="handleAddNode(NodeType.NOTE)"
      >
        Note
      </v-btn>

      <v-btn
        prepend-icon="mdi-format-title"
        variant="tonal"
        size="small"
        color="purple-lighten-2"
        title="Add Section Label to Canvas"
        @click="handleAddNode(NodeType.LABEL)"
      >
        Label
      </v-btn>

      <v-divider vertical class="mx-1" />

      <!-- Undo / Redo Buttons -->
      <v-btn
        icon="mdi-undo"
        variant="text"
        size="small"
        :disabled="!globalViewModel.canUndo"
        title="Undo (Ctrl+Z)"
        @click="handleUndo"
      />

      <v-btn
        icon="mdi-redo"
        variant="text"
        size="small"
        :disabled="!globalViewModel.canRedo"
        title="Redo (Ctrl+Y / Ctrl+Shift+Z)"
        @click="handleRedo"
      />

      <v-divider vertical class="mx-1" />

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

      <!-- Inspector Toggle Button -->
      <v-btn
        icon="mdi-tune-variant"
        variant="text"
        size="small"
        :color="globalViewModel.isInspectorOpen ? 'primary' : 'grey-lighten-1'"
        title="Toggle Right Inspector Panel"
        @click="globalViewModel.isInspectorOpen = !globalViewModel.isInspectorOpen"
      />

      <!-- Keyboard Shortcuts Help -->
      <v-btn
        icon="mdi-keyboard-outline"
        variant="text"
        size="small"
        color="grey-lighten-1"
        title="Keyboard Shortcuts"
        @click="isShortcutsHelpOpen = true"
      />

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
      :multi-selection-key-code="['Control', 'Meta', 'Shift']"
      :selection-key-code="['Shift']"
      fit-view-on-init
    >
      <Background pattern-color="#334155" :gap="16" />
      <Controls />
      <MiniMap :node-color="getMiniMapNodeColor" mask-color="rgba(15, 23, 42, 0.75)" />

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

      <template #node-note="nodeProps">
        <NoteNode v-bind="nodeProps" @configure="openNodeConfig" @delete="handleNodeDelete" />
      </template>

      <template #node-label="nodeProps">
        <LabelNode v-bind="nodeProps" @configure="openNodeConfig" @delete="handleNodeDelete" />
      </template>
    </VueFlow>

    <!-- Keyboard Shortcuts Dialog -->
    <v-dialog v-model="isShortcutsHelpOpen" max-width="480">
      <v-card color="surface" rounded="lg">
        <v-card-title class="d-flex align-center justify-space-between pa-4">
          <div class="d-flex align-center">
            <v-icon icon="mdi-keyboard-outline" color="primary" class="mr-2" />
            <span class="text-subtitle-1 font-weight-bold">Canvas Keyboard Shortcuts</span>
          </div>
          <v-btn icon="mdi-close" variant="text" size="small" @click="isShortcutsHelpOpen = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-list density="compact" bg-color="transparent">
            <v-list-item>
              <template #prepend>
                <v-chip size="x-small" label class="mr-3 font-monospace font-weight-bold">Ctrl + A</v-chip>
              </template>
              <v-list-item-title class="text-caption">Select all nodes on canvas</v-list-item-title>
            </v-list-item>

            <v-list-item>
              <template #prepend>
                <v-chip size="x-small" label class="mr-3 font-monospace font-weight-bold">Ctrl + C</v-chip>
              </template>
              <v-list-item-title class="text-caption">Copy selected nodes & connections</v-list-item-title>
            </v-list-item>

            <v-list-item>
              <template #prepend>
                <v-chip size="x-small" label class="mr-3 font-monospace font-weight-bold">Ctrl + X</v-chip>
              </template>
              <v-list-item-title class="text-caption">Cut selected nodes</v-list-item-title>
            </v-list-item>

            <v-list-item>
              <template #prepend>
                <v-chip size="x-small" label class="mr-3 font-monospace font-weight-bold">Ctrl + V</v-chip>
              </template>
              <v-list-item-title class="text-caption">Paste nodes with offset</v-list-item-title>
            </v-list-item>

            <v-list-item>
              <template #prepend>
                <v-chip size="x-small" label class="mr-3 font-monospace font-weight-bold">Del / Backspace</v-chip>
              </template>
              <v-list-item-title class="text-caption">Delete selected node(s)</v-list-item-title>
            </v-list-item>

            <v-list-item>
              <template #prepend>
                <v-chip size="x-small" label class="mr-3 font-monospace font-weight-bold">Ctrl + Z</v-chip>
              </template>
              <v-list-item-title class="text-caption">Undo canvas action</v-list-item-title>
            </v-list-item>

            <v-list-item>
              <template #prepend>
                <v-chip size="x-small" label class="mr-3 font-monospace font-weight-bold">Ctrl + Y / Ctrl+Shift+Z</v-chip>
              </template>
              <v-list-item-title class="text-caption">Redo canvas action</v-list-item-title>
            </v-list-item>

            <v-list-item>
              <template #prepend>
                <v-chip size="x-small" label class="mr-3 font-monospace font-weight-bold">Arrow Keys</v-chip>
              </template>
              <v-list-item-title class="text-caption">Move selected node(s) by 10px</v-list-item-title>
            </v-list-item>

            <v-list-item>
              <template #prepend>
                <v-chip size="x-small" label class="mr-3 font-monospace font-weight-bold">Shift + Arrows</v-chip>
              </template>
              <v-list-item-title class="text-caption">Move selected node(s) by 40px (fast)</v-list-item-title>
            </v-list-item>
          </v-list>
        </v-card-text>
      </v-card>
    </v-dialog>

    <!-- Run Workflow Dialog -->
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
  background-color: rgba(15, 23, 42, 0.88);
  backdrop-filter: blur(12px);
  border-radius: 10px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
}

:deep(.vue-flow__controls) {
  background-color: #172136 !important;
  border: 1px solid #334155 !important;
  border-radius: 8px !important;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5) !important;
  overflow: hidden !important;
}

:deep(.vue-flow__controls-button) {
  background-color: #172136 !important;
  border-bottom: 1px solid #334155 !important;
  fill: #94A3B8 !important;
  color: #94A3B8 !important;
  width: 28px !important;
  height: 28px !important;
  display: flex !important;
  align-items: center !important;
  justify-content: center !important;
}

:deep(.vue-flow__controls-button:last-child) {
  border-bottom: none !important;
}

:deep(.vue-flow__controls-button:hover) {
  background-color: #1E293B !important;
  fill: #FFFFFF !important;
  color: #FFFFFF !important;
}

:deep(.vue-flow__controls-button svg) {
  fill: #94A3B8 !important;
  width: 14px !important;
  height: 14px !important;
}

:deep(.vue-flow__controls-button:hover svg) {
  fill: #FFFFFF !important;
}

:deep(.vue-flow__minimap) {
  background-color: #0B1120 !important;
  border: 1px solid #334155 !important;
  border-radius: 8px !important;
  box-shadow: 0 4px 14px rgba(0, 0, 0, 0.6) !important;
  overflow: hidden !important;
}

:deep(.vue-flow__minimap-mask) {
  fill: rgba(15, 23, 42, 0.75) !important;
  stroke: #E63946 !important;
  stroke-width: 2px !important;
}

:deep(.vue-flow__minimap-node) {
  fill: #334155 !important;
  stroke: none !important;
}
</style>
