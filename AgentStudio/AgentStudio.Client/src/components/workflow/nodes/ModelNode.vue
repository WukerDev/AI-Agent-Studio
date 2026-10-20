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
  <div class="custom-node model-node" :class="{ selected: props.selected }">
    <Handle type="target" :position="Position.Top" class="node-handle" />

    <div class="node-header d-flex align-center justify-space-between px-3 py-2">
      <div class="d-flex align-center">
        <v-icon icon="mdi-brain" size="small" class="mr-2" color="white" />
        <span class="text-caption font-weight-bold text-white">{{ props.data.name || 'Model Config' }}</span>
      </div>
      <div class="d-flex align-center">
        <v-btn icon="mdi-cog" size="x-small" variant="text" color="white" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete" size="x-small" variant="text" color="white" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <div class="node-body pa-3">
      <div class="d-flex align-center justify-space-between mb-1">
        <span class="text-caption text-grey">Provider:</span>
        <v-chip size="x-small" color="info" variant="flat">
          {{ props.data.modelConfig?.provider || 'OpenAI' }}
        </v-chip>
      </div>
      <div class="d-flex align-center justify-space-between mb-1">
        <span class="text-caption text-grey">Model:</span>
        <span class="text-caption font-weight-medium text-white">
          {{ props.data.modelConfig?.modelName || 'gpt-4o' }}
        </span>
      </div>
      <div class="d-flex align-center justify-space-between">
        <span class="text-caption text-grey">Temp:</span>
        <span class="text-caption text-white">
          {{ props.data.modelConfig?.temperature ?? 0.7 }}
        </span>
      </div>
    </div>

    <Handle type="source" :position="Position.Bottom" class="node-handle" />
  </div>
</template>

<style scoped>
.custom-node {
  min-width: 200px;
  max-width: 260px;
  background-color: #1E293B;
  border-radius: 8px;
  border: 1px solid #334155;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
  font-family: inherit;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.custom-node.selected {
  border-color: #2196F3;
  box-shadow: 0 0 0 2px rgba(33, 150, 243, 0.4);
}

.model-node .node-header {
  background-color: #2196F3;
  border-top-left-radius: 7px;
  border-top-right-radius: 7px;
}

.node-handle {
  width: 10px;
  height: 10px;
  background-color: #2196F3;
  border: 2px solid #FFFFFF;
}
</style>
