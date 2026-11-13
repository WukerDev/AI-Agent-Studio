<script setup lang="ts">
defineOptions({
  name: 'WorkflowCanvas'
})

import { shallowRef, watch, computed, onMounted, onUnmounted, ref } from 'vue'
import { VueFlow, useVueFlow, ConnectionMode, type Node, type Edge, type Connection } from '@vue-flow/core'
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
import SkillNode from './nodes/SkillNode.vue'
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

const flowSamplePrompts: Record<string, { label: string; prompt: string }[]> = {
  'default-flow': [
    {
      label: 'Obliczenia matematyczne (MCP Calculator)',
      prompt: 'Użyj narzędzia calculate_expression aby obliczyć: (145 * 28) + 350 i przedstaw wynik po polsku.'
    },
    {
      label: 'Diagnostyka systemu (MCP System Info)',
      prompt: 'Uruchom narzędzie get_system_info i podaj system operacyjny, wersję .NET oraz liczbę rdzeni procesora.'
    },
    {
      label: 'Aktualny czas UTC (MCP Time)',
      prompt: 'Sprawdź aktualny czas UTC oraz dzień tygodnia za pomocą narzędzia get_current_time.'
    }
  ],
  'multi-agent-research': [
    {
      label: 'gRPC vs REST API (Badacz -> Redaktor)',
      prompt: 'Zbadaj kluczowe różnice między komunikacją gRPC a REST API w mikroserwisach wysokiej wydajności.'
    },
    {
      label: 'Indeksy PostgreSQL (Badacz -> Redaktor)',
      prompt: 'Przeanalizuj zastosowania indeksów B-Tree oraz JSONB GIN w bazie PostgreSQL dla systemów AI.'
    },
    {
      label: 'Architektura ReAct i MCP',
      prompt: 'Opisz wzorzec orkiestracji wieloagentowej ReAct oraz rolę protokołu Model Context Protocol.'
    }
  ],
  'gemini-assistant-flow': [
    {
      label: 'Obliczenia + Czas (Gemini 3.5 Flash-Lite + MCP)',
      prompt: 'Oblicz za pomocą calculate_expression wyrażenie (512 * 32) + 1024 oraz podaj zwięzłe podsumowanie.'
    },
    {
      label: 'Projekt bazy danych dla Agentów AI',
      prompt: 'Zaprojektuj zwięzły schemat tabel PostgreSQL dla historii wykonań agentów AI z kolumną JSONB.'
    },
    {
      label: 'Diagnostyka środowiska uruchomieniowego',
      prompt: 'Pobierz informacje o systemie przez get_system_info i podsumuj parametry maszyny.'
    }
  ],
  'code-review-flow': [
    {
      label: 'Lista plików projektu (MCP list_dir)',
      prompt: 'Wypisz zawartość bieżącego katalogu roboczego za pomocą narzędzia list_dir i opisz strukturę projektu.'
    },
    {
      label: 'Diagnostyka hosta .NET (MCP get_system_info)',
      prompt: 'Wywołaj narzędzie get_system_info i podaj wersję środowiska .NET, architekturę OS oraz liczbę rdzeni CPU.'
    },
    {
      label: 'Audyt bezpieczeństwa gRPC-Web i SignalR',
      prompt: 'Jako Senior Architect wymień 3 najważniejsze zasady zabezpieczania usług gRPC-Web oraz hubów SignalR.'
    }
  ],
  'mcp-calculator-time-flow': [
    {
      label: 'Test get_system_info (Diagnostyka OS / CPU / .NET)',
      prompt: 'Wywołaj narzędzie get_system_info i podaj nazwę systemu operacyjnego, wersję .NET oraz liczbę rdzeni CPU.'
    },
    {
      label: 'Test calculate_expression (Kalkulator MCP)',
      prompt: 'Użyj narzędzia calculate_expression aby policzyć (256 * 64) + 1024 i podaj dokładny wynik.'
    },
    {
      label: 'Test get_current_time (Aktualna data i czas UTC)',
      prompt: 'Wywołaj narzędzie get_current_time i podaj dokładną datę oraz godzinę UTC.'
    }
  ]
}

const currentFlowSamples = computed(() => {
  const flowId = globalViewModel.currentFlowId || 'default-flow'
  return flowSamplePrompts[flowId] ?? flowSamplePrompts['default-flow'] ?? []
})

function resolveEdgeHandlesAndColor(edge: {
  sourceNodeId: string
  sourceHandle?: string
  targetNodeId: string
  targetHandle?: string
}) {
  const allNodes = globalViewModel.nodes || []
  const sourceNode = allNodes.find(n => n.id === edge.sourceNodeId)
  const targetNode = allNodes.find(n => n.id === edge.targetNodeId)

  let sourceId = edge.sourceNodeId
  let targetId = edge.targetNodeId
  let sourceHandle = edge.sourceHandle || undefined
  let targetHandle = edge.targetHandle || undefined
  let strokeColor = '#E63946'

  // If an Agent is connected to a sub-node (Model, Prompt, MCP, Skill), normalize direction: Sub-Node -> Agent port
  if (sourceNode?.type === NodeType.AGENT && targetNode && targetNode.type !== NodeType.AGENT) {
    sourceId = targetNode.id
    targetId = sourceNode.id
    sourceHandle = undefined
    targetHandle = undefined
  }

  const normalizedSource = allNodes.find(n => n.id === sourceId)
  const normalizedTarget = allNodes.find(n => n.id === targetId)

  if (normalizedTarget?.type === NodeType.AGENT) {
    if (normalizedSource?.type === NodeType.MODEL) {
      sourceHandle = 'model-out'
      targetHandle = 'model'
      strokeColor = '#3B82F6'
    } else if (normalizedSource?.type === NodeType.PROMPT) {
      sourceHandle = 'prompt-out'
      targetHandle = 'prompt'
      strokeColor = '#F59E0B'
    } else if (normalizedSource?.type === NodeType.MCP_CONNECTOR) {
      sourceHandle = 'tool-out'
      targetHandle = 'tool'
      strokeColor = '#10B981'
    } else if (normalizedSource?.type === NodeType.SKILL) {
      sourceHandle = 'skill-out'
      targetHandle = 'skill'
      strokeColor = '#06B6D4'
    } else if (normalizedSource?.type === NodeType.AGENT) {
      sourceHandle = 'agent-out'
      targetHandle = 'agent-in'
      strokeColor = '#E63946'
    }
  }

  return { sourceId, targetId, sourceHandle, targetHandle, strokeColor }
}

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
    case NodeType.SKILL:
      return 'skill'
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

watch([() => globalViewModel.edges, () => globalViewModel.nodes], ([newEdges]) => {
  edges.value = newEdges.map(edge => {
    const resolved = resolveEdgeHandlesAndColor(edge)
    return {
      id: edge.id || `e_${resolved.sourceId}_${resolved.targetId}`,
      source: resolved.sourceId,
      target: resolved.targetId,
      sourceHandle: resolved.sourceHandle,
      targetHandle: resolved.targetHandle,
      animated: true,
      style: { stroke: resolved.strokeColor, strokeWidth: 2 }
    }
  })
}, { immediate: true, deep: true })

watch(() => globalViewModel.currentFlowId, (newFlowId) => {
  const samples = flowSamplePrompts[newFlowId || 'default-flow'] || flowSamplePrompts['default-flow']
  if (samples && samples.length > 0) {
    workflowInputPrompt.value = samples[0]!.prompt
  }
  setTimeout(() => {
    fitView({ padding: 0.2 })
  }, 100)
}, { immediate: true })

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

  const resolved = resolveEdgeHandlesAndColor({
    sourceNodeId: params.source,
    sourceHandle: params.sourceHandle || '',
    targetNodeId: params.target,
    targetHandle: params.targetHandle || ''
  })

  const newEdge = {
    id: `e_${resolved.sourceId}_${resolved.targetId}`,
    flowId: globalViewModel.currentFlowId || 'default-flow',
    sourceNodeId: resolved.sourceId,
    sourceHandle: resolved.sourceHandle || '',
    targetNodeId: resolved.targetId,
    targetHandle: resolved.targetHandle || ''
  }

  let filteredEdges = globalViewModel.edges.filter(e => e.id !== newEdge.id)
  if (resolved.targetHandle === 'model') {
    filteredEdges = filteredEdges.filter(
      e => !(e.targetNodeId === resolved.targetId && e.targetHandle === 'model')
    )
  }

  const updatedEdges = [...filteredEdges, newEdge]
  globalViewModel.saveEdges(updatedEdges)
})

onPaneReady((vueFlowInstance) => {
  vueFlowInstance.fitView()
  globalViewModel.loadNodes(globalViewModel.currentFlowId || 'default-flow')
  globalViewModel.loadEdges(globalViewModel.currentFlowId || 'default-flow')
  globalViewModel.loadSkills()
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
      return '#3B82F6'
    case 'prompt':
      return '#F59E0B'
    case 'mcp':
      return '#10B981'
    case 'note':
      return '#F59E0B'
    case 'label':
      return '#8B5CF6'
    case 'skill':
      return '#06B6D4'
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

  if (isCtrlOrMeta && event.key.toLowerCase() === 'a') {
    event.preventDefault()
    selectAllNodes()
    return
  }

  if (isCtrlOrMeta && event.key.toLowerCase() === 'c') {
    event.preventDefault()
    handleCopy()
    return
  }

  if (isCtrlOrMeta && event.key.toLowerCase() === 'x') {
    event.preventDefault()
    handleCut()
    return
  }

  if (isCtrlOrMeta && event.key.toLowerCase() === 'v') {
    event.preventDefault()
    handlePaste()
    return
  }

  if (isCtrlOrMeta && event.key.toLowerCase() === 'z' && !event.shiftKey) {
    event.preventDefault()
    handleUndo()
    return
  }

  if (
    (isCtrlOrMeta && event.key.toLowerCase() === 'y') ||
    (isCtrlOrMeta && event.key.toLowerCase() === 'z' && event.shiftKey)
  ) {
    event.preventDefault()
    handleRedo()
    return
  }

  if (event.key === 'Delete' || event.key === 'Backspace') {
    const selectedIds = getCurrentlySelectedNodeIds()
    if (selectedIds.length > 0) {
      event.preventDefault()
      handleDeleteSelected()
      return
    }
  }

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
</script>

<template>
  <div class="canvas-container" @dragover.prevent="onDragOver" @drop="onDrop">
    <!-- Solid Dark Floating Toolbar -->
    <div class="floating-toolbar d-flex align-center ga-1.5 px-2.5 py-1.5">
      <!-- Add Node Menu -->
      <v-menu offset-y>
        <template v-slot:activator="{ props: menuProps }">
          <button type="button" v-bind="menuProps" class="tb-btn tb-btn-primary">
            <v-icon icon="mdi-plus" size="14" />
            <span>Add Node</span>
            <v-icon icon="mdi-chevron-down" size="13" />
          </button>
        </template>
        <v-list density="compact" min-width="210" class="solid-dropdown-menu pa-1 rounded-lg">
          <v-list-item class="rounded" @click="handleAddNode(NodeType.AGENT)">
            <template #prepend>
              <v-icon icon="mdi-robot-outline" color="#E63946" size="16" class="mr-2" />
            </template>
            <v-list-item-title class="text-caption font-weight-medium">AI Agent Hub</v-list-item-title>
          </v-list-item>
          <v-list-item class="rounded" @click="handleAddNode(NodeType.MODEL)">
            <template #prepend>
              <v-icon icon="mdi-brain" color="#3B82F6" size="16" class="mr-2" />
            </template>
            <v-list-item-title class="text-caption font-weight-medium">LLM Model</v-list-item-title>
          </v-list-item>
          <v-list-item class="rounded" @click="handleAddNode(NodeType.PROMPT)">
            <template #prepend>
              <v-icon icon="mdi-script-text-outline" color="#F59E0B" size="16" class="mr-2" />
            </template>
            <v-list-item-title class="text-caption font-weight-medium">System Prompt</v-list-item-title>
          </v-list-item>
          <v-list-item class="rounded" @click="handleAddNode(NodeType.MCP_CONNECTOR)">
            <template #prepend>
              <v-icon icon="mdi-connection" color="#10B981" size="16" class="mr-2" />
            </template>
            <v-list-item-title class="text-caption font-weight-medium">MCP Tools Connector</v-list-item-title>
          </v-list-item>
          <v-list-item class="rounded" @click="handleAddNode(NodeType.SKILL)">
            <template #prepend>
              <v-icon icon="mdi-book-open-page-variant-outline" color="#06B6D4" size="16" class="mr-2" />
            </template>
            <v-list-item-title class="text-caption font-weight-medium">Agent Skill (SKILL.md)</v-list-item-title>
          </v-list-item>
          <v-divider class="my-1" />
          <v-list-item class="rounded" @click="handleAddNode(NodeType.NOTE)">
            <template #prepend>
              <v-icon icon="mdi-note-text-outline" color="#FBBF24" size="16" class="mr-2" />
            </template>
            <v-list-item-title class="text-caption font-weight-medium">Sticky Note</v-list-item-title>
          </v-list-item>
          <v-list-item class="rounded" @click="handleAddNode(NodeType.LABEL)">
            <template #prepend>
              <v-icon icon="mdi-format-title" color="#A855F7" size="16" class="mr-2" />
            </template>
            <v-list-item-title class="text-caption font-weight-medium">Section Label</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>

      <!-- Quick Add Note & Label (Subtle Dark Buttons with Colored Icons) -->
      <button
        type="button"
        class="tb-btn"
        title="Add Sticky Note to Canvas"
        @click="handleAddNode(NodeType.NOTE)"
      >
        <v-icon icon="mdi-note-plus-outline" size="14" color="#FBBF24" />
        <span>Note</span>
      </button>

      <button
        type="button"
        class="tb-btn"
        title="Add Section Label to Canvas"
        @click="handleAddNode(NodeType.LABEL)"
      >
        <v-icon icon="mdi-format-title" size="14" color="#A855F7" />
        <span>Label</span>
      </button>

      <div class="tb-divider" />

      <!-- Undo / Redo -->
      <button
        type="button"
        class="tb-icon-btn"
        :disabled="!globalViewModel.canUndo"
        title="Undo (Ctrl+Z)"
        @click="handleUndo"
      >
        <v-icon icon="mdi-undo" size="15" />
      </button>

      <button
        type="button"
        class="tb-icon-btn"
        :disabled="!globalViewModel.canRedo"
        title="Redo (Ctrl+Y / Ctrl+Shift+Z)"
        @click="handleRedo"
      >
        <v-icon icon="mdi-redo" size="15" />
      </button>

      <div class="tb-divider" />

      <!-- Save All -->
      <button
        type="button"
        class="tb-btn"
        :disabled="globalViewModel.isGrpcLoading"
        @click="handleSaveAll"
      >
        <v-icon icon="mdi-content-save-outline" size="14" color="#94A3B8" />
        <span>Save All</span>
      </button>

      <!-- Run Flow -->
      <button
        type="button"
        class="tb-btn tb-btn-run"
        @click="isRunDialogOpen = true"
      >
        <v-icon icon="mdi-play" size="14" />
        <span>Run Flow</span>
      </button>

      <!-- Execution Status Badge -->
      <span
        class="exec-status-badge font-monospace"
        :class="{
          'exec-running': globalViewModel.executionStatus === 'Running' || globalViewModel.executionStatus === 'Starting',
          'exec-completed': globalViewModel.executionStatus === 'Completed',
          'exec-failed': globalViewModel.executionStatus === 'Failed'
        }"
      >
        {{ globalViewModel.executionStatus }}
      </span>

      <v-spacer />

      <!-- Right Canvas Utilities -->
      <button
        type="button"
        class="tb-icon-btn"
        :class="{ 'tb-icon-btn-active': globalViewModel.isInspectorOpen }"
        title="Toggle Right Inspector Panel"
        @click="globalViewModel.isInspectorOpen = !globalViewModel.isInspectorOpen"
      >
        <v-icon icon="mdi-tune-variant" size="15" />
      </button>

      <button
        type="button"
        class="tb-icon-btn"
        title="Keyboard Shortcuts"
        @click="isShortcutsHelpOpen = true"
      >
        <v-icon icon="mdi-keyboard-outline" size="15" />
      </button>

      <button
        type="button"
        class="tb-icon-btn"
        title="Fit View"
        @click="() => fitView()"
      >
        <v-icon icon="mdi-fit-to-screen-outline" size="15" />
      </button>

      <button
        type="button"
        class="tb-icon-btn tb-icon-btn-danger"
        title="Clear Canvas"
        @click="handleClearCanvas"
      >
        <v-icon icon="mdi-delete-sweep-outline" size="15" />
      </button>
    </div>

    <VueFlow
      :nodes="nodes"
      :edges="edges"
      :connection-mode="ConnectionMode.Loose"
      :default-viewport="{ zoom: 1 }"
      :min-zoom="0.2"
      :max-zoom="4"
      :multi-selection-key-code="['Control', 'Meta', 'Shift']"
      :selection-key-code="['Shift']"
      fit-view-on-init
    >
      <Background pattern-color="#1E293B" :gap="18" />
      <Controls />
      <MiniMap :node-color="getMiniMapNodeColor" mask-color="rgba(9, 13, 22, 0.78)" />

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

      <template #node-skill="nodeProps">
        <SkillNode v-bind="nodeProps" @configure="openNodeConfig" @delete="handleNodeDelete" />
      </template>
    </VueFlow>

    <!-- Keyboard Shortcuts Dialog -->
    <v-dialog v-model="isShortcutsHelpOpen" max-width="460">
      <v-card class="solid-dialog-card" rounded="lg">
        <div class="dialog-header d-flex align-center justify-space-between px-4 py-3">
          <div class="d-flex align-center ga-2">
            <v-icon icon="mdi-keyboard-outline" color="#60A5FA" size="18" />
            <span class="text-subtitle-2 font-weight-bold text-white">Canvas Keyboard Shortcuts</span>
          </div>
          <v-btn icon="mdi-close" variant="text" size="x-small" color="grey" @click="isShortcutsHelpOpen = false" />
        </div>
        <div class="pa-3">
          <div class="d-flex flex-column ga-1">
            <div
              v-for="sc in [
                { keys: 'Ctrl + A', desc: 'Select all nodes on canvas' },
                { keys: 'Ctrl + C', desc: 'Copy selected nodes & connections' },
                { keys: 'Ctrl + X', desc: 'Cut selected nodes' },
                { keys: 'Ctrl + V', desc: 'Paste nodes with offset' },
                { keys: 'Del / Backspace', desc: 'Delete selected node(s)' },
                { keys: 'Ctrl + Z', desc: 'Undo canvas action' },
                { keys: 'Ctrl + Y', desc: 'Redo canvas action' },
                { keys: 'Arrow Keys', desc: 'Move selected node(s) by 10px' },
                { keys: 'Shift + Arrows', desc: 'Move selected node(s) by 40px' }
              ]"
              :key="sc.keys"
              class="shortcut-row d-flex align-center justify-space-between px-3 py-1.5 rounded"
            >
              <span class="text-caption" style="color: #CBD5E1;">{{ sc.desc }}</span>
              <kbd class="kbd-tag font-monospace">{{ sc.keys }}</kbd>
            </div>
          </div>
        </div>
      </v-card>
    </v-dialog>

    <!-- Run Workflow Dialog (Upgraded: No Red Alert-Style Cards) -->
    <v-dialog v-model="isRunDialogOpen" max-width="560">
      <v-card class="solid-dialog-card" rounded="lg">
        <!-- Header -->
        <div class="dialog-header d-flex align-center justify-space-between px-4 py-3">
          <div class="d-flex align-center ga-2">
            <div class="run-dialog-icon-box">
              <v-icon icon="mdi-play" color="#4ADE80" size="16" />
            </div>
            <span class="text-subtitle-2 font-weight-bold text-white">Execute Multi-Agent Workflow</span>
          </div>
          <div class="d-flex align-center ga-2">
            <span class="flow-tag font-monospace">{{ globalViewModel.currentFlowId || 'default-flow' }}</span>
            <v-btn icon="mdi-close" variant="text" size="x-small" color="grey-lighten-1" @click="isRunDialogOpen = false" />
          </div>
        </div>

        <!-- Body -->
        <div class="pa-4">
          <div class="d-flex align-center justify-space-between mb-2">
            <span class="section-label">Gotowe scenariusze testowe (kliknij, aby wstawić):</span>
          </div>

          <div class="sample-prompts-stack mb-4">
            <button
              v-for="(sample, idx) in currentFlowSamples"
              :key="idx"
              type="button"
              class="sample-prompt-item"
              :class="{ 'sample-prompt-item-active': workflowInputPrompt === sample.prompt }"
              @click="workflowInputPrompt = sample.prompt"
            >
              <div class="d-flex align-center justify-space-between ga-2 mb-1">
                <div class="d-flex align-center ga-2 overflow-hidden">
                  <v-icon
                    :icon="workflowInputPrompt === sample.prompt ? 'mdi-check-circle' : 'mdi-terminal'"
                    size="14"
                    :color="workflowInputPrompt === sample.prompt ? '#60A5FA' : '#64748B'"
                  />
                  <span class="sample-prompt-title text-truncate">{{ sample.label }}</span>
                </div>
                <span class="sample-use-pill">
                  {{ workflowInputPrompt === sample.prompt ? 'Wybrano' : 'Użyj' }}
                </span>
              </div>
              <div class="sample-prompt-body font-monospace">
                {{ sample.prompt }}
              </div>
            </button>
          </div>

          <div class="as-field-group mb-0">
            <label class="as-field-label">Prompt wejściowy (<code>&#123;&#123;input&#125;&#125;</code>):</label>
            <textarea
              v-model="workflowInputPrompt"
              rows="3"
              class="as-textarea font-monospace"
              placeholder="Wpisz polecenie dla przepływu wieloagentowego..."
            />
          </div>
        </div>

        <!-- Footer -->
        <div class="dialog-footer d-flex align-center justify-end ga-2 px-4 py-3">
          <v-btn variant="text" size="small" color="grey-lighten-1" @click="isRunDialogOpen = false">
            Cancel
          </v-btn>
          <v-btn
            color="success"
            variant="flat"
            size="small"
            prepend-icon="mdi-play"
            class="font-weight-bold px-4"
            @click="handleExecuteWorkflow"
          >
            Execute
          </v-btn>
        </div>
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
  background-color: #090D16;
}

/* Solid Dark Floating Toolbar (No Glass / No Blur) */
.floating-toolbar {
  position: absolute;
  top: 12px;
  left: 12px;
  right: 12px;
  z-index: 10;
  background-color: #111827;
  border-radius: 8px;
  border: 1px solid #24324B;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.4);
}

.tb-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 28px;
  padding: 0 10px;
  font-size: 12px;
  font-weight: 600;
  color: #E2E8F0;
  background-color: #161F30;
  border: 1px solid #283548;
  border-radius: 5px;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease, color 0.12s ease;
}

.tb-btn:hover:not(:disabled) {
  background-color: #1E293B;
  border-color: #3B4D6B;
  color: #FFFFFF;
}

.tb-btn:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.tb-btn-primary {
  background-color: #1D4ED8;
  border-color: #2563EB;
  color: #FFFFFF;
}

.tb-btn-primary:hover:not(:disabled) {
  background-color: #2563EB;
  border-color: #3B82F6;
}

.tb-btn-run {
  background-color: #15803D;
  border-color: #16A34A;
  color: #FFFFFF;
}

.tb-btn-run:hover:not(:disabled) {
  background-color: #16A34A;
  border-color: #22C55E;
}

.tb-icon-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  color: #94A3B8;
  background-color: transparent;
  border: 1px solid transparent;
  border-radius: 5px;
  cursor: pointer;
  transition: background-color 0.12s ease, color 0.12s ease, border-color 0.12s ease;
}

.tb-icon-btn:hover:not(:disabled) {
  background-color: #161F30;
  border-color: #283548;
  color: #F8FAFC;
}

.tb-icon-btn:disabled {
  opacity: 0.35;
  cursor: not-allowed;
}

.tb-icon-btn-active {
  background-color: #17253E;
  border-color: #2563EB;
  color: #60A5FA;
}

.tb-icon-btn-danger:hover:not(:disabled) {
  background-color: #2A1215;
  border-color: #7F1D1D;
  color: #F87171;
}

.tb-divider {
  width: 1px;
  height: 18px;
  background-color: #1E293B;
  margin: 0 2px;
}

.exec-status-badge {
  font-size: 11px;
  font-weight: 600;
  color: #94A3B8;
  background-color: #0D131F;
  border: 1px solid #1E293B;
  padding: 2px 8px;
  border-radius: 4px;
}

.exec-running {
  background-color: #132542;
  border-color: #1D4ED8;
  color: #93C5FD;
}

.exec-completed {
  background-color: #0D281E;
  border-color: #15803D;
  color: #4ADE80;
}

.exec-failed {
  background-color: #2A1215;
  border-color: #7F1D1D;
  color: #F87171;
}

.solid-dropdown-menu {
  background-color: #111827 !important;
  border: 1px solid #283548 !important;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.45) !important;
}

/* Solid Dialogs */
.solid-dialog-card {
  background-color: #111827 !important;
  border: 1px solid #283548 !important;
}

.dialog-header {
  background-color: #0D131F;
  border-bottom: 1px solid #1E293B;
}

.dialog-footer {
  background-color: #0D131F;
  border-top: 1px solid #1E293B;
}

.run-dialog-icon-box {
  width: 26px;
  height: 26px;
  border-radius: 6px;
  background-color: #0D281E;
  border: 1px solid #14532D;
  display: flex;
  align-items: center;
  justify-content: center;
}

.flow-tag {
  font-size: 11px;
  font-weight: 600;
  color: #94A3B8;
  background-color: #161F30;
  border: 1px solid #24324B;
  padding: 2px 8px;
  border-radius: 4px;
}

.section-label {
  font-size: 11.5px;
  font-weight: 600;
  color: #94A3B8;
}

.section-label code {
  color: #93C5FD;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

/* Clean Sample Prompt Cards (No Red Alert Look!) */
.sample-prompts-stack {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.sample-prompt-item {
  width: 100%;
  text-align: left;
  background-color: #151E2E;
  border: 1px solid #24324B;
  border-radius: 7px;
  padding: 10px 12px;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.sample-prompt-item:hover {
  background-color: #192438;
  border-color: #364B6E;
}

.sample-prompt-item-active {
  background-color: #152540 !important;
  border-color: #2563EB !important;
}

.sample-prompt-title {
  font-size: 12.5px;
  font-weight: 600;
  color: #F1F5F9;
}

.sample-prompt-item-active .sample-prompt-title {
  color: #93C5FD;
}

.sample-use-pill {
  font-size: 10px;
  font-weight: 600;
  color: #94A3B8;
  background-color: #0D131F;
  border: 1px solid #24324B;
  padding: 1px 6px;
  border-radius: 4px;
  flex-shrink: 0;
}

.sample-prompt-item-active .sample-use-pill {
  background-color: #1E3A5F;
  border-color: #2563EB;
  color: #93C5FD;
}

.sample-prompt-body {
  font-size: 11px;
  line-height: 1.4;
  color: #94A3B8;
  padding-left: 22px;
}

/* Shortcuts List */
.shortcut-row {
  background-color: #141C2B;
  border: 1px solid #1E293B;
}

.kbd-tag {
  font-size: 11px;
  font-weight: 600;
  color: #E2E8F0;
  background-color: #0D131F;
  border: 1px solid #283548;
  padding: 2px 7px;
  border-radius: 4px;
}
</style>
