<script setup lang="ts">
import { computed } from 'vue'
import { Handle, Position } from '@vue-flow/core'
import { GlobalViewModel } from '../../../GlobalViewModel'
import { NodeType } from '../../../clients/generated/agent_node'

const props = defineProps<{
  id: string
  data: any
  selected?: boolean
}>()

const emit = defineEmits<{
  (e: 'configure', nodeData: any): void
  (e: 'delete', nodeId: string): void
}>()

const globalViewModel = new GlobalViewModel()

const connectedNodes = computed(() => {
  const edges = globalViewModel.edges || []
  const allNodes = globalViewModel.nodes || []
  const neighborIds = new Set<string>()

  for (const e of edges) {
    if (e.sourceNodeId === props.id) neighborIds.add(e.targetNodeId)
    if (e.targetNodeId === props.id) neighborIds.add(e.sourceNodeId)
  }

  const neighbors = allNodes.filter(n => neighborIds.has(n.id))
  return {
    model: neighbors.find(n => n.type === NodeType.MODEL),
    prompts: neighbors.filter(n => n.type === NodeType.PROMPT),
    tools: neighbors.filter(n => n.type === NodeType.MCP_CONNECTOR),
    skills: neighbors.filter(n => n.type === NodeType.SKILL)
  }
})
</script>

<template>
  <div class="custom-node agent-node" :class="{ selected: props.selected }">
    <!-- Pipeline Handles (Agent-to-Agent) -->
    <Handle id="agent-in" type="target" :position="Position.Left" class="pipeline-handle pipeline-in" title="Wejście od poprzedniego Agenta" />
    <Handle id="agent-out" type="source" :position="Position.Right" class="pipeline-handle pipeline-out" title="Wyjście do kolejnego Agenta" />

    <!-- Header -->
    <div class="node-header d-flex align-center justify-space-between px-2.5">
      <div class="d-flex align-center ga-2 overflow-hidden mr-1">
        <div class="node-icon-badge flex-shrink-0">
          <v-icon icon="mdi-robot-outline" size="13" color="#F87171" />
        </div>
        <span class="node-title text-truncate">{{ props.data.name || 'AI Agent' }}</span>
      </div>
      <div class="node-actions d-flex align-center ga-0.5 flex-shrink-0">
        <v-btn icon="mdi-cog-outline" size="18" variant="text" color="grey-lighten-1" class="node-action-btn" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete-outline" size="18" variant="text" color="grey-lighten-1" class="node-action-btn" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <!-- Body: Connected Sub-Nodes Summary -->
    <div class="node-body px-2.5 py-2">
      <div class="d-flex align-center justify-space-between mb-2">
        <span class="badge-tag">AGENT HUB</span>
        <span class="status-text text-truncate" style="max-width: 135px;">
          {{ connectedNodes.model ? connectedNodes.model.modelConfig?.modelName || 'Model OK' : 'Brak modelu' }}
        </span>
      </div>

      <div class="ports-status-row d-flex align-center justify-space-between pt-2">
        <div class="port-slot" :class="{ connected: !!connectedNodes.model }">
          <v-icon icon="mdi-brain" size="12" color="#60A5FA" />
          <span>Model*</span>
        </div>
        <div class="port-slot" :class="{ connected: connectedNodes.prompts.length > 0 }">
          <v-icon icon="mdi-script-text-outline" size="12" color="#FBBF24" />
          <span>Prompt ({{ connectedNodes.prompts.length }})</span>
        </div>
        <div class="port-slot" :class="{ connected: connectedNodes.tools.length > 0 }">
          <v-icon icon="mdi-connection" size="12" color="#34D399" />
          <span>Tool ({{ connectedNodes.tools.length }})</span>
        </div>
        <div class="port-slot" :class="{ connected: connectedNodes.skills.length > 0 }">
          <v-icon icon="mdi-book-open-page-variant-outline" size="12" color="#22D3EE" />
          <span>Skill ({{ connectedNodes.skills.length }})</span>
        </div>
      </div>
    </div>

    <!-- Dedicated n8n-style Bottom Ports for Model, System Prompt, MCP Tool, and Agent Skill -->
    <Handle
      id="model"
      type="target"
      :position="Position.Bottom"
      class="subnode-handle port-model"
      style="left: 14%"
      title="Port: LLM Model"
    />
    <Handle
      id="prompt"
      type="target"
      :position="Position.Bottom"
      class="subnode-handle port-prompt"
      style="left: 38%"
      title="Port: System Prompt"
    />
    <Handle
      id="tool"
      type="target"
      :position="Position.Bottom"
      class="subnode-handle port-tool"
      style="left: 62%"
      title="Port: MCP Tool"
    />
    <Handle
      id="skill"
      type="target"
      :position="Position.Bottom"
      class="subnode-handle port-skill"
      style="left: 86%"
      title="Port: Agent Skill (SKILL.md)"
    />
  </div>
</template>

<style scoped>
.custom-node {
  width: 260px;
  background-color: #111827;
  border-radius: 7px;
  border: 1px solid #283548;
  border-top: 3px solid #E63946;
  box-shadow: 0 3px 10px rgba(0, 0, 0, 0.4);
  font-family: inherit;
  transition: border-color 0.15s ease;
  user-select: none;
  position: relative;
}

.custom-node.selected {
  border-color: #E63946;
  border-top: 3px solid #E63946;
}

.node-header {
  background-color: #151E2E;
  border-bottom: 1px solid #1E293B;
  height: 32px;
}

.node-icon-badge {
  width: 20px;
  height: 20px;
  border-radius: 4px;
  background-color: #2A1215;
  border: 1px solid #7F1D1D;
  display: flex;
  align-items: center;
  justify-content: center;
}

.node-title {
  font-size: 12px;
  font-weight: 700;
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

.badge-tag {
  font-size: 9px;
  font-weight: 700;
  color: #FCA5A5;
  background-color: #2A1215;
  border: 1px solid #7F1D1D;
  padding: 1px 5px;
  border-radius: 3px;
  letter-spacing: 0.03em;
}

.status-text {
  font-size: 10px;
  color: #94A3B8;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

.ports-status-row {
  border-top: 1px solid #1E293B;
  padding-bottom: 4px;
}

.port-slot {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  font-size: 9px;
  font-weight: 600;
  color: #64748B;
  width: 24%;
  text-align: center;
}

.port-slot.connected {
  color: #E2E8F0;
}

.node-action-btn {
  width: 20px !important;
  height: 20px !important;
  min-width: 20px !important;
  padding: 0 !important;
}

.pipeline-handle {
  width: 9px;
  height: 9px;
  background-color: #E63946;
  border: 1.5px solid #F8FAFC;
}

.subnode-handle {
  width: 9px;
  height: 9px;
  border-radius: 2px;
  transform: translate(-50%, 50%) rotate(45deg);
  border: 1.5px solid #F8FAFC;
}

.port-model {
  background-color: #3B82F6;
}

.port-prompt {
  background-color: #F59E0B;
}

.port-tool {
  background-color: #10B981;
}

.port-skill {
  background-color: #06B6D4;
}
</style>
