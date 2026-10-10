<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <v-app-bar elevation="1" density="compact">
    <div class="d-flex align-center px-4 w-100">
      <span class="text-subtitle-1 font-weight-bold">
        {{ t('layout.header.title') }}
      </span>

      <v-spacer></v-spacer>

      <v-btn icon>
        <v-avatar color="primary" size="32">
          <span class="text-white text-caption">WK</span>
        </v-avatar>
      </v-btn>
    </div>
  </v-app-bar>
</template>