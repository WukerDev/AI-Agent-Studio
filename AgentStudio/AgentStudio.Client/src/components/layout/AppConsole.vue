<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { computed } from 'vue'

const props = defineProps<{
  modelValue: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
}>()

const { t } = useI18n()

const isOpen = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

const closeConsole = (): void => {
  isOpen.value = false
}
</script>

<template>
  <v-navigation-drawer
      v-model="isOpen"
      location="bottom"
      temporary
      elevation="4"
      height="300"
  >
    <v-toolbar density="compact" color="surface">
      <v-toolbar-title class="text-subtitle-2">
        {{ t('layout.console.title') }}
      </v-toolbar-title>
      <v-spacer></v-spacer>
      <v-btn icon="mdi-close" variant="text" size="small" @click="closeConsole"></v-btn>
    </v-toolbar>

    <v-container fluid class="fill-height align-start bg-black text-white pa-4 font-monospace">
      <div>> dotnet run AgentStudio.dll...</div>
    </v-container>
  </v-navigation-drawer>
</template>