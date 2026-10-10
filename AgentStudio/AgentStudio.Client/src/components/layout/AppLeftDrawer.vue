<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { computed } from 'vue'

const props = defineProps<{
  modelValue: boolean
  tab: string | null
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
}>()

const { t } = useI18n()

const isOpen = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})
</script>

<template>
  <v-navigation-drawer
      v-model="isOpen"
      location="left"
      elevation="0"
      border="0"
      width="300"
  >
    <v-toolbar density="compact" color="surface">
      <v-toolbar-title class="text-subtitle-2 font-weight-bold">
        {{ props.tab ? t(`layout.sidebar.${props.tab}`) : '' }}
      </v-toolbar-title>
      <v-spacer></v-spacer>
      <v-btn icon="mdi-close" variant="text" size="small" @click="isOpen = false"></v-btn>
    </v-toolbar>

    <v-divider></v-divider>

    <v-container class="pa-4">
      <div v-if="props.tab === 'files'">
        <p class="text-body-2 text-medium-emphasis">{{ t('layout.leftDrawer.filesEmpty') }}</p>
      </div>
      <div v-else-if="props.tab === 'agents'">
        <p class="text-body-2 text-medium-emphasis">{{ t('layout.leftDrawer.agentsEmpty') }}</p>
      </div>
      <div v-else-if="props.tab === 'settings'">
        <p class="text-body-2 text-medium-emphasis">{{ t('layout.leftDrawer.settingsEmpty') }}</p>
      </div>
    </v-container>
  </v-navigation-drawer>
</template>