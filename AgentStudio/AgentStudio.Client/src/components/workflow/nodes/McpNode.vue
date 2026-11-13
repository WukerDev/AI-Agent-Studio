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
    <Handle id="tool-out" type="source" :position="Position.Top" class="node-handle" title="Podłącz do portu 'Tool' w węźle AI Agent" />

    <div class="node-header d-flex align-center justify-space-between px-2.5">
      <div class="d-flex align-center ga-2 overflow-hidden mr-1">
        <div class="node-icon-badge flex-shrink-0">
          <v-icon icon="mdi-connection" size="12" color="#34D399" />
        </div>
        <span class="node-title text-truncate">{{ props.data.name || 'MCP Tools' }}</span>
      </div>
      <div class="node-actions d-flex align-center flex-shrink-0">
        <v-btn icon="mdi-cog-outline" size="18" variant="text" color="grey-lighten-1" class="node-action-btn" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete-outline" size="18" variant="text" color="grey-lighten-1" class="node-action-btn" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <div class="node-body px-2.5 py-2">
      <div class="d-flex align-center justify-space-between mb-1">
        <span class="label-text">Server</span>
        <span class="value-text text-truncate" style="max-width: 115px;">
          {{
            !props.data.mcpConfig?.serverUrl
              ? 'Built-in MCP'
              : props.data.mcpConfig.serverUrl.startsWith('builtin://')
                ? 'Built-in Tools'
                : props.data.mcpConfig.serverUrl.replace(/^https?:\/\//, '')
          }}
        </span>
      </div>
      <div class="d-flex align-center justify-space-between">
        <span class="label-text">Tools</span>
        <span class="badge-tag">
          {{ props.data.mcpConfig?.enabledTools?.length ? `${props.data.mcpConfig.enabledTools.length} ACTIVE` : 'ALL ACTIVE' }}
        </span>
      </div>
    </div>

    <Handle id="tool-bottom" type="source" :position="Position.Bottom" class="node-handle" />
  </div>
</template>

<style scoped>
.custom-node {
  width: 190px;
  background-color: #111827;
  border-radius: 7px;
  border: 1px solid #283548;
  border-top: 3px solid #10B981;
  box-shadow: 0 3px 10px rgba(0, 0, 0, 0.35);
  font-family: inherit;
  transition: border-color 0.15s ease;
  user-select: none;
}

.custom-node.selected {
  border-color: #10B981;
  border-top: 3px solid #10B981;
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
  background-color: #0D281E;
  border: 1px solid #15803D;
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
  font-weight: 500;
  color: #E2E8F0;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

.badge-tag {
  font-size: 9px;
  font-weight: 600;
  color: #6EE7B7;
  background-color: #0D281E;
  border: 1px solid #14532D;
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
  background-color: #10B981;
  border: 1.5px solid #F8FAFC;
}
</style>
