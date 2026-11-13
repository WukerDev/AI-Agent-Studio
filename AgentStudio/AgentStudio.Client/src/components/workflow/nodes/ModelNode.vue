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
    <Handle id="model-out" type="source" :position="Position.Top" class="node-handle" title="Podłącz do portu 'Model' w węźle AI Agent" />

    <div class="node-header d-flex align-center justify-space-between px-2.5">
      <div class="d-flex align-center ga-2 overflow-hidden mr-1">
        <div class="node-icon-badge flex-shrink-0">
          <v-icon icon="mdi-brain" size="12" color="#60A5FA" />
        </div>
        <span class="node-title text-truncate">{{ props.data.name || 'LLM Model' }}</span>
      </div>
      <div class="node-actions d-flex align-center flex-shrink-0">
        <v-btn icon="mdi-cog-outline" size="18" variant="text" color="grey-lighten-1" class="node-action-btn" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete-outline" size="18" variant="text" color="grey-lighten-1" class="node-action-btn" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <div class="node-body px-2.5 py-2">
      <div class="d-flex align-center justify-space-between mb-1">
        <span class="label-text">Provider</span>
        <span class="badge-tag">{{ props.data.modelConfig?.provider || 'Ollama' }}</span>
      </div>
      <div class="d-flex align-center justify-space-between">
        <span class="label-text">Model</span>
        <span class="value-text text-truncate" style="max-width: 115px;">
          {{ props.data.modelConfig?.modelName || 'gemma3:12b' }}
        </span>
      </div>
    </div>

    <Handle id="model-bottom" type="source" :position="Position.Bottom" class="node-handle" />
  </div>
</template>

<style scoped>
.custom-node {
  width: 190px;
  background-color: #111827;
  border-radius: 7px;
  border: 1px solid #283548;
  border-top: 3px solid #3B82F6;
  box-shadow: 0 3px 10px rgba(0, 0, 0, 0.35);
  font-family: inherit;
  transition: border-color 0.15s ease;
  user-select: none;
}

.custom-node.selected {
  border-color: #3B82F6;
  border-top: 3px solid #3B82F6;
}

.node-header {
  background-color: #151E2E;
  border-bottom: 1px solid #1E293B;
  height: 30px;
}

.node-icon-badge {
  width: 18px;
  height: 18px;
  border-radius: 4px;
  background-color: #132542;
  border: 1px solid #1D4ED8;
  display: flex;
  align-items: center;
  justify-content: center;
}

.node-title {
  font-size: 11.5px;
  font-weight: 600;
  color: #F8FAFC;
}

.node-actions {
  opacity: 0;
  transition: opacity 0.12s ease;
}

.custom-node:hover .node-actions,
.custom-node.selected .node-actions {
  opacity: 1;
}

.label-text {
  font-size: 10px;
  color: #64748B;
}

.value-text {
  font-size: 10.5px;
  font-weight: 600;
  color: #E2E8F0;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

.badge-tag {
  font-size: 9.5px;
  font-weight: 600;
  color: #93C5FD;
  background-color: #132542;
  border: 1px solid #1E3A8A;
  padding: 1px 5px;
  border-radius: 3px;
}

.node-action-btn {
  width: 18px !important;
  height: 18px !important;
  min-width: 18px !important;
  padding: 0 !important;
}

.node-handle {
  width: 8px;
  height: 8px;
  background-color: #3B82F6;
  border: 1.5px solid #F8FAFC;
}
</style>
