<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { computed } from 'vue'
import { GlobalViewModel } from '../../GlobalViewModel'
import { useNotificationStore } from '../../notifications'

const props = defineProps<{
  modelValue: boolean
  tab: string | null
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'update:tab', value: string | null): void
}>()

const { t } = useI18n()
const globalViewModel = new GlobalViewModel()
const notifStore = useNotificationStore()

const isOpen = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

const activeTab = computed({
  get: () => props.tab || 'console',
  set: (value: string | null) => emit('update:tab', value)
})

const closePanel = (): void => {
  isOpen.value = false
}

function clearLogs() {
  globalViewModel.executionLogs.length = 0
  notifStore.info('Console logs cleared')
}

async function copyOutput() {
  if (!globalViewModel.executionOutput) return
  try {
    await navigator.clipboard.writeText(globalViewModel.executionOutput)
    notifStore.success('Copied output to clipboard')
  } catch {
    notifStore.info('Failed to copy to clipboard')
  }
}

function getLogLevelColor(level: string): string {
  switch (level?.toLowerCase()) {
    case 'error':
      return 'error'
    case 'warning':
      return 'warning'
    case 'info':
      return 'info'
    default:
      return 'grey'
  }
}
</script>

<template>
  <v-navigation-drawer
      v-model="isOpen"
      location="bottom"
      temporary
      elevation="6"
      height="300"
      class="app-bottom-drawer"
  >
    <v-toolbar density="compact" color="surface" class="border-b px-2">
      <v-tabs v-model="activeTab" density="compact" color="primary">
        <v-tab value="console">
          <v-badge
            v-if="globalViewModel.executionLogs.length > 0"
            color="primary"
            :content="globalViewModel.executionLogs.length"
            inline
          >
            {{ t('layout.bottomPanel.console') }}
          </v-badge>
          <span v-else>{{ t('layout.bottomPanel.console') }}</span>
        </v-tab>
        <v-tab value="output">{{ t('layout.bottomPanel.output') }}</v-tab>
      </v-tabs>

      <v-spacer></v-spacer>

      <v-btn
        v-if="activeTab === 'console'"
        icon="mdi-broom"
        variant="text"
        size="x-small"
        class="mr-1 text-grey"
        title="Wyczyść konsolę"
        @click="clearLogs"
      />
      <v-btn
        v-if="activeTab === 'output'"
        icon="mdi-content-copy"
        variant="text"
        size="x-small"
        class="mr-1 text-grey"
        title="Kopiuj wynik"
        @click="copyOutput"
      />
      <v-btn icon="mdi-close" variant="text" size="small" @click="closePanel"></v-btn>
    </v-toolbar>

    <v-window v-model="activeTab" class="fill-height">
      <v-window-item value="console" class="fill-height">
        <div class="fill-height overflow-y-auto bg-black text-white pa-4 font-monospace text-caption terminal-view">
          <div v-if="globalViewModel.executionLogs.length === 0" class="text-grey-darken-1 font-italic">
            > Ready. Run workflow to stream real-time agent execution telemetry...
          </div>
          <div
            v-for="(log, idx) in globalViewModel.executionLogs"
            :key="idx"
            class="d-flex align-start py-1 terminal-line"
          >
            <span class="text-grey-darken-1 mr-2 select-none">[{{ globalViewModel.formatTimestamp(log.timestamp) }}]</span>
            <v-chip size="x-small" :color="getLogLevelColor(log.level)" variant="flat" class="mr-2 text-uppercase font-weight-bold">
              {{ log.level }}
            </v-chip>
            <span class="flex-grow-1 text-grey-lighten-2">{{ log.message }}</span>
          </div>
        </div>
      </v-window-item>

      <v-window-item value="output" class="fill-height">
        <div class="fill-height overflow-y-auto bg-grey-darken-4 pa-4 font-monospace text-body-2 output-view">
          <div v-if="globalViewModel.executionOutput" class="text-white whitespace-pre-wrap">
            {{ globalViewModel.executionOutput }}
          </div>
          <div v-else class="text-grey-lighten-1">
            Status: {{ globalViewModel.executionStatus }}
            <br />
            No agent response generated yet.
          </div>
        </div>
      </v-window-item>
    </v-window>
  </v-navigation-drawer>
</template>

<style scoped>
.app-bottom-drawer {
  background: rgba(11, 17, 32, 0.98) !important;
  backdrop-filter: blur(16px);
  border-top: 1px solid rgba(255, 255, 255, 0.1);
}
.terminal-view {
  background-color: #030712 !important;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}
.output-view {
  background-color: #0F172A !important;
}
.whitespace-pre-wrap {
  white-space: pre-wrap;
}
.terminal-line {
  line-height: 1.4;
}
.select-none {
  user-select: none;
}
</style>
