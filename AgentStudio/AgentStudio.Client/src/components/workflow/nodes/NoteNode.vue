<script setup lang="ts">
import { computed } from 'vue'
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

const noteColor = computed(() => {
  return props.data?.endpoint || '#F59E0B'
})
</script>

<template>
  <div
    class="custom-node note-node"
    :class="{ selected: props.selected }"
    :style="{ '--note-accent': noteColor }"
  >
    <Handle type="target" :position="Position.Top" class="node-handle" />

    <div class="node-header d-flex align-center justify-space-between px-2.5">
      <div class="d-flex align-center ga-2 overflow-hidden mr-1">
        <v-icon icon="mdi-note-text-outline" size="14" class="flex-shrink-0" :style="{ color: noteColor }" />
        <span class="node-title text-truncate">{{ props.data.name || 'Sticky Note' }}</span>
      </div>
      <div class="node-actions d-flex align-center flex-shrink-0">
        <v-btn
          icon="mdi-cog-outline"
          size="18"
          variant="text"
          color="grey-lighten-1"
          class="node-action-btn"
          title="Configure Note"
          @click.stop="emit('configure', props.data)"
        />
        <v-btn
          icon="mdi-delete-outline"
          size="18"
          variant="text"
          color="grey-lighten-1"
          class="node-action-btn"
          title="Delete Note"
          @click.stop="emit('delete', props.id)"
        />
      </div>
    </div>

    <div class="node-body px-2.5 py-2" @dblclick.stop="emit('configure', props.data)">
      <div class="note-content">
        {{ props.data.systemPrompt || 'Add workflow note or documentation here...' }}
      </div>
      <div class="d-flex align-center justify-space-between mt-2 pt-1.5 border-top-subtle">
        <span class="badge-tag">NOTE</span>
        <v-icon icon="mdi-pin-outline" size="11" color="#64748B" />
      </div>
    </div>

    <Handle type="source" :position="Position.Bottom" class="node-handle" />
  </div>
</template>

<style scoped>
.custom-node {
  width: 215px;
  min-height: 110px;
  background-color: #131824;
  border-radius: 7px;
  border: 1px solid #283548;
  border-top: 3px solid var(--note-accent, #F59E0B);
  box-shadow: 0 3px 10px rgba(0, 0, 0, 0.35);
  font-family: inherit;
  transition: border-color 0.15s ease;
  user-select: none;
  display: flex;
  flex-direction: column;
}

.custom-node.selected {
  border-color: var(--note-accent, #F59E0B) !important;
  border-top: 3px solid var(--note-accent, #F59E0B) !important;
}

.node-header {
  background-color: #172030;
  border-bottom: 1px solid #1E293B;
  height: 30px;
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

.node-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.note-content {
  font-size: 11px;
  line-height: 1.45;
  color: #E2E8F0;
  background-color: #0D131F;
  border: 1px solid #1E293B;
  padding: 7px 8px;
  border-radius: 5px;
  white-space: pre-wrap;
  word-break: break-word;
  max-height: 165px;
  overflow-y: auto;
}

.border-top-subtle {
  border-top: 1px solid #1E293B;
}

.badge-tag {
  font-size: 9px;
  font-weight: 600;
  color: #94A3B8;
  background-color: #0D131F;
  border: 1px solid #24324B;
  padding: 1px 5px;
  border-radius: 3px;
}

.node-action-btn {
  width: 18px !important;
  height: 18px !important;
  min-width: 18px !important;
  padding: 0 !important;
}

:deep(.vue-flow__handle) {
  width: 8px;
  height: 8px;
  background: var(--note-accent, #F59E0B);
  border: 1.5px solid #F8FAFC;
}
</style>
