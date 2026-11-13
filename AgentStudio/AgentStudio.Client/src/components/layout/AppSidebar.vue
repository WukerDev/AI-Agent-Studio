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
  <v-navigation-drawer permanent rail rail-width="52" elevation="0" class="app-sidebar-rail">
    <div class="d-flex flex-column justify-space-between fill-height py-3">
      <div class="d-flex flex-column align-center ga-2">
        <v-tooltip location="right" :open-delay="250">
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

        <v-tooltip location="right" :open-delay="250">
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

        <v-tooltip location="right" :open-delay="250">
          <template v-slot:activator="{ props: tooltipProps }">
            <v-btn
              icon
              v-bind="tooltipProps"
              size="small"
              variant="text"
              class="rail-icon-btn"
              :class="{ 'active-rail-btn': props.activeLeftPanel === 'skills' }"
              @click="emit('toggle-left-panel', 'skills')"
            >
              <v-icon icon="mdi-book-open-page-variant-outline" size="20" />
            </v-btn>
          </template>
          <span>{{ t('layout.sidebar.skills') }}</span>
        </v-tooltip>

        <v-tooltip location="right" :open-delay="250">
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
        <v-tooltip location="right" :open-delay="250">
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

        <v-tooltip location="right" :open-delay="250">
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
  background-color: #0D131F !important;
  border-right: 1px solid #1E293B !important;
}

.rail-icon-btn {
  color: #64748B;
  border-radius: 6px;
  width: 36px !important;
  height: 36px !important;
  border: 1px solid transparent;
  transition: background-color 0.12s ease, color 0.12s ease, border-color 0.12s ease;
}

.rail-icon-btn:hover {
  color: #E2E8F0;
  background-color: #161F30;
}

.active-rail-btn {
  background-color: #1B263B !important;
  color: #F8FAFC !important;
  border-color: #334155 !important;
}
</style>
