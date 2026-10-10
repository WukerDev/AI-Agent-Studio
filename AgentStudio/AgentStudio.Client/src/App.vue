<script setup lang="ts">
import { ref } from 'vue'
import AppHeader from './components/layout/AppHeader.vue'
import AppSidebar from './components/layout/AppSidebar.vue'
import AppLeftDrawer from './components/layout/AppLeftDrawer.vue'
import AppBottomPanel from './components/layout/AppBottomPanel.vue'
import WorkflowCanvas from './components/workflow/WorkflowCanvas.vue'

const isBottomPanelOpen = ref(false)
const activeBottomTab = ref<string | null>(null)

const isLeftPanelOpen = ref(false)
const activeLeftTab = ref<string | null>(null)

const toggleBottomPanel = (tab: string): void => {
  if (isBottomPanelOpen.value && activeBottomTab.value === tab) {
    isBottomPanelOpen.value = false
    activeBottomTab.value = null
  } else {
    activeBottomTab.value = tab
    isBottomPanelOpen.value = true
  }
}

const toggleLeftPanel = (tab: string): void => {
  if (isLeftPanelOpen.value && activeLeftTab.value === tab) {
    isLeftPanelOpen.value = false
    activeLeftTab.value = null
  } else {
    activeLeftTab.value = tab
    isLeftPanelOpen.value = true
  }
}
</script>

<template>
  <v-app theme="agentStudioDarkTheme">
    <AppHeader />

    <AppSidebar
        :active-left-panel="activeLeftTab"
        :active-bottom-panel="activeBottomTab"
        @toggle-left-panel="toggleLeftPanel"
        @toggle-bottom-panel="toggleBottomPanel"
    />

    <AppLeftDrawer
        v-model="isLeftPanelOpen"
        :tab="activeLeftTab"
        @update:model-value="(val) => { if(!val) activeLeftTab = null }"
    />

    <AppBottomPanel
        v-model="isBottomPanelOpen"
        v-model:tab="activeBottomTab"
    />

    <v-main class="bg-grey-lighten-4 d-flex flex-column">
      <div class="flex-grow-1 position-relative">
        <WorkflowCanvas />
      </div>
    </v-main>
  </v-app>
</template>