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
  <v-app-bar elevation="0" density="compact" class="app-header-bar px-2">
    <div class="d-flex align-center px-2 w-100 ga-3">
      <div class="d-flex align-center cursor-pointer">
        <div class="logo-box mr-2">
          <v-icon icon="mdi-robot-excited-outline" color="primary" size="20" />
        </div>
        <div class="d-flex align-center">
          <span class="text-subtitle-1 font-weight-bold tracking-wide text-white">
            Agent Studio
          </span>
          <span class="studio-badge ml-1.5">v2.0</span>
        </div>
      </div>

      <v-chip size="small" variant="tonal" color="primary" class="ml-2 font-weight-medium flow-badge">
        <v-icon icon="mdi-sitemap" start size="14" />
        {{ globalViewModel.currentFlowId || 'default-flow' }}
      </v-chip>

      <v-progress-circular
        v-if="globalViewModel.isGrpcLoading"
        indeterminate
        color="primary"
        size="18"
        width="2"
        class="ml-2"
      />

      <v-spacer></v-spacer>

      <v-tooltip location="bottom">
        <template v-slot:activator="{ props: tooltipProps }">
          <v-chip
            size="x-small"
            variant="flat"
            :color="globalViewModel.isSignalRConnected ? 'success' : 'error'"
            v-bind="tooltipProps"
            class="text-caption font-weight-bold px-2 status-chip"
          >
            <span class="pulse-dot mr-1" :class="globalViewModel.isSignalRConnected ? 'pulse-green' : 'pulse-red'"></span>
            {{ globalViewModel.isSignalRConnected ? 'LIVE' : 'OFFLINE' }}
          </v-chip>
        </template>
        <span>
          SignalR Event Bus: {{ globalViewModel.isSignalRConnected ? 'Connected & listening' : 'Disconnected' }}
        </span>
      </v-tooltip>

      <v-btn
        variant="tonal"
        size="small"
        density="compact"
        @click="toggleLocale"
        class="font-weight-bold text-caption rounded-pill px-3"
      >
        <v-icon icon="mdi-translate" start size="14" />
        {{ locale.toUpperCase() }}
      </v-btn>

      <v-menu offset-y>
        <template v-slot:activator="{ props: menuProps }">
          <v-btn icon v-bind="menuProps" size="small" density="compact" class="user-avatar-btn">
            <v-avatar color="primary" size="28">
              <span class="text-white text-caption font-weight-bold">{{ userInitials }}</span>
            </v-avatar>
          </v-btn>
        </template>

        <v-list density="compact" min-width="190" class="glass-menu rounded-lg">
          <v-list-item>
            <template v-slot:prepend>
              <v-icon icon="mdi-account" class="mr-2" size="18" color="primary" />
            </template>
            <v-list-item-title class="text-caption font-weight-bold">{{ userDisplay }}</v-list-item-title>
            <v-list-item-subtitle class="text-caption text-grey">Keycloak Authenticated</v-list-item-subtitle>
          </v-list-item>

          <v-divider class="my-1 border-opacity-25" />

          <v-list-item @click="handleLogout">
            <template v-slot:prepend>
              <v-icon icon="mdi-logout" class="mr-2" size="18" color="error" />
            </template>
            <v-list-item-title class="text-caption font-weight-medium text-error">{{ t('layout.header.logout') }}</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>
    </div>
  </v-app-bar>
</template>

<style scoped>
.app-header-bar {
  background: rgba(15, 23, 42, 0.9) !important;
  backdrop-filter: blur(12px);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}
.tracking-wide {
  letter-spacing: 0.5px;
}
.logo-box {
  width: 30px;
  height: 30px;
  border-radius: 8px;
  background: rgba(230, 57, 70, 0.15);
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid rgba(230, 57, 70, 0.3);
}
.studio-badge {
  font-size: 9px;
  font-weight: 700;
  color: #E63946;
  background: rgba(230, 57, 70, 0.15);
  border: 1px solid rgba(230, 57, 70, 0.3);
  padding: 1px 5px;
  border-radius: 4px;
}
.flow-badge {
  border: 1px solid rgba(230, 57, 70, 0.25);
  background: rgba(230, 57, 70, 0.08);
}
.status-chip {
  letter-spacing: 0.5px;
}
.pulse-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  display: inline-block;
}
.pulse-green {
  background-color: #4CAF50;
  box-shadow: 0 0 6px #4CAF50;
}
.pulse-red {
  background-color: #EF4444;
  box-shadow: 0 0 6px #EF4444;
}
.glass-menu {
  background: #172136 !important;
  border: 1px solid rgba(255, 255, 255, 0.08);
}
.user-avatar-btn {
  margin-left: 4px;
}
</style>
