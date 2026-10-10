<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const props = defineProps<{
  activeLeftPanel: string | null
  activeBottomPanel: string | null
}>()

const emit = defineEmits<{
  (e: 'toggle-left-panel', panel: string): void
  (e: 'toggle-bottom-panel', panel: string): void
}>()

const { t } = useI18n()
</script>

<template>
  <v-navigation-drawer permanent rail elevation="2">
    <v-list density="compact" nav>
      <v-list-item
          prepend-icon="mdi-folder-outline"
          :active="props.activeLeftPanel === 'files'"
          :title="t('layout.sidebar.files')"
          @click="emit('toggle-left-panel', 'files')"
      ></v-list-item>

      <v-list-item
          prepend-icon="mdi-robot-outline"
          :active="props.activeLeftPanel === 'agents'"
          :title="t('layout.sidebar.agents')"
          @click="emit('toggle-left-panel', 'agents')"
      ></v-list-item>

      <v-list-item
          prepend-icon="mdi-cog-outline"
          :active="props.activeLeftPanel === 'settings'"
          :title="t('layout.sidebar.settings')"
          @click="emit('toggle-left-panel', 'settings')"
      ></v-list-item>
    </v-list>

    <template v-slot:append>
      <v-list density="compact" nav>
        <v-list-item
            prepend-icon="mdi-console-line"
            :active="props.activeBottomPanel === 'console'"
            :title="t('layout.sidebar.console')"
            @click="emit('toggle-bottom-panel', 'console')"
        ></v-list-item>

        <v-list-item
            prepend-icon="mdi-text-box-outline"
            :active="props.activeBottomPanel === 'output'"
            :title="t('layout.sidebar.output')"
            @click="emit('toggle-bottom-panel', 'output')"
        ></v-list-item>
      </v-list>
    </template>
  </v-navigation-drawer>
</template>