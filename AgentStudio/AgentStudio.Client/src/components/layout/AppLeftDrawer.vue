<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { computed, watch } from 'vue'
import { GlobalViewModel } from '../../GlobalViewModel'
import { ItemType, NodeType } from '../../clients/generated/agent_node'
import { useConfig } from '../../config'

const props = defineProps<{
  modelValue: boolean
  tab: string | null
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
}>()

const { t } = useI18n()
const globalViewModel = new GlobalViewModel()
const configStore = useConfig()

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

function handleAddNode(type: NodeType) {
  const currentCount = globalViewModel.nodes.length
  globalViewModel.addNode(type, { x: 180 + currentCount * 25, y: 120 + currentCount * 25 })
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

    <div v-else-if="props.tab === 'agents'" class="pa-3">
      <p class="text-caption text-grey mb-3">{{ t('layout.leftDrawer.agentsEmpty') }}</p>

      <div class="d-flex flex-column gap-2">
        <v-card
          variant="tonal"
          color="primary"
          class="pa-3 cursor-pointer hover-card"
          @click="handleAddNode(NodeType.AGENT)"
        >
          <div class="d-flex align-center">
            <v-icon icon="mdi-robot" class="mr-2" />
            <div>
              <div class="text-subtitle-2 font-weight-bold">AI Agent</div>
              <div class="text-caption text-medium-emphasis">Core autonomous reasoning node</div>
            </div>
          </div>
        </v-card>

        <v-card
          variant="tonal"
          color="info"
          class="pa-3 cursor-pointer hover-card"
          @click="handleAddNode(NodeType.MODEL)"
        >
          <div class="d-flex align-center">
            <v-icon icon="mdi-brain" class="mr-2" />
            <div>
              <div class="text-subtitle-2 font-weight-bold">LLM Model</div>
              <div class="text-caption text-medium-emphasis">OpenAI, Ollama & custom endpoint</div>
            </div>
          </div>
        </v-card>

        <v-card
          variant="tonal"
          color="warning"
          class="pa-3 cursor-pointer hover-card"
          @click="handleAddNode(NodeType.PROMPT)"
        >
          <div class="d-flex align-center">
            <v-icon icon="mdi-script-text-outline" class="mr-2" />
            <div>
              <div class="text-subtitle-2 font-weight-bold">System Prompt</div>
              <div class="text-caption text-medium-emphasis">Persona & instructions injection</div>
            </div>
          </div>
        </v-card>

        <v-card
          variant="tonal"
          color="success"
          class="pa-3 cursor-pointer hover-card"
          @click="handleAddNode(NodeType.MCP_CONNECTOR)"
        >
          <div class="d-flex align-center">
            <v-icon icon="mdi-connection" class="mr-2" />
            <div>
              <div class="text-subtitle-2 font-weight-bold">MCP Connector</div>
              <div class="text-caption text-medium-emphasis">External tools & context protocol</div>
            </div>
          </div>
        </v-card>
      </div>
    </div>

    <v-container v-else-if="props.tab === 'settings'" class="pa-4">
      <div class="text-subtitle-2 font-weight-bold mb-3">System Settings & Status</div>

      <v-list density="compact" class="bg-transparent">
        <v-list-item>
          <template v-slot:prepend>
            <v-icon
              :color="globalViewModel.isSignalRConnected ? 'success' : 'error'"
              :icon="globalViewModel.isSignalRConnected ? 'mdi-check-circle' : 'mdi-alert-circle'"
            />
          </template>
          <v-list-item-title class="text-caption">SignalR Realtime</v-list-item-title>
          <v-list-item-subtitle class="text-caption">
            {{ globalViewModel.isSignalRConnected ? 'Connected' : 'Disconnected' }}
          </v-list-item-subtitle>
        </v-list-item>

        <v-list-item>
          <template v-slot:prepend>
            <v-icon color="info" icon="mdi-server" />
          </template>
          <v-list-item-title class="text-caption">Backend gRPC-Web</v-list-item-title>
          <v-list-item-subtitle class="text-caption font-monospace">
            {{ configStore.grpcUrl }}
          </v-list-item-subtitle>
        </v-list-item>

        <v-list-item>
          <template v-slot:prepend>
            <v-icon color="primary" icon="mdi-shield-key" />
          </template>
          <v-list-item-title class="text-caption">Keycloak IAM</v-list-item-title>
          <v-list-item-subtitle class="text-caption font-monospace">
            {{ configStore.authUrl }}/realms/{{ configStore.authRealm }}
          </v-list-item-subtitle>
        </v-list-item>

        <v-list-item v-if="globalViewModel.getFormattedLastSync()">
          <template v-slot:prepend>
            <v-icon color="grey" icon="mdi-clock-outline" />
          </template>
          <v-list-item-title class="text-caption">Last Realtime Sync</v-list-item-title>
          <v-list-item-subtitle class="text-caption">
            {{ globalViewModel.getFormattedLastSync() }}
          </v-list-item-subtitle>
        </v-list-item>
      </v-list>
    </v-container>
  </v-navigation-drawer>
</template>

<style scoped>
.hover-card {
  transition: transform 0.15s ease, box-shadow 0.15s ease;
}
.hover-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.2);
}
</style>
