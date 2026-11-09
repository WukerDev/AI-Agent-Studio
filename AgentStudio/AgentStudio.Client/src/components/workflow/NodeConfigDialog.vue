<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { NodeType } from '../../clients/generated/agent_node'

const props = defineProps<{
  modelValue: boolean
  nodeData: any | null
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'save', updatedData: any): void
}>()

const localNode = ref<any>({})
const enabledToolsInput = ref('')

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

const dialogTitle = computed(() => {
  if (!localNode.value) return 'Configure Node'
  switch (localNode.value.type) {
    case NodeType.NOTE:
      return 'Configure Sticky Note'
    case NodeType.LABEL:
      return 'Configure Canvas Label'
    case NodeType.AGENT:
      return 'Configure AI Agent Hub'
    case NodeType.MODEL:
      return 'Configure LLM Model'
    case NodeType.PROMPT:
      return 'Configure System Prompt'
    case NodeType.MCP_CONNECTOR:
      return 'Configure MCP Connector'
    default:
      return 'Configure Node'
  }
})

const nameLabel = computed(() => {
  if (localNode.value?.type === NodeType.LABEL) return 'Label Text'
  if (localNode.value?.type === NodeType.NOTE) return 'Note Title'
  return 'Node Name'
})

watch(() => props.nodeData, (val) => {
  if (val) {
    localNode.value = JSON.parse(JSON.stringify(val))
    if (!localNode.value.modelConfig) {
      localNode.value.modelConfig = { provider: 'Ollama', modelName: 'gemma3:12b', temperature: 0.7 }
    } else if (localNode.value.type === NodeType.MODEL) {
      if (!localNode.value.modelConfig.provider) {
        localNode.value.modelConfig.provider = 'Ollama'
      }
      if (!localNode.value.modelConfig.modelName) {
        localNode.value.modelConfig.modelName =
          localNode.value.modelConfig.provider === 'Ollama'
            ? 'gemma3:12b'
            : localNode.value.modelConfig.provider === 'Gemini'
              ? 'gemini-3.5-flash-lite'
              : 'gpt-4o'
      }
    }
    if (!localNode.value.mcpConfig) {
      localNode.value.mcpConfig = {
        serverUrl: localNode.value.type === NodeType.MCP_CONNECTOR ? 'builtin://tools' : '',
        authToken: '',
        enabledTools: []
      }
    } else if (localNode.value.type === NodeType.MCP_CONNECTOR && !localNode.value.mcpConfig.serverUrl) {
      localNode.value.mcpConfig.serverUrl = 'builtin://tools'
    }
    if (localNode.value.type === NodeType.NOTE && !localNode.value.endpoint) {
      localNode.value.endpoint = '#F59E0B'
    }
    if (localNode.value.type === NodeType.LABEL) {
      if (!localNode.value.endpoint) localNode.value.endpoint = 'medium'
      if (!localNode.value.modelConfig?.provider) {
        localNode.value.modelConfig.provider = '#8B5CF6'
      }
    }
    enabledToolsInput.value = (localNode.value.mcpConfig?.enabledTools || []).join(', ')
  }
}, { immediate: true })

function handleProviderChange(provider: string) {
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

function handleSave() {
  if (localNode.value.type === NodeType.MCP_CONNECTOR && localNode.value.mcpConfig) {
    localNode.value.mcpConfig.enabledTools = enabledToolsInput.value
      .split(',')
      .map(s => s.trim())
      .filter(s => s.length > 0)
  }

  emit('save', localNode.value)
  emit('update:modelValue', false)
}
</script>

<template>
  <v-dialog
    :model-value="props.modelValue"
    max-width="560px"
    @update:model-value="(val) => emit('update:modelValue', val)"
  >
    <v-card v-if="localNode">
      <v-card-title class="d-flex align-center justify-space-between bg-surface pa-4">
        <span class="text-h6 font-weight-bold">{{ dialogTitle }}</span>
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
      </v-card-title>

      <v-divider />

      <v-card-text class="pa-4">
        <v-text-field
          v-model="localNode.name"
          :label="nameLabel"
          variant="outlined"
          density="compact"
          class="mb-3"
        />

        <!-- Agent Node Info (n8n-style ports) -->
        <template v-if="localNode.type === NodeType.AGENT">
          <v-alert type="info" variant="tonal" density="compact" class="text-caption mb-2">
            Węzeł <strong>AI Agent</strong> posiada dedykowane porty na dole karty: <strong>Model</strong>, <strong>Prompt</strong> oraz <strong>Tool</strong>. Podłącz do nich odpowiednie węzły na płótnie.
          </v-alert>
        </template>

        <!-- Note Node Inspector -->
        <template v-if="localNode.type === NodeType.NOTE">
          <v-textarea
            v-model="localNode.systemPrompt"
            label="Note Content"
            placeholder="Write workflow notes, architecture thoughts, or markdown comments..."
            variant="outlined"
            density="compact"
            rows="5"
            hint="Supports multiline text and documentation."
            persistent-hint
            class="mb-3"
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
          />
        </template>

        <!-- Label Node Inspector -->
        <template v-if="localNode.type === NodeType.LABEL">
          <v-text-field
            v-model="localNode.systemPrompt"
            label="Subtitle / Description (Optional)"
            placeholder="e.g. Primary processing workflow"
            variant="outlined"
            density="compact"
            class="mb-3"
          />

          <v-row density="compact">
            <v-col cols="6">
              <v-select
                v-model="localNode.endpoint"
                :items="labelSizeOptions"
                item-title="title"
                item-value="value"
                label="Font Size"
                variant="outlined"
                density="compact"
              />
            </v-col>
            <v-col cols="6">
              <v-select
                v-model="localNode.modelConfig.provider"
                :items="labelColorOptions"
                item-title="title"
                item-value="value"
                label="Accent Color"
                variant="outlined"
                density="compact"
              />
            </v-col>
          </v-row>
        </template>

        <!-- Prompt Node Inspector ONLY -->
        <template v-if="localNode.type === NodeType.PROMPT">
          <v-textarea
            v-model="localNode.systemPrompt"
            label="System Prompt / Instructions"
            variant="outlined"
            density="compact"
            rows="4"
            hint="Defines role, behavior guidelines, and operational instructions."
            persistent-hint
            class="mb-3 font-monospace text-body-2"
          />
        </template>

        <!-- Model Node Inspector ONLY -->
        <template v-if="localNode.type === NodeType.MODEL">
          <v-select
            v-model="localNode.modelConfig.provider"
            :items="providers"
            label="LLM Provider"
            variant="outlined"
            density="compact"
            class="mb-3"
            @update:model-value="handleProviderChange"
          />

          <v-text-field
            v-model="localNode.modelConfig.modelName"
            label="Model Name"
            placeholder="e.g. gemma3:12b, gemini-3.5-flash-lite, gpt-4o"
            variant="outlined"
            density="compact"
            class="mb-2"
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
                @click="localNode.modelConfig.modelName = gm.name"
              >
                {{ gm.label }}
              </v-chip>
            </div>
          </div>

          <v-text-field
            v-if="localNode.modelConfig?.provider !== 'Ollama'"
            v-model="localNode.mcpConfig.authToken"
            label="API Key (Gemini / OpenAI - Optional)"
            placeholder="Paste Gemini or OpenAI API key"
            type="password"
            variant="outlined"
            density="compact"
            class="mb-3"
          />

          <v-text-field
            v-model="localNode.endpoint"
            label="Custom API Endpoint (Optional)"
            placeholder="e.g. http://localhost:11434 or https://generativelanguage.googleapis.com/v1beta/openai/"
            variant="outlined"
            density="compact"
            class="mb-3"
          />

          <div class="mb-3">
            <div class="d-flex justify-space-between text-caption text-grey mb-1">
              <span>Temperature</span>
              <span>{{ localNode.modelConfig.temperature }}</span>
            </div>
            <v-slider
              v-model="localNode.modelConfig.temperature"
              min="0"
              max="2"
              step="0.05"
              thumb-label
              color="primary"
            />
          </div>
        </template>

        <!-- MCP Connector Node Inspector ONLY -->
        <template v-if="localNode.type === NodeType.MCP_CONNECTOR">
          <v-text-field
            v-model="localNode.mcpConfig.serverUrl"
            label="MCP Server URL"
            placeholder="e.g. builtin://tools or http://localhost:8000/mcp"
            variant="outlined"
            density="compact"
            class="mb-3"
          />

          <v-text-field
            v-model="localNode.mcpConfig.authToken"
            label="MCP Auth Token (Optional)"
            type="password"
            variant="outlined"
            density="compact"
            class="mb-3"
          />

          <v-text-field
            v-model="enabledToolsInput"
            label="Enabled Tools (comma-separated, leave blank for all)"
            placeholder="e.g. calculate_expression, get_system_info, get_current_time"
            variant="outlined"
            density="compact"
            class="mb-3"
          />
        </template>
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-4 bg-surface">
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" @click="handleSave">Save Changes</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
