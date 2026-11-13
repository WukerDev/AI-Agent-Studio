<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { AuthViewModel } from '../../AuthViewModel'
import { GlobalViewModel } from '../../GlobalViewModel'

const { t, locale } = useI18n()
const authViewModel = new AuthViewModel()
const globalViewModel = new GlobalViewModel()

function toggleLocale() {
  locale.value = locale.value === 'pl' ? 'en' : 'pl'
}

function handleLogout() {
  authViewModel.logout()
}

const userDisplay = computed(() => {
  return authViewModel.username || 'Admin'
})

const userInitials = computed(() => {
  const name = userDisplay.value
  return name.substring(0, 2).toUpperCase()
})
</script>

<template>
  <v-app-bar elevation="0" density="compact" class="app-header-bar px-3">
    <div class="d-flex align-center w-100 ga-3">
      <!-- Brand Identity -->
      <div class="d-flex align-center cursor-pointer">
        <div class="logo-box mr-2">
          <v-icon icon="mdi-robot-outline" color="#E63946" size="18" />
        </div>
        <div class="d-flex align-center ga-2">
          <span class="header-title">Agent Studio</span>
          <span class="studio-badge">v2.0</span>
        </div>
      </div>

      <div class="header-divider" />

      <!-- Active Flow Indicator -->
      <div class="flow-indicator d-flex align-center ga-1.5">
        <v-icon icon="mdi-sitemap-outline" size="14" color="#94A3B8" />
        <span class="flow-indicator-label">Flow:</span>
        <span class="flow-indicator-value font-monospace">
          {{ globalViewModel.currentFlowId || 'default-flow' }}
        </span>
      </div>

      <v-progress-circular
        v-if="globalViewModel.isGrpcLoading"
        indeterminate
        color="info"
        size="16"
        width="2"
        class="ml-1"
      />

      <v-spacer />

      <!-- Right Controls -->
      <div class="d-flex align-center ga-2">
        <v-tooltip location="bottom">
          <template v-slot:activator="{ props: tooltipProps }">
            <div
              v-bind="tooltipProps"
              class="status-pill d-flex align-center ga-1.5"
              :class="globalViewModel.isSignalRConnected ? 'status-online' : 'status-offline'"
            >
              <span class="status-dot" />
              <span>{{ globalViewModel.isSignalRConnected ? 'LIVE' : 'OFFLINE' }}</span>
            </div>
          </template>
          <span>
            SignalR Event Bus: {{ globalViewModel.isSignalRConnected ? 'Connected & listening' : 'Disconnected' }}
          </span>
        </v-tooltip>

        <button
          type="button"
          class="lang-switch-btn d-flex align-center ga-1"
          title="Switch Language (PL / EN)"
          @click="toggleLocale"
        >
          <v-icon icon="mdi-translate" size="14" color="#94A3B8" />
          <span>{{ locale.toUpperCase() }}</span>
        </button>

        <v-menu offset-y>
          <template v-slot:activator="{ props: menuProps }">
            <button type="button" v-bind="menuProps" class="user-avatar-btn" title="Account">
              {{ userInitials }}
            </button>
          </template>

          <v-list density="compact" min-width="200" class="solid-menu rounded-lg pa-1">
            <v-list-item class="rounded">
              <template v-slot:prepend>
                <v-icon icon="mdi-account-circle-outline" class="mr-2" size="18" color="#94A3B8" />
              </template>
              <v-list-item-title class="text-caption font-weight-bold text-white">{{ userDisplay }}</v-list-item-title>
              <v-list-item-subtitle class="text-caption" style="color: #64748B;">Keycloak IAM</v-list-item-subtitle>
            </v-list-item>

            <v-divider class="my-1" />

            <v-list-item class="rounded" @click="handleLogout">
              <template v-slot:prepend>
                <v-icon icon="mdi-logout" class="mr-2" size="16" color="error" />
              </template>
              <v-list-item-title class="text-caption font-weight-medium text-error">
                {{ t('layout.header.logout') }}
              </v-list-item-title>
            </v-list-item>
          </v-list>
        </v-menu>
      </div>
    </div>
  </v-app-bar>
</template>

<style scoped>
.app-header-bar {
  background-color: #0D131F !important;
  border-bottom: 1px solid #1E293B !important;
}

.logo-box {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  background-color: #1E151C;
  border: 1px solid #4C1D24;
  display: flex;
  align-items: center;
  justify-content: center;
}

.header-title {
  font-size: 14px;
  font-weight: 700;
  color: #F8FAFC;
  letter-spacing: -0.01em;
}

.studio-badge {
  font-size: 10px;
  font-weight: 600;
  color: #94A3B8;
  background-color: #161F30;
  border: 1px solid #283548;
  padding: 1px 6px;
  border-radius: 4px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

.header-divider {
  width: 1px;
  height: 18px;
  background-color: #1E293B;
}

.flow-indicator {
  background-color: #131C2E;
  border: 1px solid #233047;
  padding: 3px 10px;
  border-radius: 6px;
  font-size: 12px;
}

.flow-indicator-label {
  color: #64748B;
  font-weight: 500;
}

.flow-indicator-value {
  color: #E2E8F0;
  font-weight: 600;
}

.status-pill {
  font-size: 11px;
  font-weight: 600;
  padding: 3px 9px;
  border-radius: 5px;
  letter-spacing: 0.03em;
  cursor: default;
}

.status-online {
  background-color: #0D281E;
  border: 1px solid #14532D;
  color: #4ADE80;
}

.status-offline {
  background-color: #2A1215;
  border: 1px solid #7F1D1D;
  color: #F87171;
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background-color: currentColor;
}

.lang-switch-btn {
  background-color: #161F30;
  border: 1px solid #283548;
  color: #E2E8F0;
  font-size: 11px;
  font-weight: 600;
  padding: 3px 9px;
  border-radius: 5px;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.lang-switch-btn:hover {
  background-color: #1E293B;
  border-color: #3B4D6B;
}

.user-avatar-btn {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  background-color: #1E293B;
  border: 1px solid #334155;
  color: #F8FAFC;
  font-size: 11px;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.user-avatar-btn:hover {
  background-color: #283548;
  border-color: #475569;
}

.solid-menu {
  background-color: #111827 !important;
  border: 1px solid #283548 !important;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.45) !important;
}
</style>
