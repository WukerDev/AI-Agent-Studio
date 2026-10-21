<script setup lang="ts">
import { ref, onMounted } from 'vue'
import AppHeader from './components/layout/AppHeader.vue'
import AppSidebar from './components/layout/AppSidebar.vue'
import AppLeftDrawer from './components/layout/AppLeftDrawer.vue'
import AppBottomPanel from './components/layout/AppBottomPanel.vue'
import AppInspectorDrawer from './components/layout/AppInspectorDrawer.vue'
import AppNotificationSnackbar from './components/layout/AppNotificationSnackbar.vue'
import WorkflowCanvas from './components/workflow/WorkflowCanvas.vue'
import { GlobalViewModel } from './GlobalViewModel'

const globalViewModel = new GlobalViewModel()

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

onMounted(() => {
  globalViewModel.initialize()
})
</script>

<template>
  <v-app theme="agentStudioDarkTheme" class="agent-studio-app">
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

    <!-- Shell Right Inspector Drawer -->
    <AppInspectorDrawer />

    <v-main class="d-flex flex-column main-wrapper">
      <div class="flex-grow-1 position-relative">
        <WorkflowCanvas />
      </div>
    </v-main>

    <AppNotificationSnackbar />
  </v-app>
</template>

<style scoped>
.agent-studio-app {
  background-color: #070D18 !important;
  user-select: none;
}
.main-wrapper {
  background-color: #070D18 !important;
  overflow: hidden;
}
</style>
