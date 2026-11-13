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

    <div class="node-header d-flex align-center justify-space-between px-2.5">
      <div class="d-flex align-center ga-2 overflow-hidden mr-1">
        <div class="node-icon-badge flex-shrink-0">
          <v-icon icon="mdi-book-open-page-variant-outline" size="12" color="#22D3EE" />
        </div>
        <span class="node-title text-truncate">{{ props.data.name || 'Agent Skill' }}</span>
      </div>
      <div class="node-actions d-flex align-center flex-shrink-0">
        <v-btn icon="mdi-cog-outline" size="18" variant="text" color="grey-lighten-1" class="node-action-btn" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete-outline" size="18" variant="text" color="grey-lighten-1" class="node-action-btn" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <div class="node-body px-2.5 py-2">
      <div class="skill-folder-box text-truncate">
        <v-icon icon="mdi-folder-outline" size="11" color="#22D3EE" class="mr-1.5 flex-shrink-0" />
        <span class="text-truncate">{{ props.data.endpoint ? `${props.data.endpoint}/SKILL.md` : 'Brak wybranego skilla' }}</span>
      </div>
      <div v-if="props.data.systemPrompt" class="skill-desc text-truncate mt-1">
        {{ props.data.systemPrompt }}
      </div>
      <div class="d-flex align-center justify-space-between mt-1.5">
        <span class="badge-tag">SKILL PORT</span>
        <span class="ms-tag">SKILL.md</span>
      </div>
    </div>

    <Handle id="skill-bottom" type="source" :position="Position.Bottom" class="node-handle" />
  </div>
</template>

<style scoped>
.custom-node {
  width: 195px;
  background-color: #111827;
  border-radius: 7px;
  border: 1px solid #283548;
  border-top: 3px solid #06B6D4;
  box-shadow: 0 3px 10px rgba(0, 0, 0, 0.35);
  font-family: inherit;
  transition: border-color 0.15s ease;
  user-select: none;
}

.custom-node.selected {
  border-color: #06B6D4;
  border-top: 3px solid #06B6D4;
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
  background-color: #0F2937;
  border: 1px solid #0E7490;
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

.skill-folder-box {
  font-size: 10px;
  color: #E0F2FE;
  background-color: #090D16;
  padding: 4px 6px;
  border-radius: 4px;
  border: 1px solid #164E63;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  display: flex;
  align-items: center;
}

.skill-desc {
  font-size: 10px;
  color: #94A3B8;
}

.badge-tag {
  font-size: 9px;
  font-weight: 600;
  color: #67E8F9;
  background-color: #0F2937;
  border: 1px solid #155E75;
  padding: 1px 5px;
  border-radius: 3px;
}

.ms-tag {
  font-size: 9px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  color: #64748B;
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
  border: 1.5px solid #F8FAFC;
}
</style>
