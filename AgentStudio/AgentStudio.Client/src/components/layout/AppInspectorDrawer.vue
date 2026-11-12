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
  { name: 'gemini-3.5-flash-lite', label: 'gemini-3.5-flash-lite (Domyślny, najszybszy i najtańszy)' },
  { name: 'gemini-flash-lite-latest', label: 'gemini-flash-lite-latest (Zawsze najnowszy Flash-Lite)' },
  { name: 'gemini-3.1-flash-lite', label: 'gemini-3.1-flash-lite (Lekki i stabilny)' },
  { name: 'gemini-3.8-flash', label: 'gemini-3.8-flash (Rekomendowany następca Flash)' },
  { name: 'gemini-flash-latest', label: 'gemini-flash-latest (Najnowszy Flash)' },
  { name: 'gemini-3.5-flash', label: 'gemini-3.5-flash (Flash 3.5)' },
  { name: 'gemini-3.1-pro-preview', label: 'gemini-3.1-pro-preview (Rekomendowany następca Pro)' },
  { name: 'gemini-pro-latest', label: 'gemini-pro-latest (Najnowszy Pro)' },
  { name: 'gemini-4-argon', label: 'gemini-4-argon (Flagowy model Gemini 4)' },
  { name: 'gemma-4-26b-a4b-it', label: 'gemma-4-26b-a4b-it (Otwarty model Gemma 4 w API)' }
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

function handleSelectSkillForNode(skillName: string) {
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
    } else if (localNode.value!.type === NodeType.MODEL) {
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

function handleProviderChange(provider: string) {
  isDirty.value = true
  if (!localNode.value?.modelConfig) return
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
      return 'Agent Skill Inspector (SKILL.md)'
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
    elevation="6"
    border="0"
    class="app-inspector-drawer"
  >
    <!-- Drawer Header -->
    <v-toolbar density="compact" color="surface" class="px-3 border-bottom-subtle">
      <v-avatar size="24" :color="localNode ? globalViewModel.getNodeColor(localNode.type) : 'primary'" class="mr-2">
        <v-icon
          :icon="localNode ? globalViewModel.getNodeIcon(localNode.type) : 'mdi-tune-variant'"
          size="14"
          color="white"
        />
      </v-avatar>
      <div class="overflow-hidden mr-2">
        <div class="text-subtitle-2 font-weight-bold text-truncate">
          {{ inspectorTitle }}
        </div>
      </div>
      <v-spacer />
      <v-btn
        icon="mdi-close"
        variant="text"
        size="small"
        color="grey-lighten-1"
        title="Close Inspector"
        @click="handleClose"
      />
    </v-toolbar>

    <!-- Drawer Content -->
    <div v-if="localNode" class="inspector-scroll-area pa-4">
      <!-- Node Identification -->
      <div class="d-flex align-center justify-space-between mb-3 pb-2 border-bottom-subtle">
        <v-chip
          size="x-small"
          :color="globalViewModel.getNodeColor(localNode.type)"
          variant="tonal"
          class="font-weight-bold"
        >
          {{ nodeTypeBadge }}
        </v-chip>

        <v-chip
          size="x-small"
          variant="text"
          class="font-monospace text-grey"
          prepend-icon="mdi-content-copy"
          title="Click to copy node ID"
          @click="copyNodeId"
        >
          {{ localNode.id ? localNode.id.slice(0, 16) + '...' : '' }}
        </v-chip>
      </div>

      <!-- Common Name / Title -->
      <v-text-field
        v-model="localNode.name"
        :label="nameFieldLabel"
        variant="outlined"
        density="compact"
        class="mb-3"
        @input="isDirty = true"
      />

      <!-- AI Agent Inspector (n8n-style Hub: Sub-nodes provide Model, System Prompt & MCP Tools) -->
      <template v-if="localNode?.type === NodeType.AGENT">
        <div class="text-caption text-grey mb-3">
          Węzeł <strong>AI Agent</strong> działa w architekturze portów (jak w n8n). Konfiguracja modelu LLM, instrukcji System Prompt oraz narzędzi MCP jest dostarczana przez osobne węzły podłączone do dolnych portów agenta:
        </div>

        <!-- Port 1: Model -->
        <v-card variant="outlined" class="pa-2.5 mb-2.5 port-card port-card-model">
          <div class="d-flex align-center justify-space-between mb-1">
            <div class="d-flex align-center">
              <v-icon icon="mdi-brain" size="16" color="info" class="mr-1.5" />
              <span class="text-caption font-weight-bold">Port [model] — LLM Model *</span>
            </div>
            <v-chip size="x-small" :color="agentPortConnections.model ? 'info' : 'grey'" variant="tonal">
              {{ agentPortConnections.model ? 'CONNECTED' : 'UNCONNECTED' }}
            </v-chip>
          </div>
          <div v-if="agentPortConnections.model" class="d-flex align-center justify-space-between mt-1">
            <div class="text-caption text-grey-lighten-1">
              {{ agentPortConnections.model.name }}
              <span class="font-monospace text-info">
                ({{ agentPortConnections.model.modelConfig?.provider }}: {{ agentPortConnections.model.modelConfig?.modelName }})
              </span>
            </div>
            <v-btn
              size="x-small"
              variant="tonal"
              color="info"
              @click="globalViewModel.openInspector(agentPortConnections.model.id)"
            >
              Konfiguruj
            </v-btn>
          </div>
          <div v-else class="d-flex align-center justify-space-between mt-1">
            <span class="text-caption text-grey">Brak podłączonego węzła Model</span>
            <v-btn size="x-small" variant="tonal" color="info" prepend-icon="mdi-plus" @click="attachSubNodeToAgent(NodeType.MODEL)">
              Podłącz Model
            </v-btn>
          </div>
        </v-card>

        <!-- Port 2: System Prompt -->
        <v-card variant="outlined" class="pa-2.5 mb-2.5 port-card port-card-prompt">
          <div class="d-flex align-center justify-space-between mb-1">
            <div class="d-flex align-center">
              <v-icon icon="mdi-script-text-outline" size="16" color="warning" class="mr-1.5" />
              <span class="text-caption font-weight-bold">Port [prompt] — System Prompt</span>
            </div>
            <v-chip size="x-small" :color="agentPortConnections.prompts.length ? 'warning' : 'grey'" variant="tonal">
              {{ agentPortConnections.prompts.length }} ACTIVE
            </v-chip>
          </div>
          <div v-if="agentPortConnections.prompts.length > 0">
            <div
              v-for="pNode in agentPortConnections.prompts"
              :key="pNode.id"
              class="d-flex align-center justify-space-between mt-1"
            >
              <span class="text-caption text-truncate mr-2">{{ pNode.name }}</span>
              <v-btn size="x-small" variant="tonal" color="warning" @click="globalViewModel.openInspector(pNode.id)">
                Edytuj Prompt
              </v-btn>
            </div>
          </div>
          <div v-else class="d-flex align-center justify-space-between mt-1">
            <span class="text-caption text-grey">Brak podłączonego węzła Prompt</span>
            <v-btn size="x-small" variant="tonal" color="warning" prepend-icon="mdi-plus" @click="attachSubNodeToAgent(NodeType.PROMPT)">
              Podłącz Prompt
            </v-btn>
          </div>
        </v-card>

        <!-- Port 3: MCP Tool -->
        <v-card variant="outlined" class="pa-2.5 mb-2.5 port-card port-card-tool">
          <div class="d-flex align-center justify-space-between mb-1">
            <div class="d-flex align-center">
              <v-icon icon="mdi-connection" size="16" color="success" class="mr-1.5" />
              <span class="text-caption font-weight-bold">Port [tool] — MCP Tools</span>
            </div>
            <v-chip size="x-small" :color="agentPortConnections.tools.length ? 'success' : 'grey'" variant="tonal">
              {{ agentPortConnections.tools.length }} ACTIVE
            </v-chip>
          </div>
          <div v-if="agentPortConnections.tools.length > 0">
            <div
              v-for="tNode in agentPortConnections.tools"
              :key="tNode.id"
              class="d-flex align-center justify-space-between mt-1"
            >
              <span class="text-caption text-truncate mr-2">
                {{ tNode.name }} ({{ tNode.mcpConfig?.enabledTools?.length || 'All' }} tools)
              </span>
              <v-btn size="x-small" variant="tonal" color="success" @click="globalViewModel.openInspector(tNode.id)">
                Konfiguruj MCP
              </v-btn>
            </div>
          </div>
          <div v-else class="d-flex align-center justify-space-between mt-1">
            <span class="text-caption text-grey">Brak podłączonego węzła MCP</span>
            <v-btn size="x-small" variant="tonal" color="success" prepend-icon="mdi-plus" @click="attachSubNodeToAgent(NodeType.MCP_CONNECTOR)">
              Podłącz MCP Tool
            </v-btn>
          </div>
        </v-card>

        <!-- Port 4: Agent Skill (SKILL.md) -->
        <v-card variant="outlined" class="pa-2.5 mb-4 port-card port-card-skill">
          <div class="d-flex align-center justify-space-between mb-1">
            <div class="d-flex align-center">
              <v-icon icon="mdi-book-open-page-variant-outline" size="16" color="cyan" class="mr-1.5" />
              <span class="text-caption font-weight-bold">Port [skill] — Agent Skills (SKILL.md)</span>
            </div>
            <v-chip size="x-small" :color="agentPortConnections.skills.length ? 'cyan' : 'grey'" variant="tonal">
              {{ agentPortConnections.skills.length }} ACTIVE
            </v-chip>
          </div>
          <div v-if="agentPortConnections.skills.length > 0">
            <div
              v-for="sNode in agentPortConnections.skills"
              :key="sNode.id"
              class="d-flex align-center justify-space-between mt-1"
            >
              <span class="text-caption text-truncate mr-2 font-monospace">
                {{ sNode.name }} ({{ sNode.endpoint ? `${sNode.endpoint}/SKILL.md` : 'brak' }})
              </span>
              <v-btn size="x-small" variant="tonal" color="cyan" @click="globalViewModel.openInspector(sNode.id)">
                Konfiguruj Skill
              </v-btn>
            </div>
          </div>
          <div v-else class="d-flex align-center justify-space-between mt-1">
            <span class="text-caption text-grey">Brak podłączonego węzła Skill</span>
            <v-btn size="x-small" variant="tonal" color="cyan" prepend-icon="mdi-plus" @click="attachSubNodeToAgent(NodeType.SKILL)">
              Podłącz Skill
            </v-btn>
          </div>
        </v-card>
      </template>

      <!-- Agent Skill Node Inspector (Microsoft SKILL.md Convention) -->
      <template v-if="localNode?.type === NodeType.SKILL">
        <v-alert
          type="info"
          variant="tonal"
          density="compact"
          color="cyan"
          class="text-caption mb-3"
        >
          Ten węzeł podpina <strong>pojedynczy skill</strong> zgodny z konwencją <code>Microsoft.Agents.AI</code> (folder <code>skills/&lt;nazwa&gt;/SKILL.md</code>) do portu <code>[skill]</code> wybranego Agenta AI.
        </v-alert>

        <v-select
          :model-value="localNode.endpoint"
          :items="globalViewModel.skills"
          item-title="name"
          item-value="name"
          label="Wybierz Skill (folder z plikiem SKILL.md)"
          variant="outlined"
          density="compact"
          prepend-inner-icon="mdi-folder-outline"
          class="mb-3 font-monospace"
          @update:model-value="handleSelectSkillForNode"
        >
          <template v-slot:item="{ props: itemProps, item }">
            <v-list-item v-bind="itemProps" :subtitle="((item as any).raw ?? item).description">
              <template v-slot:title>
                <span class="font-monospace font-weight-bold">{{ ((item as any).raw ?? item).name }}/SKILL.md</span>
              </template>
            </v-list-item>
          </template>
        </v-select>

        <v-text-field
          v-model="localNode.endpoint"
          label="Folder Skilla (skills/<folder>/SKILL.md)"
          placeholder="np. code-review"
          variant="outlined"
          density="compact"
          class="mb-3 font-monospace"
          @input="isDirty = true"
        />

        <v-textarea
          v-model="localNode.systemPrompt"
          label="Opis / adnotacja skilla na węźle"
          rows="2"
          auto-grow
          variant="outlined"
          density="compact"
          class="mb-3"
          @input="isDirty = true"
        />

        <v-card v-if="selectedSkillDetails" variant="outlined" color="cyan" class="pa-2.5 mb-4 bg-grey-darken-4">
          <div class="d-flex align-center justify-space-between mb-1">
            <span class="text-caption font-weight-bold text-cyan-lighten-2 font-monospace">
              {{ selectedSkillDetails.path || `skills/${selectedSkillDetails.name}/SKILL.md` }}
            </span>
            <v-chip size="x-small" color="cyan" variant="tonal">SKILL.md</v-chip>
          </div>
          <div class="text-caption text-grey-lighten-1 mb-2">
            {{ selectedSkillDetails.description }}
          </div>
          <pre class="pa-2 rounded bg-black text-caption font-monospace text-grey-lighten-2 overflow-x-auto" style="max-height: 180px; white-space: pre-wrap;">{{ selectedSkillDetails.content }}</pre>
        </v-card>
      </template>

      <!-- Sticky Note Inspector -->
      <template v-if="localNode?.type === NodeType.NOTE">
        <v-textarea
          v-model="localNode.systemPrompt"
          label="Note Content"
          placeholder="Write workflow notes, architectural guidelines, or markdown comments..."
          variant="outlined"
          density="compact"
          rows="6"
          hint="Supports multiline text and documentation."
          persistent-hint
          class="mb-3"
          @input="isDirty = true"
        />

        <v-select
          v-model="localNode.endpoint"
          :items="noteColorOptions"
          item-title="title"
          item-value="value"
          label="Sticky Note Color"
          variant="outlined"
          density="compact"
          class="mb-3"
          @update:model-value="isDirty = true"
        />
      </template>

      <!-- Section Label Inspector -->
      <template v-if="localNode?.type === NodeType.LABEL">
        <v-text-field
          v-model="localNode.systemPrompt"
          label="Subtitle / Description (Optional)"
          placeholder="e.g. Primary processing workflow"
          variant="outlined"
          density="compact"
          class="mb-3"
          @input="isDirty = true"
        />

        <v-row density="compact" class="mb-2">
          <v-col cols="6">
            <v-select
              v-model="localNode.endpoint"
              :items="labelSizeOptions"
              item-title="title"
              item-value="value"
              label="Font Size"
              variant="outlined"
              density="compact"
              @update:model-value="isDirty = true"
            />
          </v-col>
          <v-col cols="6">
            <v-select
              v-model="localNode.modelConfig!.provider"
              :items="labelColorOptions"
              item-title="title"
              item-value="value"
              label="Accent Color"
              variant="outlined"
              density="compact"
              @update:model-value="isDirty = true"
            />
          </v-col>
        </v-row>
      </template>

      <!-- System Prompt Node Inspector ONLY -->
      <template v-if="localNode?.type === NodeType.PROMPT">
        <v-textarea
          v-model="localNode.systemPrompt"
          label="System Prompt / Instructions"
          placeholder="You are an autonomous AI Agent with specialized reasoning..."
          variant="outlined"
          density="compact"
          rows="6"
          hint="Operational guidelines, persona role, and reasoning prompts."
          persistent-hint
          class="mb-2 font-monospace text-body-2"
          @input="isDirty = true"
        />

        <div class="mb-3">
          <div class="text-caption text-grey mb-1">Insert Variable:</div>
          <div class="d-flex flex-wrap gap-1">
            <v-chip
              v-for="v in promptVariables"
              :key="v"
              size="x-small"
              variant="tonal"
              color="warning"
              class="cursor-pointer font-monospace"
              @click="insertPromptVariable(v)"
            >
              {{ v }}
            </v-chip>
          </div>
        </div>
      </template>

      <!-- LLM Model Node Inspector ONLY -->
      <template v-if="localNode?.type === NodeType.MODEL">
        <v-select
          v-model="localNode.modelConfig!.provider"
          :items="providers"
          label="LLM Provider"
          variant="outlined"
          density="compact"
          class="mb-3"
          @update:model-value="handleProviderChange"
        />

        <v-text-field
          v-model="localNode.modelConfig!.modelName"
          label="Model Name"
          placeholder="e.g. gemma3:12b, gemini-2.5-flash-lite, gpt-4o"
          variant="outlined"
          density="compact"
          class="mb-2"
          @input="isDirty = true"
        />

        <!-- Quick Gemini Model Selector -->
        <div v-if="localNode.modelConfig?.provider === 'Gemini'" class="mb-3">
          <div class="text-caption text-grey mb-1">Dostępne modele Google Gemini API (kliknij, aby wybrać):</div>
          <div class="d-flex flex-column gap-1">
            <v-chip
              v-for="gm in geminiModels"
              :key="gm.name"
              size="x-small"
              variant="tonal"
              :color="localNode.modelConfig.modelName === gm.name ? 'info' : 'grey'"
              class="cursor-pointer justify-start"
              @click="localNode.modelConfig.modelName = gm.name; isDirty = true"
            >
              {{ gm.label }}
            </v-chip>
          </div>
        </div>

        <v-text-field
          v-if="localNode.modelConfig?.provider !== 'Ollama'"
          v-model="localNode.mcpConfig!.authToken"
          label="API Key (Gemini / OpenAI - Optional)"
          placeholder="Paste Gemini or OpenAI API key (overrides server env)"
          type="password"
          variant="outlined"
          density="compact"
          class="mb-3"
          @input="isDirty = true"
        />

        <v-text-field
          v-model="localNode.endpoint"
          label="Custom Endpoint URL (Optional)"
          placeholder="e.g. http://localhost:11434 or https://generativelanguage.googleapis.com/v1beta/openai/"
          variant="outlined"
          density="compact"
          class="mb-3"
          @input="isDirty = true"
        />

        <div class="mb-4">
          <div class="d-flex justify-space-between text-caption text-grey mb-1">
            <span>Sampling Temperature</span>
            <span class="font-weight-bold text-white">{{ localNode.modelConfig?.temperature }}</span>
          </div>
          <v-slider
            v-model="localNode.modelConfig!.temperature"
            min="0"
            max="2"
            step="0.05"
            thumb-label
            color="primary"
            @update:model-value="isDirty = true"
          />
        </div>
      </template>

      <!-- MCP Connector Node Inspector ONLY -->
      <template v-if="localNode?.type === NodeType.MCP_CONNECTOR">
        <v-text-field
          v-model="localNode!.mcpConfig!.serverUrl"
          label="MCP Server URL"
          placeholder="e.g. builtin://tools or http://localhost:8000/mcp"
          variant="outlined"
          density="compact"
          class="mb-3"
          @input="isDirty = true"
        />

        <v-text-field
          v-model="localNode!.mcpConfig!.authToken"
          label="MCP Auth Token (Optional)"
          type="password"
          variant="outlined"
          density="compact"
          class="mb-3"
          @input="isDirty = true"
        />

        <v-text-field
          v-model="enabledToolsInput"
          label="Enabled Tools (comma-separated)"
          placeholder="e.g. calculate_expression, get_system_info, get_current_time"
          variant="outlined"
          density="compact"
          hint="Leave blank to grant all available MCP tools."
          persistent-hint
          class="mb-2"
          @input="isDirty = true"
        />

        <!-- Quick MCP tool chips -->
        <div class="mb-4">
          <div class="text-caption text-grey mb-1">Quick Tools (click to toggle):</div>
          <div class="d-flex flex-wrap gap-1">
            <v-chip
              v-for="tool in commonMcpTools"
              :key="tool"
              size="x-small"
              variant="tonal"
              :color="enabledToolsInput.includes(tool) ? 'success' : 'grey'"
              class="cursor-pointer"
              @click="toggleTool(tool)"
            >
              {{ tool }}
            </v-chip>
          </div>
        </div>
      </template>

      <!-- Position Info -->
      <div class="text-caption text-grey mb-4 d-flex align-center justify-space-between">
        <span>Position: ({{ Math.round(localNode?.position?.x ?? 0) }}, {{ Math.round(localNode?.position?.y ?? 0) }})</span>
        <span>Flow: {{ localNode?.flowId || 'default-flow' }}</span>
      </div>

      <!-- Action Footer -->
      <div class="d-flex align-center gap-2 pt-2 border-top-subtle">
        <v-btn
          color="primary"
          variant="flat"
          size="small"
          prepend-icon="mdi-content-save-outline"
          class="flex-grow-1"
          @click="handleSave"
        >
          Save Changes
        </v-btn>

        <v-btn
          color="error"
          variant="tonal"
          size="small"
          icon="mdi-delete-outline"
          title="Delete Node"
          @click="handleDelete"
        />
      </div>
    </div>

    <!-- Empty State -->
    <div v-else class="empty-inspector-state pa-6 d-flex flex-column align-center justify-center fill-height text-center">
      <v-avatar size="56" color="rgba(255, 255, 255, 0.05)" class="mb-3">
        <v-icon icon="mdi-cursor-default-click-outline" size="28" color="grey" />
      </v-avatar>
      <div class="text-subtitle-2 font-weight-bold text-grey-lighten-1 mb-1">
        No Node Selected
      </div>
      <p class="text-caption text-grey">
        Click on any agent, model, prompt, connector, note, or label on the canvas to inspect and configure its parameters.
      </p>
    </div>
  </v-navigation-drawer>
</template>

<style scoped>
.app-inspector-drawer {
  background-color: #0b1220 !important;
  border-left: 1px solid rgba(255, 255, 255, 0.08) !important;
  color: #E2E8F0;
  z-index: 20;
}

.border-bottom-subtle {
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.border-top-subtle {
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}

.inspector-scroll-area {
  overflow-y: auto;
  height: calc(100% - 48px);
}

.cursor-pointer {
  cursor: pointer;
}

.port-card {
  background-color: rgba(17, 24, 39, 0.7);
}

.port-card-model {
  border-color: rgba(33, 150, 243, 0.35) !important;
}

.port-card-prompt {
  border-color: rgba(251, 140, 0, 0.35) !important;
}

.port-card-tool {
  border-color: rgba(76, 175, 80, 0.35) !important;
}

.port-card-skill {
  border-color: rgba(6, 182, 212, 0.35) !important;
}

.empty-inspector-state {
  height: calc(100% - 48px);
}
</style>
