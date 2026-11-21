<script setup lang="ts">
defineOptions({
  inheritAttrs: false
})

import { computed, ref } from 'vue'
import { BaseEdge, EdgeLabelRenderer, getBezierPath, type EdgeProps } from '@vue-flow/core'

const props = defineProps<EdgeProps>()
const emit = defineEmits<{
  (e: 'delete', edgeId: string): void
}>()

const isHovered = ref(false)
let hideTimer: ReturnType<typeof setTimeout> | null = null

function handleMouseEnter() {
  if (hideTimer) {
    clearTimeout(hideTimer)
    hideTimer = null
  }
  isHovered.value = true
}

function handleMouseLeave() {
  hideTimer = setTimeout(() => {
    isHovered.value = false
  }, 140)
}

const pathResult = computed(() =>
  getBezierPath({
    sourceX: props.sourceX,
    sourceY: props.sourceY,
    sourcePosition: props.sourcePosition,
    targetX: props.targetX,
    targetY: props.targetY,
    targetPosition: props.targetPosition
  })
)

const edgeStyle = computed(() => {
  const baseStyle = (props.style && typeof props.style === 'object' ? props.style : {}) as Record<string, any>
  const baseStroke = baseStyle.stroke || '#E63946'
  if (props.selected || isHovered.value) {
    return {
      ...baseStyle,
      stroke: props.selected ? '#F87171' : baseStroke,
      strokeWidth: props.selected ? 3 : 2.6,
      filter: props.selected
        ? 'drop-shadow(0 0 6px rgba(248, 113, 113, 0.75))'
        : `drop-shadow(0 0 4px ${baseStroke})`
    }
  }
  return baseStyle
})

const showDeleteButton = computed(() => Boolean(props.selected || isHovered.value))

function onDeleteClick(event: MouseEvent) {
  event.stopPropagation()
  emit('delete', props.id)
}
</script>

<template>
  <g @mouseenter="handleMouseEnter" @mouseleave="handleMouseLeave">
    <BaseEdge
      :id="id"
      :path="pathResult[0]"
      :style="edgeStyle"
      :marker-end="markerEnd"
      :interaction-width="24"
    />
  </g>

  <EdgeLabelRenderer>
    <div
      v-if="showDeleteButton"
      class="edge-delete-wrapper nodrag nopan"
      :style="{
        transform: `translate(-50%, -50%) translate(${pathResult[1]}px, ${pathResult[2]}px)`
      }"
      @mouseenter="handleMouseEnter"
      @mouseleave="handleMouseLeave"
    >
      <button
        type="button"
        class="edge-delete-btn"
        title="Usuń połączenie (Del)"
        aria-label="Delete edge"
        @click="onDeleteClick"
      >
        ×
      </button>
    </div>
  </EdgeLabelRenderer>
</template>

<style scoped>
.edge-delete-wrapper {
  position: absolute;
  pointer-events: all;
  z-index: 1000;
}

.edge-delete-btn {
  width: 20px;
  height: 20px;
  border-radius: 9999px;
  background-color: #1E293B;
  border: 1.5px solid #EF4444;
  color: #FCA5A5;
  font-size: 14px;
  font-weight: 700;
  line-height: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  padding: 0;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.55);
  transition: background-color 0.12s ease, color 0.12s ease, transform 0.12s ease;
}

.edge-delete-btn:hover {
  background-color: #EF4444;
  color: #FFFFFF;
  transform: scale(1.15);
}
</style>
