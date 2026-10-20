<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { computed } from 'vue'
import { GlobalViewModel } from '../../GlobalViewModel'

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
      elevation="4"
      height="300"
  >
    <v-toolbar density="compact" color="surface">
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

      <v-btn icon="mdi-close" variant="text" size="small" @click="closePanel"></v-btn>
    </v-toolbar>

    <v-window v-model="activeTab" class="fill-height">
      <v-window-item value="console" class="fill-height">
        <div class="fill-height overflow-y-auto bg-black text-white pa-4 font-monospace text-caption">
          <div v-if="globalViewModel.executionLogs.length === 0" class="text-grey">
            > Ready. Run workflow to stream real-time agent execution telemetry...
          </div>
          <div
            v-for="(log, idx) in globalViewModel.executionLogs"
            :key="idx"
            class="d-flex align-start py-1"
          >
            <span class="text-grey mr-2">[{{ globalViewModel.formatTimestamp(log.timestamp) }}]</span>
            <v-chip size="x-small" :color="getLogLevelColor(log.level)" variant="flat" class="mr-2 text-uppercase">
              {{ log.level }}
            </v-chip>
            <span class="flex-grow-1 text-grey-lighten-2">{{ log.message }}</span>
          </div>
        </div>
      </v-window-item>

      <v-window-item value="output" class="fill-height">
        <div class="fill-height overflow-y-auto bg-grey-darken-4 pa-4 font-monospace text-body-2">
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
.whitespace-pre-wrap {
  white-space: pre-wrap;
}
</style>
