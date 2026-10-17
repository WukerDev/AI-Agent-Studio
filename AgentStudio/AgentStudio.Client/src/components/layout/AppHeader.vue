<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { AuthViewModel } from '../../AuthViewModel'

const { t } = useI18n()
const authViewModel = new AuthViewModel()

function handleLogout() {
  authViewModel.logout()
}
</script>

<template>
  <v-app-bar elevation="1" density="compact">
    <div class="d-flex align-center px-4 w-100">
      <span class="text-subtitle-1 font-weight-bold">
        {{ t('layout.header.title') }}
      </span>

      <v-spacer></v-spacer>

      <v-menu offset-y>
        <template v-slot:activator="{ props }">
          <v-btn icon v-bind="props">
            <v-avatar color="primary" size="32">
              <span class="text-white text-caption">WK</span>
            </v-avatar>
          </v-btn>
        </template>

        <v-list>
          <v-list-item @click="handleLogout">
            <template v-slot:prepend>
              <v-icon icon="mdi-logout" class="mr-2"></v-icon>
            </template>
            <v-list-item-title>{{ t('layout.header.logout') }}</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>
    </div>
  </v-app-bar>
</template>