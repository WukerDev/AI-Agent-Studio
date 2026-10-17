<script setup lang="ts">
defineOptions({
  name: 'WorkflowCanvas'
})

import { shallowRef, watch } from 'vue'
import { VueFlow, useVueFlow, type Node, type Edge } from '@vue-flow/core'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'
import { MiniMap } from '@vue-flow/minimap'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import '@vue-flow/controls/dist/style.css'
import '@vue-flow/minimap/dist/style.css'
import { GlobalViewModel } from '../viewmodels/GlobalViewModel'

const { onPaneReady, onNodeDragStop } = useVueFlow()
const globalViewModel = new GlobalViewModel()

const nodes = shallowRef<Node[]>([])
const edges = shallowRef<Edge[]>([])

watch(() => globalViewModel.nodes, (newNodes) => {
  //@ts-ignore
  nodes.value = newNodes.map(node => ({
    id: node.id,
    label: node.name,
    position: { x: node.position?.x ?? 0, y: node.position?.y ?? 0 },
    data: JSON.parse(JSON.stringify(node))
  }))
}, { immediate: true, deep: true })

onNodeDragStop((event) => {
  const draggedNode = event.node
  const originalData = draggedNode.data

  globalViewModel.saveNode({
    id: draggedNode.id,
    name: originalData.name,
    type: originalData.type,
    position: { x: draggedNode.position.x, y: draggedNode.position.y },
    modelConfig: originalData.modelConfig,
    systemPrompt: originalData.systemPrompt,
    endpoint: originalData.endpoint,
    mcpConfig: originalData.mcpConfig
  })
})

onPaneReady((vueFlowInstance) => {
  vueFlowInstance.fitView()
})
</script>

<template>
  <VueFlow
      :nodes="nodes"
      :edges="edges"
      :default-viewport="{ zoom: 1 }"
      :min-zoom="0.2"
      :max-zoom="4"
      fit-view-on-init
  >
    <Background/>
    <Controls />
    <MiniMap />
  </VueFlow>
</template>

<style scoped>
.canvas-container {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  width: 100%;
  height: 100%;
}
</style>