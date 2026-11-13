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
      return '22px'
    case 'huge':
      return '28px'
    case 'medium':
    default:
      return '17px'
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
      <div class="d-flex align-center ga-2 overflow-hidden mr-2">
        <v-icon icon="mdi-format-title" size="16" class="flex-shrink-0" :style="{ color: labelColor }" />
        <span class="label-title" :style="{ fontSize: fontSize }">
          {{ props.data.name || 'Section Label' }}
        </span>
      </div>

      <div class="actions-group d-flex align-center flex-shrink-0">
        <v-btn
          icon="mdi-pencil-outline"
          size="18"
          variant="text"
          color="grey-lighten-1"
          class="node-action-btn"
          title="Configure Label"
          @click.stop="emit('configure', props.data)"
        />
        <v-btn
          icon="mdi-delete-outline"
          size="18"
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
  min-width: 190px;
  max-width: 390px;
  background-color: #111827;
  border-radius: 7px;
  border: 1px solid #283548;
  border-left: 3px solid var(--label-accent, #8B5CF6);
  padding: 9px 12px;
  box-shadow: 0 3px 10px rgba(0, 0, 0, 0.35);
  font-family: inherit;
  transition: border-color 0.15s ease;
  user-select: none;
}

.custom-node.selected {
  border-color: var(--label-accent, #8B5CF6) !important;
  border-left: 3px solid var(--label-accent, #8B5CF6) !important;
  background-color: #151E2E;
}

.label-title {
  font-weight: 700;
  color: #F8FAFC;
  letter-spacing: -0.01em;
  line-height: 1.25;
  word-break: break-word;
}

.label-desc {
  font-size: 11px;
  color: #94A3B8;
  line-height: 1.4;
}

.actions-group {
  opacity: 0;
  transition: opacity 0.15s ease;
}

.custom-node:hover .actions-group,
.custom-node.selected .actions-group {
  opacity: 1;
}

.node-action-btn {
  width: 18px !important;
  height: 18px !important;
  min-width: 18px !important;
  padding: 0 !important;
}
</style>
