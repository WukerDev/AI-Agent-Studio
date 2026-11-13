<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { GlobalViewModel } from '../../GlobalViewModel'
import { NodeType, type AgentNode } from '../../clients/generated/agent_node'
import { useNotificationStore } from '../../notifications'

const globalViewModel = new GlobalViewModel()
const notifStore = useNotificationStore()

const isOpen = computed({
  get: () => globalViewModel.isInspectorOpen,
  set: (val: boolean) => {
    globalViewModel.isInspectorOpen = val
  }
})

const localNode = ref<AgentNode | null>(null)
const enabledToolsInput = ref('')
const isDirty = ref(false)

const providers = ['Ollama', 'Gemini', 'OpenAI', 'Custom']

const geminiModels = [
  { name: 'gemini-3.5-flash-lite', desc: 'Domyślny · najszybszy', label: 'gemini-3.5-flash-lite (Domyślny, najszybszy i najtańszy)' },
  { name: 'gemini-flash-lite-latest', desc: 'Najnowszy Flash-Lite', label: 'gemini-flash-lite-latest (Zawsze najnowszy Flash-Lite)' },
  { name: 'gemini-3.1-flash-lite', desc: 'Lekki i stabilny', label: 'gemini-3.1-flash-lite (Lekki i stabilny)' },
  { name: 'gemini-3.8-flash', desc: 'Rekomendowany Flash', label: 'gemini-3.8-flash (Rekomendowany następca Flash)' },
  { name: 'gemini-flash-latest', desc: 'Najnowszy Flash', label: 'gemini-flash-latest (Najnowszy Flash)' },
  { name: 'gemini-3.5-flash', desc: 'Flash 3.5', label: 'gemini-3.5-flash (Flash 3.5)' },
  { name: 'gemini-3.1-pro-preview', desc: 'Rekomendowany Pro', label: 'gemini-3.1-pro-preview (Rekomendowany następca Pro)' },
  { name: 'gemini-pro-latest', desc: 'Najnowszy Pro', label: 'gemini-pro-latest (Najnowszy Pro)' },
  { name: 'gemini-4-argon', desc: 'Flagowy Gemini 4', label: 'gemini-4-argon (Flagowy model Gemini 4)' },
  { name: 'gemma-4-26b-a4b-it', desc: 'Otwarty Gemma 4', label: 'gemma-4-26b-a4b-it (Otwarty model Gemma 4 w API)' }
]

const noteColorOptions = [
  { title: 'Amber (Default)', value: '#F59E0B' },
  { title: 'Sky Blue', value: '#0EA5E9' },
  { title: 'Emerald Green', value: '#10B981' },
  { title: 'Purple', value: '#8B5CF6' },
  { title: 'Rose Pink', value: '#F43F5E' },
  { title: 'Slate Dark', value: '#475569' }
]

const labelSizeOptions = [
  { title: 'Small (14px)', value: 'small' },
  { title: 'Medium (18px)', value: 'medium' },
  { title: 'Large (24px)', value: 'large' },
  { title: 'Huge (32px)', value: 'huge' }
]

const labelColorOptions = [
  { title: 'Purple (Default)', value: '#8B5CF6' },
  { title: 'Blue', value: '#3B82F6' },
  { title: 'Green', value: '#10B981' },
  { title: 'Orange', value: '#F97316' },
  { title: 'Pink', value: '#EC4899' },
  { title: 'Slate', value: '#64748B' }
]

const commonMcpTools = [
  'calculate_expression',
  'get_current_time',
  'get_system_info',
  'search_web',
  'list_dir',
  'read_file',
  'write_file',
  'execute_query',
  'fetch_page'
]

const promptVariables = ['{{input}}', '{{date}}', '{{time}}', '{{agent}}', '{{flowId}}', '{{previous_output}}']

const formattedTemperature = computed(() => {
  const raw = localNode.value?.modelConfig?.temperature ?? 0.7
  return Number(raw).toFixed(2)
})

function handleTemperatureInput(event: Event) {
  const val = parseFloat((event.target as HTMLInputElement).value)
  if (!isNaN(val) && localNode.value?.modelConfig) {
    localNode.value.modelConfig.temperature = Math.round(val * 100) / 100
    isDirty.value = true
  }
}

const agentPortConnections = computed(() => {
  if (!localNode.value || localNode.value.type !== NodeType.AGENT) {
    return { model: null as AgentNode | null, prompts: [] as AgentNode[], tools: [] as AgentNode[], skills: [] as AgentNode[] }
  }
  const agentId = localNode.value.id
  const edges = globalViewModel.edges || []
  const allNodes = globalViewModel.nodes || []
  const neighborIds = new Set<string>()

  for (const e of edges) {
    if (e.sourceNodeId === agentId) neighborIds.add(e.targetNodeId)
    if (e.targetNodeId === agentId) neighborIds.add(e.sourceNodeId)
  }

  const neighbors = allNodes.filter(n => neighborIds.has(n.id))
  return {
    model: neighbors.find(n => n.type === NodeType.MODEL) || null,
    prompts: neighbors.filter(n => n.type === NodeType.PROMPT),
    tools: neighbors.filter(n => n.type === NodeType.MCP_CONNECTOR),
    skills: neighbors.filter(n => n.type === NodeType.SKILL)
  }
})

const selectedSkillDetails = computed(() => {
  if (!localNode.value || localNode.value.type !== NodeType.SKILL) return null
  const skillName = localNode.value.endpoint || ''
  return globalViewModel.skills.find(s => s.name === skillName) || null
})

function handleSelectSkillForNode(event: Event) {
  const skillName = (event.target as HTMLSelectElement).value
  if (!localNode.value) return
  localNode.value.endpoint = skillName
  const matched = globalViewModel.skills.find(s => s.name === skillName)
  if (matched) {
    localNode.value.systemPrompt = matched.description
    if (!localNode.value.name || localNode.value.name === 'Agent Skill' || localNode.value.name.startsWith('Skill:')) {
      localNode.value.name = `Skill: ${matched.name}`
    }
  }
  isDirty.value = true
}

watch(() => globalViewModel.selectedNode, (selected) => {
  if (selected) {
    if (selected.type === NodeType.SKILL && globalViewModel.skills.length === 0) {
      globalViewModel.loadSkills()
    }
    localNode.value = JSON.parse(JSON.stringify(selected))
    if (!localNode.value!.modelConfig) {
      localNode.value!.modelConfig = { provider: 'Ollama', modelName: 'gemma3:12b', temperature: 0.7 }
    } else {
      if (typeof localNode.value!.modelConfig.temperature === 'number') {
        localNode.value!.modelConfig.temperature = Math.round(localNode.value!.modelConfig.temperature * 100) / 100
      }
      if (localNode.value!.type === NodeType.MODEL) {
        if (!localNode.value!.modelConfig.provider) {
          localNode.value!.modelConfig.provider = 'Ollama'
        }
        if (!localNode.value!.modelConfig.modelName) {
          localNode.value!.modelConfig.modelName =
            localNode.value!.modelConfig.provider === 'Ollama'
              ? 'gemma3:12b'
              : localNode.value!.modelConfig.provider === 'Gemini'
                ? 'gemini-3.5-flash-lite'
                : 'gpt-4o'
        }
      }
    }
    if (!localNode.value!.mcpConfig) {
      localNode.value!.mcpConfig = {
        serverUrl: localNode.value!.type === NodeType.MCP_CONNECTOR ? 'builtin://tools' : '',
        authToken: '',
        enabledTools: []
      }
    } else if (localNode.value!.type === NodeType.MCP_CONNECTOR && !localNode.value!.mcpConfig.serverUrl) {
      localNode.value!.mcpConfig.serverUrl = 'builtin://tools'
    }
    if (localNode.value!.type === NodeType.NOTE && !localNode.value!.endpoint) {
      localNode.value!.endpoint = '#F59E0B'
    }
    if (localNode.value!.type === NodeType.LABEL) {
      if (!localNode.value!.endpoint) localNode.value!.endpoint = 'medium'
      if (!localNode.value!.modelConfig?.provider) {
        localNode.value!.modelConfig.provider = '#8B5CF6'
      }
    }
    enabledToolsInput.value = (localNode.value!.mcpConfig?.enabledTools || []).join(', ')
    isDirty.value = false
  } else {
    localNode.value = null
    enabledToolsInput.value = ''
    isDirty.value = false
  }
}, { immediate: true })

function handleProviderSelectChange(event: Event) {
  const provider = (event.target as HTMLSelectElement).value
  isDirty.value = true
  if (!localNode.value?.modelConfig) return
  localNode.value.modelConfig.provider = provider
  if (provider === 'Ollama') {
    if (!localNode.value.modelConfig.modelName || localNode.value.modelConfig.modelName === 'gpt-4o' || localNode.value.modelConfig.modelName.startsWith('gemini-')) {
      localNode.value.modelConfig.modelName = 'gemma3:12b'
    }
    if (!localNode.value.endpoint || localNode.value.endpoint.includes('googleapis.com') || localNode.value.endpoint.includes('openai.com')) {
      localNode.value.endpoint = 'http://localhost:11434'
    }
  } else if (provider === 'Gemini') {
    if (!localNode.value.modelConfig.modelName || localNode.value.modelConfig.modelName === 'gemma3:12b' || localNode.value.modelConfig.modelName === 'gpt-4o') {
      localNode.value.modelConfig.modelName = 'gemini-3.5-flash-lite'
    }
    if (!localNode.value.endpoint || localNode.value.endpoint.includes('localhost:11434') || localNode.value.endpoint.includes('openai.com')) {
      localNode.value.endpoint = 'https://generativelanguage.googleapis.com/v1beta/openai/'
    }
  } else if (provider === 'OpenAI') {
    if (!localNode.value.modelConfig.modelName || localNode.value.modelConfig.modelName === 'gemma3:12b' || localNode.value.modelConfig.modelName.startsWith('gemini-')) {
      localNode.value.modelConfig.modelName = 'gpt-4o'
    }
    if (!localNode.value.endpoint || localNode.value.endpoint.includes('localhost:11434') || localNode.value.endpoint.includes('googleapis.com')) {
      localNode.value.endpoint = 'https://api.openai.com/v1'
    }
  }
}

function insertPromptVariable(variable: string) {
  if (!localNode.value) return
  const current = localNode.value.systemPrompt || ''
  localNode.value.systemPrompt = current ? `${current} ${variable}` : variable
  isDirty.value = true
}

async function attachSubNodeToAgent(subType: NodeType) {
  if (!localNode.value || localNode.value.type !== NodeType.AGENT) return
  const agent = localNode.value
  const baseX = agent.position?.x ?? 300
  const baseY = (agent.position?.y ?? 150) + 190

  let pos = { x: baseX, y: baseY }
  let name = 'Sub Node'
  let customConfig: any = {}
  let sourceHandle = ''
  let targetHandle = ''

  if (subType === NodeType.MODEL) {
    pos = { x: baseX - 240, y: baseY }
    name = 'Ollama Gemma 3 (12B)'
    customConfig = {
      modelConfig: { provider: 'Ollama', modelName: 'gemma3:12b', temperature: 0.3 },
      endpoint: 'http://localhost:11434'
    }
    sourceHandle = 'model-out'
    targetHandle = 'model'
  } else if (subType === NodeType.PROMPT) {
    pos = { x: baseX - 60, y: baseY }
    name = 'System Prompt'
    customConfig = {
      systemPrompt: 'Jesteś inteligentnym asystentem AI. Zadanie użytkownika: {{input}}'
    }
    sourceHandle = 'prompt-out'
    targetHandle = 'prompt'
  } else if (subType === NodeType.MCP_CONNECTOR) {
    pos = { x: baseX + 120, y: baseY }
    name = 'MCP Tools'
    customConfig = {
      mcpConfig: {
        serverUrl: 'builtin://tools',
        authToken: '',
        enabledTools: ['calculate_expression', 'get_current_time', 'get_system_info', 'search_web']
      }
    }
    sourceHandle = 'tool-out'
    targetHandle = 'tool'
  } else if (subType === NodeType.SKILL) {
    if (globalViewModel.skills.length === 0) {
      await globalViewModel.loadSkills()
    }
    const defaultSkill = globalViewModel.skills[0]
    const skillFolder = defaultSkill?.name || 'code-review'
    pos = { x: baseX + 300, y: baseY }
    name = `Skill: ${skillFolder}`
    customConfig = {
      endpoint: skillFolder,
      systemPrompt: defaultSkill?.description || 'Specialized domain skill loaded via SKILL.md.'
    }
    sourceHandle = 'skill-out'
    targetHandle = 'skill'
  }

  await globalViewModel.addNode(subType, pos, name, customConfig)
  const created = globalViewModel.nodes[globalViewModel.nodes.length - 1]
  if (created) {
    const newEdge = {
      id: `e_${created.id}_${agent.id}`,
      flowId: globalViewModel.currentFlowId || 'default-flow',
      sourceNodeId: created.id,
      sourceHandle,
      targetNodeId: agent.id,
      targetHandle
    }
    await globalViewModel.saveEdges([...globalViewModel.edges, newEdge])
    notifStore.success(`Podłączono węzeł "${name}" do portu [${targetHandle}] agenta`)
  }
}

const inspectorTitle = computed(() => {
  if (!localNode.value) return 'Node Inspector'
  switch (localNode.value.type) {
    case NodeType.AGENT:
      return 'AI Agent Hub Inspector'
    case NodeType.MODEL:
      return 'LLM Model Inspector'
    case NodeType.PROMPT:
      return 'System Prompt Inspector'
    case NodeType.MCP_CONNECTOR:
      return 'MCP Tools Inspector'
    case NodeType.NOTE:
      return 'Sticky Note Inspector'
    case NodeType.LABEL:
      return 'Section Label Inspector'
    case NodeType.SKILL:
      return 'Agent Skill Inspector'
    default:
      return 'Node Inspector'
  }
})

const nodeTypeBadge = computed(() => {
  if (!localNode.value) return 'UNKNOWN'
  switch (localNode.value.type) {
    case NodeType.AGENT:
      return 'N8N AGENT HUB'
    case NodeType.MODEL:
      return 'MODEL PORT'
    case NodeType.PROMPT:
      return 'PROMPT PORT'
    case NodeType.MCP_CONNECTOR:
      return 'TOOL PORT (MCP)'
    case NodeType.NOTE:
      return 'STICKY NOTE'
    case NodeType.LABEL:
      return 'LABEL'
    case NodeType.SKILL:
      return 'SKILL PORT (SKILL.md)'
    default:
      return 'CUSTOM'
  }
})

const nameFieldLabel = computed(() => {
  if (localNode.value?.type === NodeType.LABEL) return 'Label Banner Text'
  if (localNode.value?.type === NodeType.NOTE) return 'Note Title'
  return 'Node Name'
})

function isToolActive(tool: string): boolean {
  return enabledToolsInput.value
    .split(',')
    .map(s => s.trim())
    .filter(Boolean)
    .includes(tool)
}

function toggleTool(tool: string) {
  const current = enabledToolsInput.value
    .split(',')
    .map(s => s.trim())
    .filter(Boolean)

  if (current.includes(tool)) {
    enabledToolsInput.value = current.filter(t => t !== tool).join(', ')
  } else {
    current.push(tool)
    enabledToolsInput.value = current.join(', ')
  }
  isDirty.value = true
}

async function handleSave() {
  if (!localNode.value) return

  globalViewModel.pushHistorySnapshot()

  if (localNode.value.type === NodeType.MCP_CONNECTOR && localNode.value.mcpConfig) {
    localNode.value.mcpConfig.enabledTools = enabledToolsInput.value
      .split(',')
      .map(s => s.trim())
      .filter(s => s.length > 0)
  }

  await globalViewModel.saveNode(localNode.value)
  isDirty.value = false
  notifStore.success(`Saved changes for "${localNode.value.name || 'Node'}"`)
}

async function handleDelete() {
  if (!localNode.value) return
  const id = localNode.value.id
  const name = localNode.value.name
  await globalViewModel.removeNode(id)
  globalViewModel.closeInspector()
  notifStore.info(`Deleted "${name || id}"`)
}

function handleClose() {
  globalViewModel.closeInspector()
}

function copyNodeId() {
  if (localNode.value?.id) {
    navigator.clipboard?.writeText(localNode.value.id)
    notifStore.info(`Copied ID: ${localNode.value.id}`)
  }
}
</script>

<template>
  <v-navigation-drawer
    v-model="isOpen"
    location="right"
    width="380"
    elevation="0"
    border="0"
    class="app-inspector-drawer"
  >
    <!-- Drawer Header -->
    <div class="inspector-header d-flex align-center justify-space-between px-3">
      <div class="d-flex align-center ga-2 overflow-hidden mr-2">
        <div
          class="inspector-icon-badge flex-shrink-0"
          :style="{ '--badge-color': localNode ? globalViewModel.getNodeColor(localNode.type) : '#3B82F6' }"
        >
          <v-icon
            :icon="localNode ? globalViewModel.getNodeIcon(localNode.type) : 'mdi-tune-variant'"
            size="15"
            :color="localNode ? globalViewModel.getNodeColor(localNode.type) : '#3B82F6'"
          />
        </div>
        <span class="inspector-title text-truncate">{{ inspectorTitle }}</span>
      </div>

      <v-btn
        icon="mdi-close"
        variant="text"
        size="x-small"
        color="grey-lighten-1"
        title="Close Inspector"
        @click="handleClose"
      />
    </div>

    <!-- Drawer Content Wrapper -->
    <div v-if="localNode" class="inspector-wrapper">
      <!-- Scrollable Block Body (Display Block: Never Flex-Stretches Inputs!) -->
      <div class="inspector-scroll-body">
        <!-- Node Identification Row -->
        <div class="d-flex align-center justify-space-between mb-3 pb-3 border-bottom-subtle">
          <span
            class="type-pill"
            :style="{ '--pill-color': globalViewModel.getNodeColor(localNode.type) }"
          >
            {{ nodeTypeBadge }}
          </span>

          <button
            type="button"
            class="id-copy-btn font-monospace d-flex align-center ga-1"
            title="Click to copy node ID"
            @click="copyNodeId"
          >
            <v-icon icon="mdi-content-copy" size="11" />
            <span>{{ localNode.id ? localNode.id.slice(0, 16) + '...' : '' }}</span>
          </button>
        </div>

        <!-- Common Name / Title -->
        <div class="as-field-group">
          <label class="as-field-label">{{ nameFieldLabel }}</label>
          <input
            v-model="localNode.name"
            type="text"
            class="as-input"
            placeholder="Enter node name..."
            @input="isDirty = true"
          />
        </div>

        <!-- =============================================================== -->
        <!-- 1. AI AGENT HUB INSPECTOR                                       -->
        <!-- =============================================================== -->
        <template v-if="localNode?.type === NodeType.AGENT">
          <div class="info-callout mb-3">
            Węzeł <strong>AI Agent</strong> korzysta z architektury portów. Podłącz lub skonfiguruj moduły poniżej:
          </div>

          <div class="ports-stack">
            <!-- Port 1: Model -->
            <div class="port-card" style="--port-accent: #3B82F6;">
              <div class="d-flex align-center justify-space-between mb-2">
                <div class="d-flex align-center ga-2">
                  <v-icon icon="mdi-brain" size="15" color="#60A5FA" />
                  <span class="port-card-title">Port [model] — LLM Model *</span>
                </div>
                <span class="port-status-badge" :class="agentPortConnections.model ? 'badge-blue' : 'badge-muted'">
                  {{ agentPortConnections.model ? 'CONNECTED' : 'UNCONNECTED' }}
                </span>
              </div>

              <div v-if="agentPortConnections.model" class="d-flex align-center justify-space-between ga-2">
                <div class="overflow-hidden">
                  <div class="port-node-name text-truncate">{{ agentPortConnections.model.name }}</div>
                  <div class="port-node-meta font-monospace text-truncate" style="color: #60A5FA;">
                    {{ agentPortConnections.model.modelConfig?.provider }}: {{ agentPortConnections.model.modelConfig?.modelName }}
                  </div>
                </div>
                <button
                  type="button"
                  class="port-action-btn btn-blue flex-shrink-0"
                  @click="globalViewModel.openInspector(agentPortConnections.model.id)"
                >
                  Konfiguruj
                </button>
              </div>
              <div v-else class="d-flex align-center justify-space-between ga-2">
                <span class="port-empty-text">Brak podłączonego modelu</span>
                <button
                  type="button"
                  class="port-action-btn btn-blue flex-shrink-0"
                  @click="attachSubNodeToAgent(NodeType.MODEL)"
                >
                  + Podłącz Model
                </button>
              </div>
            </div>

            <!-- Port 2: System Prompt -->
            <div class="port-card" style="--port-accent: #F59E0B;">
              <div class="d-flex align-center justify-space-between mb-2">
                <div class="d-flex align-center ga-2">
                  <v-icon icon="mdi-script-text-outline" size="15" color="#FBBF24" />
                  <span class="port-card-title">Port [prompt] — System Prompt</span>
                </div>
                <span class="port-status-badge" :class="agentPortConnections.prompts.length ? 'badge-amber' : 'badge-muted'">
                  {{ agentPortConnections.prompts.length }} ACTIVE
                </span>
              </div>

              <div v-if="agentPortConnections.prompts.length > 0" class="port-items-list">
                <div
                  v-for="pNode in agentPortConnections.prompts"
                  :key="pNode.id"
                  class="d-flex align-center justify-space-between ga-2"
                >
                  <span class="port-node-name text-truncate">{{ pNode.name }}</span>
                  <button
                    type="button"
                    class="port-action-btn btn-amber flex-shrink-0"
                    @click="globalViewModel.openInspector(pNode.id)"
                  >
                    Edytuj Prompt
                  </button>
                </div>
              </div>
              <div v-else class="d-flex align-center justify-space-between ga-2">
                <span class="port-empty-text">Brak podłączonego promptu</span>
                <button
                  type="button"
                  class="port-action-btn btn-amber flex-shrink-0"
                  @click="attachSubNodeToAgent(NodeType.PROMPT)"
                >
                  + Podłącz Prompt
                </button>
              </div>
            </div>

            <!-- Port 3: MCP Tool -->
            <div class="port-card" style="--port-accent: #10B981;">
              <div class="d-flex align-center justify-space-between mb-2">
                <div class="d-flex align-center ga-2">
                  <v-icon icon="mdi-connection" size="15" color="#34D399" />
                  <span class="port-card-title">Port [tool] — MCP Tools</span>
                </div>
                <span class="port-status-badge" :class="agentPortConnections.tools.length ? 'badge-green' : 'badge-muted'">
                  {{ agentPortConnections.tools.length }} ACTIVE
                </span>
              </div>

              <div v-if="agentPortConnections.tools.length > 0" class="port-items-list">
                <div
                  v-for="tNode in agentPortConnections.tools"
                  :key="tNode.id"
                  class="d-flex align-center justify-space-between ga-2"
                >
                  <div class="overflow-hidden">
                    <div class="port-node-name text-truncate">{{ tNode.name }}</div>
                    <div class="port-node-meta font-monospace" style="color: #34D399;">
                      {{ tNode.mcpConfig?.enabledTools?.length || 'All' }} tools enabled
                    </div>
                  </div>
                  <button
                    type="button"
                    class="port-action-btn btn-green flex-shrink-0"
                    @click="globalViewModel.openInspector(tNode.id)"
                  >
                    Konfiguruj MCP
                  </button>
                </div>
              </div>
              <div v-else class="d-flex align-center justify-space-between ga-2">
                <span class="port-empty-text">Brak podłączonego węzła MCP</span>
                <button
                  type="button"
                  class="port-action-btn btn-green flex-shrink-0"
                  @click="attachSubNodeToAgent(NodeType.MCP_CONNECTOR)"
                >
                  + Podłącz MCP
                </button>
              </div>
            </div>

            <!-- Port 4: Agent Skill (SKILL.md) -->
            <div class="port-card" style="--port-accent: #06B6D4;">
              <div class="d-flex align-center justify-space-between mb-2">
                <div class="d-flex align-center ga-2">
                  <v-icon icon="mdi-book-open-page-variant-outline" size="15" color="#22D3EE" />
                  <span class="port-card-title">Port [skill] — Agent Skills</span>
                </div>
                <span class="port-status-badge" :class="agentPortConnections.skills.length ? 'badge-cyan' : 'badge-muted'">
                  {{ agentPortConnections.skills.length }} ACTIVE
                </span>
              </div>

              <div v-if="agentPortConnections.skills.length > 0" class="port-items-list">
                <div
                  v-for="sNode in agentPortConnections.skills"
                  :key="sNode.id"
                  class="d-flex align-center justify-space-between ga-2"
                >
                  <div class="overflow-hidden">
                    <div class="port-node-name text-truncate">{{ sNode.name }}</div>
                    <div class="port-node-meta font-monospace text-truncate" style="color: #22D3EE;">
                      {{ sNode.endpoint ? `${sNode.endpoint}/SKILL.md` : 'SKILL.md' }}
                    </div>
                  </div>
                  <button
                    type="button"
                    class="port-action-btn btn-cyan flex-shrink-0"
                    @click="globalViewModel.openInspector(sNode.id)"
                  >
                    Konfiguruj
                  </button>
                </div>
              </div>
              <div v-else class="d-flex align-center justify-space-between ga-2">
                <span class="port-empty-text">Brak podłączonego węzła Skill</span>
                <button
                  type="button"
                  class="port-action-btn btn-cyan flex-shrink-0"
                  @click="attachSubNodeToAgent(NodeType.SKILL)"
                >
                  + Podłącz Skill
                </button>
              </div>
            </div>
          </div>
        </template>

        <!-- =============================================================== -->
        <!-- 2. AGENT SKILL NODE INSPECTOR                                   -->
        <!-- =============================================================== -->
        <template v-if="localNode?.type === NodeType.SKILL">
          <div class="info-callout info-callout-cyan mb-3">
            Ten węzeł podpina skill zgodny z konwencją <code>Microsoft.Agents.AI</code> (plik <code>skills/&lt;nazwa&gt;/SKILL.md</code>) do portu <code>[skill]</code> Agenta AI.
          </div>

          <div class="as-field-group">
            <label class="as-field-label">Wybierz Skill (folder SKILL.md)</label>
            <select
              :value="localNode.endpoint"
              class="as-select font-monospace"
              @change="handleSelectSkillForNode"
            >
              <option value="" disabled>-- Wybierz skill --</option>
              <option v-for="s in globalViewModel.skills" :key="s.name" :value="s.name">
                {{ s.name }}/SKILL.md
              </option>
            </select>
          </div>

          <div class="as-field-group">
            <label class="as-field-label">Folder Skilla (skills/&lt;folder&gt;/SKILL.md)</label>
            <input
              v-model="localNode.endpoint"
              type="text"
              class="as-input font-monospace"
              placeholder="np. code-review"
              @input="isDirty = true"
            />
          </div>

          <div class="as-field-group">
            <label class="as-field-label">Opis / adnotacja skilla na węźle</label>
            <textarea
              v-model="localNode.systemPrompt"
              rows="2"
              class="as-textarea"
              placeholder="Opis zastosowania skilla..."
              @input="isDirty = true"
            />
          </div>

          <div v-if="selectedSkillDetails" class="skill-detail-card">
            <div class="d-flex align-center justify-space-between mb-2">
              <span class="text-caption font-weight-bold font-monospace" style="color: #67E8F9;">
                {{ selectedSkillDetails.path || `skills/${selectedSkillDetails.name}/SKILL.md` }}
              </span>
              <span class="port-status-badge badge-cyan">SKILL.md</span>
            </div>
            <div class="text-caption mb-2" style="color: #94A3B8; line-height: 1.4;">
              {{ selectedSkillDetails.description }}
            </div>
            <pre class="skill-code-pre pa-2 rounded text-caption font-monospace overflow-x-auto">{{ selectedSkillDetails.content }}</pre>
          </div>
        </template>

        <!-- =============================================================== -->
        <!-- 3. STICKY NOTE INSPECTOR                                        -->
        <!-- =============================================================== -->
        <template v-if="localNode?.type === NodeType.NOTE">
          <div class="as-field-group">
            <label class="as-field-label">Note Content</label>
            <textarea
              v-model="localNode.systemPrompt"
              rows="6"
              class="as-textarea"
              placeholder="Write workflow notes, architectural guidelines, or markdown comments..."
              @input="isDirty = true"
            />
            <div class="as-field-hint">Supports multiline text and documentation.</div>
          </div>

          <div class="as-field-group">
            <label class="as-field-label">Sticky Note Color</label>
            <select
              v-model="localNode.endpoint"
              class="as-select"
              @change="isDirty = true"
            >
              <option v-for="opt in noteColorOptions" :key="opt.value" :value="opt.value">
                {{ opt.title }}
              </option>
            </select>
          </div>
        </template>

        <!-- =============================================================== -->
        <!-- 4. SECTION LABEL INSPECTOR                                      -->
        <!-- =============================================================== -->
        <template v-if="localNode?.type === NodeType.LABEL">
          <div class="as-field-group">
            <label class="as-field-label">Subtitle / Description (Optional)</label>
            <input
              v-model="localNode.systemPrompt"
              type="text"
              class="as-input"
              placeholder="e.g. Primary processing workflow"
              @input="isDirty = true"
            />
          </div>

          <div class="two-col-grid">
            <div class="as-field-group">
              <label class="as-field-label">Font Size</label>
              <select v-model="localNode.endpoint" class="as-select" @change="isDirty = true">
                <option v-for="opt in labelSizeOptions" :key="opt.value" :value="opt.value">
                  {{ opt.title }}
                </option>
              </select>
            </div>
            <div class="as-field-group">
              <label class="as-field-label">Accent Color</label>
              <select v-model="localNode.modelConfig!.provider" class="as-select" @change="isDirty = true">
                <option v-for="opt in labelColorOptions" :key="opt.value" :value="opt.value">
                  {{ opt.title }}
                </option>
              </select>
            </div>
          </div>
        </template>

        <!-- =============================================================== -->
        <!-- 5. SYSTEM PROMPT INSPECTOR                                      -->
        <!-- =============================================================== -->
        <template v-if="localNode?.type === NodeType.PROMPT">
          <div class="as-field-group">
            <label class="as-field-label">System Prompt / Instructions</label>
            <textarea
              v-model="localNode.systemPrompt"
              rows="7"
              class="as-textarea font-monospace"
              placeholder="You are an autonomous AI Agent with specialized reasoning..."
              @input="isDirty = true"
            />
            <div class="as-field-hint">Operational guidelines, persona role, and reasoning prompts.</div>
          </div>

          <div class="as-field-group">
            <label class="as-field-label">Insert Template Variable (click to append):</label>
            <div class="chips-wrap-grid">
              <button
                v-for="v in promptVariables"
                :key="v"
                type="button"
                class="var-chip-btn font-monospace"
                @click="insertPromptVariable(v)"
              >
                {{ v }}
              </button>
            </div>
          </div>
        </template>

        <!-- =============================================================== -->
        <!-- 6. LLM MODEL INSPECTOR                                          -->
        <!-- =============================================================== -->
        <template v-if="localNode?.type === NodeType.MODEL">
          <div class="as-field-group">
            <label class="as-field-label">LLM Provider</label>
            <select
              :value="localNode.modelConfig!.provider"
              class="as-select"
              @change="handleProviderSelectChange"
            >
              <option v-for="p in providers" :key="p" :value="p">{{ p }}</option>
            </select>
          </div>

          <div class="as-field-group">
            <label class="as-field-label">Model Name</label>
            <input
              v-model="localNode.modelConfig!.modelName"
              type="text"
              class="as-input font-monospace"
              placeholder="e.g. gemma3:12b, gemini-3.5-flash-lite, gpt-4o"
              @input="isDirty = true"
            />
          </div>

          <!-- Structured, Clean Gemini Model Selector -->
          <div v-if="localNode.modelConfig?.provider === 'Gemini'" class="as-field-group">
            <label class="as-field-label">Dostępne modele Google Gemini API:</label>
            <div class="gemini-model-list">
              <button
                v-for="gm in geminiModels"
                :key="gm.name"
                type="button"
                class="gemini-model-row"
                :class="{ 'gemini-model-row-active': localNode.modelConfig.modelName === gm.name }"
                @click="localNode.modelConfig.modelName = gm.name; isDirty = true"
              >
                <div class="d-flex align-center ga-2 overflow-hidden">
                  <v-icon
                    :icon="localNode.modelConfig.modelName === gm.name ? 'mdi-radiobox-marked' : 'mdi-radiobox-blank'"
                    size="13"
                    :color="localNode.modelConfig.modelName === gm.name ? '#60A5FA' : '#64748B'"
                  />
                  <span class="gemini-model-name font-monospace text-truncate">{{ gm.name }}</span>
                </div>
                <span class="gemini-model-desc flex-shrink-0">{{ gm.desc }}</span>
              </button>
            </div>
          </div>

          <div v-if="localNode.modelConfig?.provider !== 'Ollama'" class="as-field-group">
            <label class="as-field-label">API Key (Gemini / OpenAI — Optional)</label>
            <input
              v-model="localNode.mcpConfig!.authToken"
              type="password"
              class="as-input font-monospace"
              placeholder="Paste API key (overrides server env)"
              @input="isDirty = true"
            />
          </div>

          <div class="as-field-group">
            <label class="as-field-label">Custom Endpoint URL (Optional)</label>
            <input
              v-model="localNode.endpoint"
              type="text"
              class="as-input font-monospace"
              placeholder="e.g. http://localhost:11434"
              @input="isDirty = true"
            />
          </div>

          <div class="temp-control-box">
            <div class="d-flex align-center justify-space-between mb-2">
              <span class="as-field-label mb-0">Sampling Temperature</span>
              <span class="temp-value-badge font-monospace">{{ formattedTemperature }}</span>
            </div>
            <input
              type="range"
              min="0"
              max="2"
              step="0.05"
              :value="localNode.modelConfig!.temperature"
              class="as-range-slider"
              @input="handleTemperatureInput"
            />
            <div class="d-flex justify-space-between font-monospace mt-1" style="font-size: 10px; color: #64748B;">
              <span>0.00 (Precise)</span>
              <span>1.00 (Balanced)</span>
              <span>2.00 (Creative)</span>
            </div>
          </div>
        </template>

        <!-- =============================================================== -->
        <!-- 7. MCP CONNECTOR INSPECTOR                                      -->
        <!-- =============================================================== -->
        <template v-if="localNode?.type === NodeType.MCP_CONNECTOR">
          <div class="as-field-group">
            <label class="as-field-label">MCP Server URL</label>
            <input
              v-model="localNode!.mcpConfig!.serverUrl"
              type="text"
              class="as-input font-monospace"
              placeholder="e.g. builtin://tools or http://localhost:8000/mcp"
              @input="isDirty = true"
            />
          </div>

          <div class="as-field-group">
            <label class="as-field-label">MCP Auth Token (Optional)</label>
            <input
              v-model="localNode!.mcpConfig!.authToken"
              type="password"
              class="as-input"
              placeholder="Optional bearer token..."
              @input="isDirty = true"
            />
          </div>

          <div class="as-field-group">
            <label class="as-field-label">Enabled Tools (comma-separated)</label>
            <input
              v-model="enabledToolsInput"
              type="text"
              class="as-input font-monospace"
              placeholder="e.g. calculate_expression, get_system_info"
              @input="isDirty = true"
            />
            <div class="as-field-hint">Leave blank to grant all available MCP tools.</div>
          </div>

          <!-- Quick MCP tool toggles -->
          <div class="as-field-group">
            <label class="as-field-label">Quick Tools (click to toggle):</label>
            <div class="chips-wrap-grid">
              <button
                v-for="tool in commonMcpTools"
                :key="tool"
                type="button"
                class="tool-toggle-btn font-monospace"
                :class="{ 'tool-toggle-btn-active': isToolActive(tool) }"
                @click="toggleTool(tool)"
              >
                <v-icon
                  :icon="isToolActive(tool) ? 'mdi-check' : 'mdi-plus'"
                  size="11"
                />
                <span>{{ tool }}</span>
              </button>
            </div>
          </div>
        </template>
      </div>

      <!-- Sticky Bottom Footer (Always Visible, Cleanly Separated) -->
      <div class="inspector-footer">
        <div class="meta-footer-bar d-flex align-center justify-space-between font-monospace mb-2">
          <span>Pos: ({{ Math.round(localNode?.position?.x ?? 0) }}, {{ Math.round(localNode?.position?.y ?? 0) }})</span>
          <span>Flow: {{ localNode?.flowId || 'default-flow' }}</span>
        </div>

        <div class="footer-actions-row">
          <button type="button" class="save-btn-solid" @click="handleSave">
            <v-icon icon="mdi-content-save-outline" size="15" />
            <span>Save Changes</span>
          </button>

          <button type="button" class="delete-btn-solid" title="Delete Node" @click="handleDelete">
            <v-icon icon="mdi-delete-outline" size="16" />
          </button>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div v-else class="empty-inspector-state pa-6 d-flex flex-column align-center justify-center text-center">
      <div class="empty-icon-box mb-3">
        <v-icon icon="mdi-cursor-default-click-outline" size="24" color="#64748B" />
      </div>
      <div class="text-subtitle-2 font-weight-bold text-white mb-1">
        No Node Selected
      </div>
      <p class="text-caption" style="color: #64748B; max-width: 240px;">
        Click on any agent, model, prompt, connector, skill, note, or label on the canvas to configure its parameters.
      </p>
    </div>
  </v-navigation-drawer>
</template>

<style scoped>
.app-inspector-drawer {
  background-color: #111827 !important;
  border-left: 1px solid #1E293B !important;
  color: #E2E8F0;
  z-index: 20;
}

.inspector-header {
  height: 44px;
  background-color: #0D131F;
  border-bottom: 1px solid #1E293B;
}

.inspector-icon-badge {
  width: 26px;
  height: 26px;
  border-radius: 6px;
  background-color: #161F30;
  border: 1px solid #283548;
  display: flex;
  align-items: center;
  justify-content: center;
}

.inspector-title {
  font-size: 13px;
  font-weight: 700;
  color: #F8FAFC;
  letter-spacing: -0.01em;
}

.inspector-wrapper {
  display: flex;
  flex-direction: column;
  height: calc(100% - 44px);
}

.inspector-scroll-body {
  flex: 1 1 auto;
  overflow-y: auto;
  padding: 14px 16px;
  display: block;
}

.inspector-footer {
  flex: 0 0 auto;
  padding: 10px 16px 12px;
  background-color: #0D131F;
  border-top: 1px solid #1E293B;
}

.border-bottom-subtle {
  border-bottom: 1px solid #1E293B;
}

.type-pill {
  font-size: 10px;
  font-weight: 700;
  color: #E2E8F0;
  background-color: #161F30;
  border: 1px solid #283548;
  border-left: 3px solid var(--pill-color, #3B82F6);
  padding: 2px 8px;
  border-radius: 4px;
  letter-spacing: 0.04em;
}

.id-copy-btn {
  font-size: 11px;
  color: #64748B;
  background-color: transparent;
  border: 1px solid transparent;
  padding: 2px 6px;
  border-radius: 4px;
  cursor: pointer;
  transition: color 0.12s ease, background-color 0.12s ease, border-color 0.12s ease;
}

.id-copy-btn:hover {
  color: #E2E8F0;
  background-color: #161F30;
  border-color: #283548;
}

/* Info Callouts */
.info-callout {
  background-color: #141C2B;
  border: 1px solid #1E293B;
  border-left: 3px solid #E63946;
  border-radius: 6px;
  padding: 8px 10px;
  font-size: 11.5px;
  line-height: 1.4;
  color: #94A3B8;
}

.info-callout-cyan {
  border-left-color: #06B6D4;
}

.info-callout code {
  color: #67E8F9;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

/* Port Cards Stack (AI Agent Hub Inspector) */
.ports-stack {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.port-card {
  background-color: #151E2E;
  border: 1px solid #24324B;
  border-left: 3px solid var(--port-accent, #3B82F6);
  border-radius: 7px;
  padding: 10px 12px;
}

.port-items-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.port-card-title {
  font-size: 12px;
  font-weight: 700;
  color: #F1F5F9;
}

.port-node-name {
  font-size: 12px;
  font-weight: 600;
  color: #E2E8F0;
}

.port-node-meta {
  font-size: 10.5px;
  line-height: 1.3;
}

.port-empty-text {
  font-size: 11.5px;
  color: #64748B;
}

.port-status-badge {
  font-size: 9.5px;
  font-weight: 700;
  padding: 1px 6px;
  border-radius: 4px;
  letter-spacing: 0.03em;
}

.badge-muted {
  background-color: #0D131F;
  border: 1px solid #1E293B;
  color: #64748B;
}

.badge-blue {
  background-color: #132542;
  border: 1px solid #1D4ED8;
  color: #93C5FD;
}

.badge-amber {
  background-color: #291D0E;
  border: 1px solid #B45309;
  color: #FCD34D;
}

.badge-green {
  background-color: #0D281E;
  border: 1px solid #15803D;
  color: #6EE7B7;
}

.badge-cyan {
  background-color: #0F2937;
  border: 1px solid #0E7490;
  color: #67E8F9;
}

.port-action-btn {
  font-size: 11px;
  font-weight: 600;
  padding: 4px 10px;
  border-radius: 5px;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.btn-blue {
  background-color: #162B4D;
  border: 1px solid #2563EB;
  color: #93C5FD;
}
.btn-blue:hover {
  background-color: #1E3A66;
}

.btn-amber {
  background-color: #2B1F10;
  border: 1px solid #D97706;
  color: #FCD34D;
}
.btn-amber:hover {
  background-color: #3B2A14;
}

.btn-green {
  background-color: #112E22;
  border: 1px solid #059669;
  color: #6EE7B7;
}
.btn-green:hover {
  background-color: #163E2E;
}

.btn-cyan {
  background-color: #122B39;
  border: 1px solid #0891B2;
  color: #67E8F9;
}
.btn-cyan:hover {
  background-color: #173A4C;
}

.two-col-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}

/* Gemini Model Selector List */
.gemini-model-list {
  display: flex;
  flex-direction: column;
  gap: 3px;
  max-height: 195px;
  overflow-y: auto;
  background-color: #0D131F;
  border: 1px solid #24324B;
  border-radius: 6px;
  padding: 5px;
}

.gemini-model-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  width: 100%;
  text-align: left;
  padding: 6px 8px;
  border-radius: 5px;
  background-color: transparent;
  border: 1px solid transparent;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.gemini-model-row:hover {
  background-color: #161F30;
}

.gemini-model-row-active {
  background-color: #152642 !important;
  border-color: #2563EB !important;
}

.gemini-model-name {
  font-size: 11.5px;
  font-weight: 600;
  color: #E2E8F0;
}

.gemini-model-row-active .gemini-model-name {
  color: #93C5FD;
}

.gemini-model-desc {
  font-size: 10.5px;
  color: #64748B;
}

/* Temperature Control Box */
.temp-control-box {
  background-color: #141C2B;
  border: 1px solid #1E293B;
  border-radius: 6px;
  padding: 10px 12px;
}

.temp-value-badge {
  font-size: 11.5px;
  font-weight: 700;
  color: #93C5FD;
  background-color: #0D131F;
  border: 1px solid #24324B;
  padding: 1px 7px;
  border-radius: 4px;
}

.as-range-slider {
  width: 100%;
  height: 5px;
  accent-color: #3B82F6;
  background-color: #0D131F;
  border-radius: 3px;
  cursor: pointer;
}

/* Chips Grid */
.chips-wrap-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.var-chip-btn {
  font-size: 11px;
  font-weight: 600;
  color: #FCD34D;
  background-color: #241B0E;
  border: 1px solid #78350F;
  padding: 3px 8px;
  border-radius: 5px;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.var-chip-btn:hover {
  background-color: #332512;
  border-color: #B45309;
}

.tool-toggle-btn {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  font-weight: 500;
  color: #94A3B8;
  background-color: #141C2B;
  border: 1px solid #24324B;
  padding: 4px 9px;
  border-radius: 5px;
  cursor: pointer;
  transition: all 0.12s ease;
}

.tool-toggle-btn:hover {
  background-color: #1A2538;
  color: #E2E8F0;
  border-color: #334155;
}

.tool-toggle-btn-active {
  background-color: #0E291E !important;
  border-color: #15803D !important;
  color: #4ADE80 !important;
  font-weight: 600;
}

/* Skill Detail Card */
.skill-detail-card {
  background-color: #141C2B;
  border: 1px solid #24324B;
  border-radius: 6px;
  padding: 10px 12px;
}

.skill-code-pre {
  background-color: #090D16;
  border: 1px solid #1E293B;
  color: #CBD5E1;
  max-height: 180px;
  white-space: pre-wrap;
}

/* Footer Controls */
.meta-footer-bar {
  font-size: 11px;
  color: #64748B;
}

.footer-actions-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.save-btn-solid {
  flex: 1;
  height: 34px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  background-color: #1D4ED8;
  border: 1px solid #2563EB;
  color: #FFFFFF;
  font-size: 12.5px;
  font-weight: 600;
  border-radius: 6px;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.save-btn-solid:hover {
  background-color: #2563EB;
  border-color: #3B82F6;
}

.delete-btn-solid {
  width: 34px;
  height: 34px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background-color: #2A1215;
  border: 1px solid #7F1D1D;
  color: #F87171;
  border-radius: 6px;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.delete-btn-solid:hover {
  background-color: #3B161B;
  border-color: #991B1B;
}

.empty-inspector-state {
  height: calc(100% - 44px);
}

.empty-icon-box {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  background-color: #161F30;
  border: 1px solid #26334D;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
