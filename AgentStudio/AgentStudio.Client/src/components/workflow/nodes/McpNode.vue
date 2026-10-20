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
  <div class="custom-node mcp-node" :class="{ selected: props.selected }">
    <Handle type="target" :position="Position.Top" class="node-handle" />

    <div class="node-header d-flex align-center justify-space-between px-2 py-1">
      <div class="d-flex align-center overflow-hidden mr-1">
        <v-icon icon="mdi-connection" size="14" class="mr-1.5 flex-shrink-0" color="white" />
        <span class="node-title text-truncate">{{ props.data.name || 'MCP Tools' }}</span>
      </div>
      <div class="d-flex align-center flex-shrink-0">
        <v-btn icon="mdi-cog" size="20" variant="text" color="white" class="node-action-btn" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete" size="20" variant="text" color="white" class="node-action-btn" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <div class="node-body px-2 py-1.5">
      <div class="d-flex align-center justify-space-between mb-1">
        <span class="label-text">Server</span>
        <span class="value-text text-truncate" style="max-width: 90px;">
          {{ props.data.mcpConfig?.serverUrl ? props.data.mcpConfig.serverUrl.replace(/^https?:\/\//, '') : 'None' }}
        </span>
      </div>
      <div class="d-flex align-center justify-space-between">
        <span class="label-text">Tools</span>
        <span class="badge-tag">{{ props.data.mcpConfig?.enabledTools?.length || 0 }} ACTIVE</span>
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
  border-color: #4CAF50;
  box-shadow: 0 0 0 2px rgba(76, 175, 80, 0.4);
}

.mcp-node .node-header {
  background-color: #4CAF50;
  border-top-left-radius: 5px;
  border-top-right-radius: 5px;
  height: 26px;
}

.node-title {
  font-size: 11px;
  font-weight: 600;
  color: #FFFFFF;
}

.label-text {
  font-size: 10px;
  color: #9CA3AF;
}

.value-text {
  font-size: 10px;
  font-weight: 500;
  color: #F3F4F6;
  font-family: monospace;
}

.badge-tag {
  font-size: 9px;
  font-weight: 700;
  color: #34D399;
  background: rgba(16, 185, 129, 0.15);
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
  background-color: #4CAF50;
  border: 1.5px solid #FFFFFF;
}
</style>
