<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  id: string
  data: any
  selected?: boolean
}>()

const emit = defineEmits<{
  (e: 'configure', nodeData: any): void
  (e: 'delete', nodeId: string): void
}>()

const labelColor = computed(() => {
  return props.data?.modelConfig?.provider || '#8B5CF6'
})

const fontSize = computed(() => {
  const size = props.data?.endpoint || 'medium'
  switch (size) {
    case 'small':
      return '14px'
    case 'large':
      return '24px'
    case 'huge':
      return '32px'
    case 'medium':
    default:
      return '18px'
  }
})
</script>

<template>
  <div
    class="custom-node label-node"
    :class="{ selected: props.selected }"
    :style="{ '--label-accent': labelColor }"
    @dblclick.stop="emit('configure', props.data)"
  >
    <div class="d-flex align-center justify-space-between w-100">
      <div class="d-flex align-center overflow-hidden mr-2">
        <v-icon icon="mdi-format-title" size="16" class="mr-1.5 flex-shrink-0" :style="{ color: labelColor }" />
        <span class="label-title" :style="{ fontSize: fontSize }">
          {{ props.data.name || 'Section Label' }}
        </span>
      </div>

      <div class="actions-group d-flex align-center flex-shrink-0">
        <v-btn
          icon="mdi-pencil-outline"
          size="20"
          variant="text"
          color="grey-lighten-1"
          class="node-action-btn"
          title="Configure Label"
          @click.stop="emit('configure', props.data)"
        />
        <v-btn
          icon="mdi-delete"
          size="20"
          variant="text"
          color="grey-lighten-1"
          class="node-action-btn"
          title="Delete Label"
          @click.stop="emit('delete', props.id)"
        />
      </div>
    </div>

    <div v-if="props.data.systemPrompt" class="label-desc mt-1">
      {{ props.data.systemPrompt }}
    </div>
  </div>
</template>

<style scoped>
.custom-node {
  min-width: 180px;
  max-width: 380px;
  background-color: rgba(17, 24, 39, 0.85);
  backdrop-filter: blur(8px);
  border-radius: 8px;
  border: 1.5px dashed rgba(139, 92, 246, 0.4);
  padding: 8px 12px;
  box-shadow: 0 4px 14px rgba(0, 0, 0, 0.35);
  font-family: inherit;
  transition: all 0.2s ease;
  user-select: none;
}

.custom-node.selected {
  border: 1.5px solid var(--label-accent, #8B5CF6) !important;
  box-shadow: 0 0 0 2px rgba(139, 92, 246, 0.4) !important;
  background-color: rgba(30, 41, 59, 0.95);
}

.label-title {
  font-weight: 700;
  color: #F8FAFC;
  letter-spacing: -0.01em;
  line-height: 1.2;
  word-break: break-word;
}

.label-desc {
  font-size: 11px;
  color: #94A3B8;
  line-height: 1.35;
}

.actions-group {
  opacity: 0.6;
  transition: opacity 0.15s ease;
}

.custom-node:hover .actions-group,
.custom-node.selected .actions-group {
  opacity: 1;
}

.node-action-btn {
  width: 20px !important;
  height: 20px !important;
  min-width: 20px !important;
  padding: 0 !important;
}
</style>
