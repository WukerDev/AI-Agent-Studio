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
    <Handle id="prompt-out" type="source" :position="Position.Top" class="node-handle" title="Podłącz do portu 'Prompt' w węźle AI Agent" />

    <div class="node-header d-flex align-center justify-space-between px-2.5">
      <div class="d-flex align-center ga-2 overflow-hidden mr-1">
        <div class="node-icon-badge flex-shrink-0">
          <v-icon icon="mdi-script-text-outline" size="12" color="#FBBF24" />
        </div>
        <span class="node-title text-truncate">{{ props.data.name || 'Prompt' }}</span>
      </div>
      <div class="node-actions d-flex align-center flex-shrink-0">
        <v-btn icon="mdi-cog-outline" size="18" variant="text" color="grey-lighten-1" class="node-action-btn" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete-outline" size="18" variant="text" color="grey-lighten-1" class="node-action-btn" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <div class="node-body px-2.5 py-2">
      <div class="prompt-box text-truncate">
        {{ props.data.systemPrompt || 'System instructions template' }}
      </div>
      <div class="d-flex align-center justify-space-between mt-1.5">
        <span class="badge-tag">PROMPT PORT</span>
        <v-icon icon="mdi-code-braces" size="11" color="#64748B" />
      </div>
    </div>

    <Handle id="prompt-bottom" type="source" :position="Position.Bottom" class="node-handle" />
  </div>
</template>

<style scoped>
.custom-node {
  width: 190px;
  background-color: #111827;
  border-radius: 7px;
  border: 1px solid #283548;
  border-top: 3px solid #F59E0B;
  box-shadow: 0 3px 10px rgba(0, 0, 0, 0.35);
  font-family: inherit;
  transition: border-color 0.15s ease;
  user-select: none;
}

.custom-node.selected {
  border-color: #F59E0B;
  border-top: 3px solid #F59E0B;
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
  background-color: #291D0E;
  border: 1px solid #B45309;
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

.prompt-box {
  font-size: 10px;
  color: #CBD5E1;
  background-color: #090D16;
  padding: 4px 6px;
  border-radius: 4px;
  border: 1px solid #1E293B;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

.badge-tag {
  font-size: 9px;
  font-weight: 600;
  color: #FCD34D;
  background-color: #291D0E;
  border: 1px solid #78350F;
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
  background-color: #F59E0B;
  border: 1.5px solid #F8FAFC;
}
</style>
