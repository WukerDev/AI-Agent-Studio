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

const dialogTitle = computed(() => {
  if (!localNode.value) return 'Configure Node'
  switch (localNode.value.type) {
    case NodeType.NOTE:
      return 'Configure Sticky Note'
    case NodeType.LABEL:
      return 'Configure Canvas Label'
    case NodeType.AGENT:
      return 'Configure AI Agent'
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
      localNode.value.modelConfig = { provider: 'OpenAI', modelName: 'gpt-4o', temperature: 0.7 }
    }
    if (!localNode.value.mcpConfig) {
      localNode.value.mcpConfig = { serverUrl: 'http://localhost:8000/mcp', authToken: '', enabledTools: [] }
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

function handleSave() {
  if (localNode.value.mcpConfig) {
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

        <!-- Prompt or Agent Node Inspector -->
        <template v-if="localNode.type === NodeType.PROMPT || localNode.type === NodeType.AGENT">
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

        <!-- Model or Agent Node Inspector -->
        <template v-if="localNode.type === NodeType.MODEL || localNode.type === NodeType.AGENT">
          <v-select
            v-model="localNode.modelConfig.provider"
            :items="providers"
            label="LLM Provider"
            variant="outlined"
            density="compact"
            class="mb-3"
          />

          <v-text-field
            v-model="localNode.modelConfig.modelName"
            label="Model Name"
            placeholder="e.g. gpt-4o, llama3.2, claude-3-5-sonnet"
            variant="outlined"
            density="compact"
            class="mb-3"
          />

          <v-text-field
            v-model="localNode.endpoint"
            label="Custom API Endpoint (Optional)"
            placeholder="e.g. http://localhost:11434 or https://api.openai.com/v1"
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

        <!-- MCP Connector or Agent Node Inspector -->
        <template v-if="localNode.type === NodeType.MCP_CONNECTOR || localNode.type === NodeType.AGENT">
          <v-text-field
            v-model="localNode.mcpConfig.serverUrl"
            label="MCP Server URL"
            placeholder="e.g. http://localhost:8000/mcp or http://localhost:5000/sse"
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
            placeholder="e.g. search_web, read_file, execute_query"
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
