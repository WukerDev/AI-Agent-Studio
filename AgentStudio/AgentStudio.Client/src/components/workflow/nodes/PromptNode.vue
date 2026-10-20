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

    <div class="node-header d-flex align-center justify-space-between px-2 py-1">
      <div class="d-flex align-center overflow-hidden mr-1">
        <v-icon icon="mdi-script-text-outline" size="14" class="mr-1.5 flex-shrink-0" color="white" />
        <span class="node-title text-truncate">{{ props.data.name || 'Prompt' }}</span>
      </div>
      <div class="d-flex align-center flex-shrink-0">
        <v-btn icon="mdi-cog" size="20" variant="text" color="white" class="node-action-btn" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete" size="20" variant="text" color="white" class="node-action-btn" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <div class="node-body px-2 py-1.5">
      <div class="prompt-box text-truncate">
        {{ props.data.systemPrompt || 'System instructions template' }}
      </div>
      <div class="d-flex align-center justify-space-between mt-1">
        <span class="badge-tag">INSTRUCTIONS</span>
        <v-icon icon="mdi-code-tags" size="10" color="grey" />
      </div>
    </div>

    <Handle type="source" :position="Position.Bottom" class="node-handle" />
  </div>
</template>

<style scoped>
.custom-node {
  width: 165px;
  background-color: #111827;
  border-radius: 6px;
  border: 1px solid #374151;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.4);
  font-family: inherit;
  transition: border-color 0.2s, box-shadow 0.2s;
  user-select: none;
}

.custom-node.selected {
  border-color: #FB8C00;
  box-shadow: 0 0 0 2px rgba(251, 140, 0, 0.4);
}

.prompt-node .node-header {
  background-color: #FB8C00;
  border-top-left-radius: 5px;
  border-top-right-radius: 5px;
  height: 26px;
}

.node-title {
  font-size: 11px;
  font-weight: 600;
  color: #FFFFFF;
}

.prompt-box {
  font-size: 10px;
  color: #D1D5DB;
  background-color: #030712;
  padding: 3px 5px;
  border-radius: 3px;
  border: 1px solid #1F2937;
  font-family: monospace;
}

.badge-tag {
  font-size: 9px;
  font-weight: 700;
  color: #FBBF24;
  background: rgba(245, 158, 11, 0.15);
  padding: 1px 4px;
  border-radius: 3px;
}

.node-action-btn {
  width: 18px !important;
  height: 18px !important;
  min-width: 18px !important;
  padding: 0 !important;
}

.node-handle {
  width: 7px;
  height: 7px;
  background-color: #FB8C00;
  border: 1.5px solid #FFFFFF;
}
</style>
