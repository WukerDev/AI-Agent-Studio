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
  <div class="custom-node skill-node" :class="{ selected: props.selected }">
    <Handle id="skill-out" type="source" :position="Position.Top" class="node-handle" title="Podłącz do portu 'Skill' w węźle AI Agent" />

    <div class="node-header d-flex align-center justify-space-between px-2 py-1">
      <div class="d-flex align-center overflow-hidden mr-1">
        <v-icon icon="mdi-book-open-page-variant-outline" size="14" class="mr-1.5 flex-shrink-0" color="white" />
        <span class="node-title text-truncate">{{ props.data.name || 'Agent Skill' }}</span>
      </div>
      <div class="d-flex align-center flex-shrink-0">
        <v-btn icon="mdi-cog" size="20" variant="text" color="white" class="node-action-btn" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete" size="20" variant="text" color="white" class="node-action-btn" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <div class="node-body px-2 py-1.5">
      <div class="skill-folder-box text-truncate">
        <v-icon icon="mdi-folder-outline" size="10" color="#22D3EE" class="mr-1" />
        <span>{{ props.data.endpoint ? `${props.data.endpoint}/SKILL.md` : 'Brak wybranego skilla' }}</span>
      </div>
      <div v-if="props.data.systemPrompt" class="skill-desc text-truncate mt-1">
        {{ props.data.systemPrompt }}
      </div>
      <div class="d-flex align-center justify-space-between mt-1">
        <span class="badge-tag">SKILL PORT</span>
        <span class="ms-tag">SKILL.md</span>
      </div>
    </div>

    <Handle id="skill-bottom" type="source" :position="Position.Bottom" class="node-handle" />
  </div>
</template>

<style scoped>
.custom-node {
  width: 175px;
  background-color: #111827;
  border-radius: 6px;
  border: 1px solid #374151;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.4);
  font-family: inherit;
  transition: border-color 0.2s, box-shadow 0.2s;
  user-select: none;
}

.custom-node.selected {
  border-color: #06B6D4;
  box-shadow: 0 0 0 2px rgba(6, 182, 212, 0.4);
}

.skill-node .node-header {
  background: linear-gradient(90deg, #06B6D4 0%, #0891B2 100%);
  border-top-left-radius: 5px;
  border-top-right-radius: 5px;
  height: 26px;
}

.node-title {
  font-size: 11px;
  font-weight: 600;
  color: #FFFFFF;
}

.skill-folder-box {
  font-size: 10px;
  color: #E0F2FE;
  background-color: #030712;
  padding: 3px 5px;
  border-radius: 3px;
  border: 1px solid #164E63;
  font-family: monospace;
  display: flex;
  align-items: center;
}

.skill-desc {
  font-size: 9.5px;
  color: #9CA3AF;
}

.badge-tag {
  font-size: 9px;
  font-weight: 700;
  color: #22D3EE;
  background: rgba(6, 182, 212, 0.15);
  padding: 1px 4px;
  border-radius: 3px;
}

.ms-tag {
  font-size: 8.5px;
  font-family: monospace;
  color: #67E8F9;
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
  background-color: #06B6D4;
  border: 1.5px solid #FFFFFF;
}
</style>
