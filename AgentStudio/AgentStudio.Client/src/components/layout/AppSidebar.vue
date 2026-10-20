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
  <v-navigation-drawer permanent rail elevation="0" class="app-sidebar-rail">
    <div class="d-flex flex-column justify-space-between fill-height py-3">
      <div class="d-flex flex-column align-center ga-2">
        <v-tooltip location="right">
          <template v-slot:activator="{ props: tooltipProps }">
            <v-btn
              icon
              v-bind="tooltipProps"
              size="small"
              variant="text"
              class="rail-icon-btn"
              :class="{ 'active-rail-btn': props.activeLeftPanel === 'files' }"
              @click="emit('toggle-left-panel', 'files')"
            >
              <v-icon icon="mdi-folder-outline" size="20" />
            </v-btn>
          </template>
          <span>{{ t('layout.sidebar.files') }}</span>
        </v-tooltip>

        <v-tooltip location="right">
          <template v-slot:activator="{ props: tooltipProps }">
            <v-btn
              icon
              v-bind="tooltipProps"
              size="small"
              variant="text"
              class="rail-icon-btn"
              :class="{ 'active-rail-btn': props.activeLeftPanel === 'agents' }"
              @click="emit('toggle-left-panel', 'agents')"
            >
              <v-icon icon="mdi-robot-outline" size="20" />
            </v-btn>
          </template>
          <span>{{ t('layout.sidebar.agents') }}</span>
        </v-tooltip>

        <v-tooltip location="right">
          <template v-slot:activator="{ props: tooltipProps }">
            <v-btn
              icon
              v-bind="tooltipProps"
              size="small"
              variant="text"
              class="rail-icon-btn"
              :class="{ 'active-rail-btn': props.activeLeftPanel === 'settings' }"
              @click="emit('toggle-left-panel', 'settings')"
            >
              <v-icon icon="mdi-cog-outline" size="20" />
            </v-btn>
          </template>
          <span>{{ t('layout.sidebar.settings') }}</span>
        </v-tooltip>
      </div>

      <div class="d-flex flex-column align-center ga-2">
        <v-tooltip location="right">
          <template v-slot:activator="{ props: tooltipProps }">
            <v-btn
              icon
              v-bind="tooltipProps"
              size="small"
              variant="text"
              class="rail-icon-btn"
              :class="{ 'active-rail-btn': props.activeBottomPanel === 'console' }"
              @click="emit('toggle-bottom-panel', 'console')"
            >
              <v-icon icon="mdi-console-line" size="20" />
            </v-btn>
          </template>
          <span>{{ t('layout.sidebar.console') }}</span>
        </v-tooltip>

        <v-tooltip location="right">
          <template v-slot:activator="{ props: tooltipProps }">
            <v-btn
              icon
              v-bind="tooltipProps"
              size="small"
              variant="text"
              class="rail-icon-btn"
              :class="{ 'active-rail-btn': props.activeBottomPanel === 'output' }"
              @click="emit('toggle-bottom-panel', 'output')"
            >
              <v-icon icon="mdi-text-box-outline" size="20" />
            </v-btn>
          </template>
          <span>{{ t('layout.sidebar.output') }}</span>
        </v-tooltip>
      </div>
    </div>
  </v-navigation-drawer>
</template>

<style scoped>
.app-sidebar-rail {
  background: rgba(11, 17, 32, 0.95) !important;
  backdrop-filter: blur(10px);
  border-right: 1px solid rgba(255, 255, 255, 0.08);
}
.rail-icon-btn {
  color: #94A3B8;
  border-radius: 8px;
  transition: all 0.15s ease;
}
.rail-icon-btn:hover {
  color: #FFFFFF;
  background: rgba(255, 255, 255, 0.06);
}
.active-rail-btn {
  background: rgba(230, 57, 70, 0.18) !important;
  color: #E63946 !important;
  border: 1px solid rgba(230, 57, 70, 0.3);
}
</style>
