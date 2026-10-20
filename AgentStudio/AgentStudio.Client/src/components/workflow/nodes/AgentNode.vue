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
  <div class="custom-node agent-node" :class="{ selected: props.selected }">
    <Handle type="target" :position="Position.Top" class="node-handle" />

    <div class="node-header d-flex align-center justify-space-between px-3 py-2">
      <div class="d-flex align-center">
        <v-icon icon="mdi-robot" size="small" class="mr-2" color="white" />
        <span class="text-caption font-weight-bold text-white">{{ props.data.name || 'AI Agent' }}</span>
      </div>
      <div class="d-flex align-center">
        <v-btn icon="mdi-cog" size="x-small" variant="text" color="white" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete" size="x-small" variant="text" color="white" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <div class="node-body pa-3">
      <div class="text-caption text-grey-lighten-1 mb-1">
        {{ props.data.systemPrompt ? props.data.systemPrompt.slice(0, 50) + '...' : 'Autonomous AI agent execution node' }}
      </div>
      <div class="d-flex gap-1 mt-2">
        <v-chip size="x-small" color="primary" variant="flat">Agent Core</v-chip>
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
  border-color: #E63946;
  box-shadow: 0 0 0 2px rgba(230, 57, 70, 0.4);
}

.agent-node .node-header {
  background-color: #E63946;
  border-top-left-radius: 7px;
  border-top-right-radius: 7px;
}

.node-handle {
  width: 10px;
  height: 10px;
  background-color: #E63946;
  border: 2px solid #FFFFFF;
}
</style>
