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
    <div class="node-header d-flex align-center justify-space-between px-2.5 py-1.5">
      <div class="d-flex align-center overflow-hidden mr-1">
        <v-icon icon="mdi-robot" size="15" class="mr-1.5 flex-shrink-0" color="white" />
        <span class="node-title text-truncate">{{ props.data.name || 'AI Agent' }}</span>
      </div>
      <div class="d-flex align-center flex-shrink-0">
        <v-btn icon="mdi-cog" size="20" variant="text" color="white" class="node-action-btn" @click.stop="emit('configure', props.data)" />
        <v-btn icon="mdi-delete" size="20" variant="text" color="white" class="node-action-btn" @click.stop="emit('delete', props.id)" />
      </div>
    </div>

    <!-- Body: Connected Sub-Nodes Summary -->
    <div class="node-body px-2.5 py-2">
      <div class="d-flex align-center justify-space-between mb-1">
        <span class="badge-tag">N8N AGENT HUB</span>
        <span class="status-text">
          {{ connectedNodes.model ? connectedNodes.model.modelConfig?.modelName || 'Model OK' : 'Brak modelu' }}
        </span>
      </div>

      <div class="ports-status-row d-flex align-center justify-space-between mt-1.5 pt-1.5">
        <div class="port-slot" :class="{ connected: !!connectedNodes.model }">
          <v-icon icon="mdi-brain" size="11" color="#60A5FA" />
          <span>Model*</span>
        </div>
        <div class="port-slot" :class="{ connected: connectedNodes.prompts.length > 0 }">
          <v-icon icon="mdi-script-text-outline" size="11" color="#FBBF24" />
          <span>Prompt ({{ connectedNodes.prompts.length }})</span>
        </div>
        <div class="port-slot" :class="{ connected: connectedNodes.tools.length > 0 }">
          <v-icon icon="mdi-connection" size="11" color="#34D399" />
          <span>Tool ({{ connectedNodes.tools.length }})</span>
        </div>
        <div class="port-slot" :class="{ connected: connectedNodes.skills.length > 0 }">
          <v-icon icon="mdi-book-open-page-variant-outline" size="11" color="#22D3EE" />
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
  width: 245px;
  background-color: #111827;
  border-radius: 8px;
  border: 1.5px solid #374151;
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.5);
  font-family: inherit;
  transition: border-color 0.2s, box-shadow 0.2s;
  user-select: none;
  position: relative;
}

.custom-node.selected {
  border-color: #E63946;
  box-shadow: 0 0 0 2px rgba(230, 57, 70, 0.45);
}

.agent-node .node-header {
  background: linear-gradient(90deg, #E63946 0%, #BE123C 100%);
  border-top-left-radius: 6px;
  border-top-right-radius: 6px;
  height: 28px;
}

.node-title {
  font-size: 11.5px;
  font-weight: 700;
  color: #FFFFFF;
}

.badge-tag {
  font-size: 8.5px;
  font-weight: 700;
  color: #F87171;
  background: rgba(239, 68, 68, 0.15);
  padding: 1px 5px;
  border-radius: 3px;
  letter-spacing: 0.4px;
}

.status-text {
  font-size: 9.5px;
  color: #D1D5DB;
  font-family: monospace;
}

.ports-status-row {
  border-top: 1px dashed rgba(255, 255, 255, 0.1);
  padding-bottom: 4px;
}

.port-slot {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  font-size: 8.5px;
  font-weight: 600;
  color: #9CA3AF;
  width: 24%;
  text-align: center;
}

.port-slot.connected {
  color: #F9FAFB;
}

.node-action-btn {
  width: 18px !important;
  height: 18px !important;
  min-width: 18px !important;
  padding: 0 !important;
}

.pipeline-handle {
  width: 9px;
  height: 9px;
  background-color: #E63946;
  border: 2px solid #FFFFFF;
}

.subnode-handle {
  width: 10px;
  height: 10px;
  border-radius: 2px;
  transform: translate(-50%, 50%) rotate(45deg);
  border: 1.5px solid #FFFFFF;
}

.port-model {
  background-color: #2196F3;
}

.port-prompt {
  background-color: #FB8C00;
}

.port-tool {
  background-color: #4CAF50;
}

.port-skill {
  background-color: #06B6D4;
}
</style>
