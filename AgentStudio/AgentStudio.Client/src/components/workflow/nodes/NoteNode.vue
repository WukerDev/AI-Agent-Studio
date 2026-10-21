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

    <div class="node-header d-flex align-center justify-space-between px-2 py-1">
      <div class="d-flex align-center overflow-hidden mr-1">
        <v-icon icon="mdi-note-text-outline" size="14" class="mr-1.5 flex-shrink-0" color="white" />
        <span class="node-title text-truncate">{{ props.data.name || 'Sticky Note' }}</span>
      </div>
      <div class="d-flex align-center flex-shrink-0">
        <v-btn
          icon="mdi-cog"
          size="20"
          variant="text"
          color="white"
          class="node-action-btn"
          title="Configure Note"
          @click.stop="emit('configure', props.data)"
        />
        <v-btn
          icon="mdi-delete"
          size="20"
          variant="text"
          color="white"
          class="node-action-btn"
          title="Delete Note"
          @click.stop="emit('delete', props.id)"
        />
      </div>
    </div>

    <div class="node-body px-2 py-2" @dblclick.stop="emit('configure', props.data)">
      <div class="note-content">
        {{ props.data.systemPrompt || 'Add workflow note or documentation here...' }}
      </div>
      <div class="d-flex align-center justify-space-between mt-2 pt-1 border-top-subtle">
        <span class="badge-tag">NOTE</span>
        <v-icon icon="mdi-pin-outline" size="11" color="grey" />
      </div>
    </div>

    <Handle type="source" :position="Position.Bottom" class="node-handle" />
  </div>
</template>

<style scoped>
.custom-node {
  width: 190px;
  min-height: 110px;
  background-color: #1a1714;
  border-radius: 6px;
  border: 1px solid rgba(245, 158, 11, 0.35);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.45);
  font-family: inherit;
  transition: border-color 0.2s, box-shadow 0.2s;
  user-select: none;
  display: flex;
  flex-direction: column;
}

.custom-node.selected {
  border-color: var(--note-accent, #F59E0B) !important;
  box-shadow: 0 0 0 2px rgba(245, 158, 11, 0.5) !important;
}

.note-node .node-header {
  background-color: var(--note-accent, #F59E0B);
  border-top-left-radius: 5px;
  border-top-right-radius: 5px;
  height: 26px;
}

.node-title {
  font-size: 11px;
  font-weight: 700;
  color: #FFFFFF;
}

.node-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.note-content {
  font-size: 11px;
  line-height: 1.4;
  color: #FDE68A;
  background-color: rgba(0, 0, 0, 0.25);
  padding: 6px 8px;
  border-radius: 4px;
  white-space: pre-wrap;
  word-break: break-word;
  max-height: 150px;
  overflow-y: auto;
}

.border-top-subtle {
  border-top: 1px dashed rgba(245, 158, 11, 0.2);
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

:deep(.vue-flow__handle) {
  width: 8px;
  height: 8px;
  background: var(--note-accent, #F59E0B);
  border: 1px solid #FFFFFF;
}
</style>
