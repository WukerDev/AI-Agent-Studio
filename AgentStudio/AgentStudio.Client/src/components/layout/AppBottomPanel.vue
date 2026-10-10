<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { computed } from 'vue'

const props = defineProps<{
  modelValue: boolean
  tab: string | null
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'update:tab', value: string | null): void
}>()

const { t } = useI18n()

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
        <v-tab value="console">{{ t('layout.bottomPanel.console') }}</v-tab>
        <v-tab value="output">{{ t('layout.bottomPanel.output') }}</v-tab>
      </v-tabs>

      <v-spacer></v-spacer>

      <v-btn icon="mdi-close" variant="text" size="small" @click="closePanel"></v-btn>
    </v-toolbar>

    <v-window v-model="activeTab" class="fill-height">
      <v-window-item value="console" class="fill-height">
        <v-container fluid class="fill-height align-start bg-black text-white pa-4 font-monospace">
          <div>> dotnet run AgentStudio.dll...</div>
        </v-container>
      </v-window-item>

      <v-window-item value="output" class="fill-height">
        <v-container fluid class="fill-height align-start bg-grey-lighten-4 pa-4 font-monospace">
          <div class="text-body-2 text-medium-emphasis">
            [11:03:38] Semantic Kernel initialized.<br/>
            [11:03:39] Agent workflow compiled successfully.
          </div>
        </v-container>
      </v-window-item>
    </v-window>
  </v-navigation-drawer>
</template>