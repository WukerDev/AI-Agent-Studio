<script setup lang="ts">
import { Handle, Position } from '@vue-flow/core'

const props = defineProps<{
  id: string
  data: any
  selected?: boolean
}>()

const emit = defineEmits<{
  (e: 'configure', nodeData: any): void
  (e: 'delete', nodeId: string): void
}>()
</script>

<template>
  <div class="custom-node prompt-node" :class="{ selected: props.selected }">
    <Handle type="target" :position="Position.Top" class="node-handle" />

    <div class="node-header d-flex align-center justify-space-between px-3 py-2">
      <div class="d-flex align-center">
        <v-icon icon="mdi-script-text-outline" size="small" class="mr-2" color="white" />
        <span class="text-caption font-weight-bold text-white">{{ props.data.name || 'Prompt Template' }}</span>
      </div>
      <div class="d-flex align-center">
        <v-btn icon="mdi-cog" size="x-small" variant="text" color="white" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete" size="x-small" variant="text" color="white" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <div class="node-body pa-3">
      <div class="prompt-preview pa-2 rounded text-caption font-monospace text-grey-lighten-2">
        {{ props.data.systemPrompt || 'No instructions defined. Click settings to configure system prompt.' }}
      </div>
    </div>

    <Handle type="source" :position="Position.Bottom" class="node-handle" />
  </div>
</template>

<style scoped>
.custom-node {
  min-width: 220px;
  max-width: 280px;
  background-color: #1E293B;
  border-radius: 8px;
  border: 1px solid #334155;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
  font-family: inherit;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.custom-node.selected {
  border-color: #FB8C00;
  box-shadow: 0 0 0 2px rgba(251, 140, 0, 0.4);
}

.prompt-node .node-header {
  background-color: #FB8C00;
  border-top-left-radius: 7px;
  border-top-right-radius: 7px;
}

.prompt-preview {
  background-color: #0F172A;
  max-height: 80px;
  overflow: hidden;
  text-overflow: ellipsis;
  word-break: break-word;
}

.node-handle {
  width: 10px;
  height: 10px;
  background-color: #FB8C00;
  border: 2px solid #FFFFFF;
}
</style>
