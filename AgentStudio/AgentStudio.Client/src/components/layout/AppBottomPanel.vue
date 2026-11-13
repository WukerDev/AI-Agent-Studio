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
  globalViewModel.clearExecutionLogs()
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
    elevation="0"
    height="290"
    class="app-bottom-drawer"
  >
    <div class="bottom-drawer-header d-flex align-center justify-space-between px-3">
      <v-tabs v-model="activeTab" density="compact" color="info">
        <v-tab value="console" class="text-caption font-weight-bold">
          <v-icon icon="mdi-console-line" size="15" class="mr-1.5" />
          <span>{{ t('layout.bottomPanel.console') }}</span>
          <span v-if="globalViewModel.executionLogs.length > 0" class="tab-count-pill ml-2">
            {{ globalViewModel.executionLogs.length }}
          </span>
        </v-tab>
        <v-tab value="output" class="text-caption font-weight-bold">
          <v-icon icon="mdi-text-box-outline" size="15" class="mr-1.5" />
          <span>{{ t('layout.bottomPanel.output') }}</span>
        </v-tab>
      </v-tabs>

      <div class="d-flex align-center ga-1">
        <v-btn
          v-if="activeTab === 'console'"
          icon="mdi-broom"
          variant="text"
          size="x-small"
          color="grey-lighten-1"
          title="Wyczyść konsolę"
          @click="clearLogs"
        />
        <v-btn
          v-if="activeTab === 'output'"
          icon="mdi-content-copy"
          variant="text"
          size="x-small"
          color="grey-lighten-1"
          title="Kopiuj wynik"
          @click="copyOutput"
        />
        <v-btn icon="mdi-close" variant="text" size="x-small" color="grey-lighten-1" @click="closePanel" />
      </div>
    </div>

    <v-window v-model="activeTab" class="fill-height">
      <v-window-item value="console" class="fill-height">
        <div class="fill-height overflow-y-auto pa-3 font-monospace text-caption terminal-view">
          <div v-if="globalViewModel.executionLogs.length === 0" class="text-grey-darken-1">
            $ Ready. Run workflow to stream real-time agent execution telemetry...
          </div>
          <div
            v-for="(log, idx) in globalViewModel.executionLogs"
            :key="idx"
            class="d-flex flex-column py-1 terminal-line"
          >
            <div class="d-flex align-start ga-2">
              <span class="text-grey-darken-1 select-none">[{{ globalViewModel.formatTimestamp(log.timestamp) }}]</span>
              <v-chip size="x-small" :color="getLogLevelColor(log.level)" variant="tonal" class="text-uppercase font-weight-bold">
                {{ log.level }}
              </v-chip>
              <span class="flex-grow-1 text-grey-lighten-2">{{ log.message }}</span>
            </div>
            <div v-if="log.data" class="text-grey-darken-1 pl-6 mt-1 whitespace-pre-wrap">
              {{ typeof log.data === 'string' ? log.data : JSON.stringify(log.data) }}
            </div>
          </div>
        </div>
      </v-window-item>

      <v-window-item value="output" class="fill-height">
        <div class="fill-height overflow-y-auto pa-4 font-monospace text-body-2 output-view">
          <div v-if="globalViewModel.executionOutput" class="text-white whitespace-pre-wrap">
            {{ globalViewModel.executionOutput }}
          </div>
          <div v-else class="text-grey">
            Status: {{ globalViewModel.executionStatus }} — No agent response generated yet.
          </div>
        </div>
      </v-window-item>
    </v-window>
  </v-navigation-drawer>
</template>

<style scoped>
.app-bottom-drawer {
  background-color: #0D131F !important;
  border-top: 1px solid #283548 !important;
}

.bottom-drawer-header {
  background-color: #111827;
  border-bottom: 1px solid #1E293B;
  height: 40px;
}

.tab-count-pill {
  background-color: #1E293B;
  border: 1px solid #334155;
  color: #E2E8F0;
  font-size: 10px;
  padding: 1px 6px;
  border-radius: 4px;
}

.terminal-view {
  background-color: #080C14 !important;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

.output-view {
  background-color: #0B101B !important;
}

.whitespace-pre-wrap {
  white-space: pre-wrap;
}

.terminal-line {
  line-height: 1.45;
  border-bottom: 1px solid rgba(30, 41, 59, 0.4);
}

.select-none {
  user-select: none;
}
</style>
