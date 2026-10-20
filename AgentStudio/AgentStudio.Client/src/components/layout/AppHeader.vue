<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useTheme } from 'vuetify'
import { AuthViewModel } from '../../AuthViewModel'
import { GlobalViewModel } from '../../GlobalViewModel'

const { t, locale } = useI18n()
const theme = useTheme()
const authViewModel = new AuthViewModel()
const globalViewModel = new GlobalViewModel()

const isDark = computed(() => theme.global.name.value === 'agentStudioDarkTheme')

function toggleTheme() {
  theme.global.name.value = isDark.value ? 'agentStudioLightTheme' : 'agentStudioDarkTheme'
}

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
  <v-app-bar elevation="1" density="compact" color="surface">
    <div class="d-flex align-center px-4 w-100 gap-3">
      <div class="d-flex align-center">
        <v-icon icon="mdi-robot-excited-outline" color="primary" class="mr-2" size="24" />
        <span class="text-subtitle-1 font-weight-bold tracking-wide">
          Agent Studio
        </span>
      </div>

      <v-chip size="small" variant="tonal" color="primary" class="ml-2 font-weight-medium">
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
            class="text-caption font-weight-bold px-2"
          >
            <v-icon
              :icon="globalViewModel.isSignalRConnected ? 'mdi-broadcast' : 'mdi-broadcast-off'"
              start
              size="12"
            />
            {{ globalViewModel.isSignalRConnected ? 'LIVE' : 'OFFLINE' }}
          </v-chip>
        </template>
        <span>
          SignalR Event Bus: {{ globalViewModel.isSignalRConnected ? 'Connected & listening' : 'Disconnected' }}
        </span>
      </v-tooltip>

      <v-btn
        variant="text"
        size="small"
        density="compact"
        @click="toggleLocale"
        class="font-weight-bold"
      >
        <v-icon icon="mdi-translate" start size="16" />
        {{ locale.toUpperCase() }}
      </v-btn>

      <v-btn
        icon
        variant="text"
        size="small"
        density="compact"
        @click="toggleTheme"
        :title="isDark ? 'Switch to Light Mode' : 'Switch to Dark Mode'"
      >
        <v-icon :icon="isDark ? 'mdi-weather-night' : 'mdi-weather-sunny'" size="18" />
      </v-btn>

      <v-menu offset-y>
        <template v-slot:activator="{ props: menuProps }">
          <v-btn icon v-bind="menuProps" size="small" density="compact">
            <v-avatar color="primary" size="30">
              <span class="text-white text-caption font-weight-bold">{{ userInitials }}</span>
            </v-avatar>
          </v-btn>
        </template>

        <v-list density="compact" min-width="180">
          <v-list-item>
            <template v-slot:prepend>
              <v-icon icon="mdi-account" class="mr-2" size="18" />
            </template>
            <v-list-item-title class="text-caption font-weight-bold">{{ userDisplay }}</v-list-item-title>
            <v-list-item-subtitle class="text-caption">Keycloak Authenticated</v-list-item-subtitle>
          </v-list-item>

          <v-divider class="my-1" />

          <v-list-item @click="handleLogout">
            <template v-slot:prepend>
              <v-icon icon="mdi-logout" class="mr-2" size="18" />
            </template>
            <v-list-item-title>{{ t('layout.header.logout') }}</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>
    </div>
  </v-app-bar>
</template>

<style scoped>
.tracking-wide {
  letter-spacing: 0.5px;
}
</style>
