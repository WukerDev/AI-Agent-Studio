<script setup lang="ts">
import { useNotificationStore } from '../../notifications'

const notifStore = useNotificationStore()

function getIcon(type?: string): string {
  switch (type) {
    case 'success':
      return 'mdi-check-circle'
    case 'error':
      return 'mdi-alert-circle'
    case 'warning':
      return 'mdi-alert'
    default:
      return 'mdi-information'
  }
}

function getColor(type?: string): string {
  switch (type) {
    case 'success':
      return 'success'
    case 'error':
      return 'error'
    case 'warning':
      return 'warning'
    default:
      return 'info'
  }
}
</script>

<template>
  <v-snackbar
    v-model="notifStore.isVisible"
    :color="getColor(notifStore.currentNotification?.type)"
    :timeout="notifStore.currentNotification?.timeout ?? 4000"
    location="top right"
    elevation="4"
    rounded="lg"
  >
    <div class="d-flex align-center">
      <v-icon :icon="getIcon(notifStore.currentNotification?.type)" class="mr-2" />
      <span class="text-body-2 font-weight-medium">
        {{ notifStore.currentNotification?.message }}
      </span>
    </div>

    <template v-slot:actions>
      <v-btn
        variant="text"
        icon="mdi-close"
        size="small"
        @click="notifStore.dismiss()"
      />
    </template>
  </v-snackbar>
</template>
