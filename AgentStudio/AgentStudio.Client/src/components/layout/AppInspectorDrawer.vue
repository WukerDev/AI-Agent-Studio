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

const providers = ['OpenAI', 'Ollama', 'Custom']

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

const commonMcpTools = ['search_web', 'read_file', 'write_file', 'execute_query', 'fetch_page']

watch(() => globalViewModel.selectedNode, (selected) => {
  if (selected) {
    localNode.value = JSON.parse(JSON.stringify(selected))
    if (!localNode.value!.modelConfig) {
      localNode.value!.modelConfig = { provider: 'OpenAI', modelName: 'gpt-4o', temperature: 0.7 }
    }
    if (!localNode.value!.mcpConfig) {
      localNode.value!.mcpConfig = { serverUrl: 'http://localhost:8000/mcp', authToken: '', enabledTools: [] }
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

const inspectorTitle = computed(() => {
  if (!localNode.value) return 'Node Inspector'
  switch (localNode.value.type) {
    case NodeType.AGENT:
      return 'AI Agent Inspector'
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
    default:
      return 'Node Inspector'
  }
})

const nodeTypeBadge = computed(() => {
  if (!localNode.value) return 'UNKNOWN'
  switch (localNode.value.type) {
    case NodeType.AGENT:
      return 'AGENT'
    case NodeType.MODEL:
      return 'MODEL'
    case NodeType.PROMPT:
      return 'PROMPT'
    case NodeType.MCP_CONNECTOR:
      return 'MCP CONNECTOR'
    case NodeType.NOTE:
      return 'STICKY NOTE'
    case NodeType.LABEL:
      return 'LABEL'
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

  if (localNode.value.mcpConfig) {
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

      <!-- Prompt or Agent Inspector -->
      <template v-if="localNode?.type === NodeType.PROMPT || localNode?.type === NodeType.AGENT">
        <v-textarea
          v-model="localNode.systemPrompt"
          label="System Prompt / Instructions"
          placeholder="You are an autonomous AI Agent with specialized reasoning..."
          variant="outlined"
          density="compact"
          rows="5"
          hint="Operational guidelines, persona role, and reasoning prompts."
          persistent-hint
          class="mb-3 font-monospace text-body-2"
          @input="isDirty = true"
        />
      </template>

      <!-- Model or Agent Inspector -->
      <template v-if="localNode?.type === NodeType.MODEL || localNode?.type === NodeType.AGENT">
        <v-select
          v-model="localNode.modelConfig!.provider"
          :items="providers"
          label="LLM Provider"
          variant="outlined"
          density="compact"
          class="mb-3"
          @update:model-value="isDirty = true"
        />

        <v-text-field
          v-model="localNode.modelConfig!.modelName"
          label="Model Name"
          placeholder="e.g. gpt-4o, llama3.2, claude-3-5-sonnet"
          variant="outlined"
          density="compact"
          class="mb-3"
          @input="isDirty = true"
        />

        <v-text-field
          v-model="localNode.endpoint"
          label="Custom Endpoint URL (Optional)"
          placeholder="e.g. http://localhost:11434 or https://api.openai.com/v1"
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

      <!-- MCP Connector or Agent Inspector -->
      <template v-if="localNode?.type === NodeType.MCP_CONNECTOR || localNode?.type === NodeType.AGENT">
        <v-text-field
          v-model="localNode!.mcpConfig!.serverUrl"
          label="MCP Server URL"
          placeholder="e.g. http://localhost:8000/mcp or http://localhost:5000/sse"
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
          placeholder="e.g. search_web, read_file, execute_query"
          variant="outlined"
          density="compact"
          hint="Leave blank to grant all available MCP tools."
          persistent-hint
          class="mb-2"
          @input="isDirty = true"
        />

        <!-- Quick MCP tool chips -->
        <div class="mb-4">
          <div class="text-caption text-grey mb-1">Quick Tools:</div>
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

.empty-inspector-state {
  height: calc(100% - 48px);
}
</style>
