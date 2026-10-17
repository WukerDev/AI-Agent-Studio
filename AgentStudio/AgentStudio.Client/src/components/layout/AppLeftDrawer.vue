<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { computed, watch } from 'vue'
import { GlobalViewModel } from '../../GlobalViewModel'
import { ItemType } from '../../clients/generated/agent_node'

const props = defineProps<{
  modelValue: boolean
  tab: string | null
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
}>()

const { t } = useI18n()
const globalViewModel = new GlobalViewModel()

const isOpen = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

watch([isOpen, () => props.tab], ([newIsOpen, newTab]) => {
  if (newIsOpen && newTab === 'files') {
    globalViewModel.loadExplorer('/')
  }
}, { immediate: true })

function handleItemClick(item: any) {
  if (item.type === ItemType.FOLDER) {
    globalViewModel.loadExplorer(item.path)
  }
}
</script>

<template>
  <v-navigation-drawer
      v-model="isOpen"
      location="left"
      elevation="0"
      border="0"
      width="300"
  >
    <v-toolbar density="compact" color="surface">
      <v-toolbar-title class="text-subtitle-2 font-weight-bold">
        {{ props.tab ? t(`layout.sidebar.${props.tab}`) : '' }}
      </v-toolbar-title>
      <v-spacer></v-spacer>
      <v-btn icon="mdi-close" variant="text" size="small" @click="isOpen = false"></v-btn>
    </v-toolbar>

    <v-divider></v-divider>

    <div v-if="props.tab === 'files'" class="fill-height">
      <v-list v-if="globalViewModel.explorerItems.length > 0" density="compact" nav>
        <v-list-item
            v-for="item in globalViewModel.explorerItems"
            :key="item.path"
            :value="item.path"
            @click="handleItemClick(item)"
        >
          <template v-slot:prepend>
            <v-icon :color="globalViewModel.getExplorerItemColor(item.type)">
              {{ globalViewModel.getExplorerItemIcon(item.type) }}
            </v-icon>
          </template>
          <v-list-item-title>{{ item.name }}</v-list-item-title>
        </v-list-item>
      </v-list>

      <v-container v-else class="pa-4">
        <p class="text-body-2 text-medium-emphasis">{{ t('layout.leftDrawer.filesEmpty') }}</p>
      </v-container>
    </div>

    <v-container v-else-if="props.tab === 'agents'" class="pa-4">
      <p class="text-body-2 text-medium-emphasis">{{ t('layout.leftDrawer.agentsEmpty') }}</p>
    </v-container>

    <v-container v-else-if="props.tab === 'settings'" class="pa-4">
      <p class="text-body-2 text-medium-emphasis">{{ t('layout.leftDrawer.settingsEmpty') }}</p>
    </v-container>
  </v-navigation-drawer>
</template>