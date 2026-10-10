<script setup lang="ts">
defineOptions({
  name: 'WorkflowCanvas'
})

import { ref } from 'vue'
import { VueFlow, useVueFlow } from '@vue-flow/core'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'
import { MiniMap } from '@vue-flow/minimap'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import '@vue-flow/controls/dist/style.css'
import '@vue-flow/minimap/dist/style.css'

const { onPaneReady } = useVueFlow()

const nodes = ref([
  {
    id: '1',
    type: 'input',
    label: 'Użytkownik',
    position: { x: 250, y: 100 },
  },
  {
    id: '2',
    label: 'Agent LLM',
    position: { x: 250, y: 250 },
  }
])

const edges = ref([
  {
    id: 'e1-2',
    source: '1',
    target: '2',
    animated: true
  }
])

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