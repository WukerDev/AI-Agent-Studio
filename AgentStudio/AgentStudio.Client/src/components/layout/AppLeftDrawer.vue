<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ref, computed, watch } from 'vue'
import { GlobalViewModel } from '../../GlobalViewModel'
import { ItemType, NodeType } from '../../clients/generated/agent_node'
import { useConfig } from '../../config'
import { useNotificationStore } from '../../notifications'

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
const notifStore = useNotificationStore()

const isOpen = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

const isCreateItemDialogOpen = ref(false)
const newItemName = ref('')
const newItemType = ref<ItemType>(ItemType.FILE)

watch([isOpen, () => props.tab], ([newIsOpen, newTab]) => {
  if (newIsOpen && newTab === 'files') {
    globalViewModel.loadExplorer(globalViewModel.currentExplorerPath || '/')
  }
}, { immediate: true })

function getFlowIdFromItem(item: any): string {
  if (!item) return 'default-flow'
  let id = item.path || ''
  if (id.startsWith('/')) {
    id = id.substring(1)
  }
  const lastSegment = id.split('/').pop() || id
  return lastSegment.replace(/\.agentflow$/, '') || id || 'default-flow'
}

function isItemActive(item: any): boolean {
  if (item.type === ItemType.FOLDER) return false
  const flowId = getFlowIdFromItem(item)
  return globalViewModel.currentFlowId === flowId || globalViewModel.currentFlowId === item.path.replace(/^\//, '')
}

async function handleItemClick(item: any) {
  if (item.type === ItemType.FOLDER) {
    await globalViewModel.loadExplorer(item.path)
    return
  }

  const flowId = getFlowIdFromItem(item)
  if (globalViewModel.currentFlowId === flowId) {
    return
  }

  await globalViewModel.switchWorkflow(flowId)
  notifStore.info(`Switched active workflow to: ${item.name || flowId}`)
}

function handleNavigateUp() {
  const current = globalViewModel.currentExplorerPath
  if (!current || current === '/') return
  const parts = current.split('/').filter(Boolean)
  parts.pop()
  const parent = parts.length === 0 ? '/' : '/' + parts.join('/')
  globalViewModel.loadExplorer(parent)
}

function handleAddNode(type: NodeType, name?: string, customConfig?: any) {
  const currentCount = globalViewModel.nodes.length
  globalViewModel.addNode(
    type,
    { x: 180 + (currentCount % 4) * 200, y: 120 + Math.floor(currentCount / 4) * 120 },
    name,
    customConfig
  )
  notifStore.info(`Added ${name || 'node'} to canvas`)
}

function onPaletteDragStart(event: DragEvent, payload: { type: NodeType; name: string; customConfig?: any }) {
  if (!event.dataTransfer) return
  event.dataTransfer.setData('application/json', JSON.stringify(payload))
  event.dataTransfer.setData('text/plain', payload.name)
  event.dataTransfer.effectAllowed = 'copy'
}

function openCreateDialog(type: ItemType) {
  newItemType.value = type
  newItemName.value = type === ItemType.FOLDER ? 'NewFolder' : 'workflow.agentflow'
  isCreateItemDialogOpen.value = true
}

async function confirmCreateItem() {
  let name = newItemName.value.trim()
  if (!name) return

  if (newItemType.value === ItemType.FILE) {
    if (!name.endsWith('.agentflow')) {
      name = `${name}.agentflow`
    }
    const cleanParent = (globalViewModel.currentExplorerPath || '/').replace(/\/$/, '')
    const path = cleanParent === '' ? `/${name}` : `${cleanParent}/${name}`

    globalViewModel.createExplorerItem(name, ItemType.FILE, globalViewModel.currentExplorerPath || '/')
    const flowId = getFlowIdFromItem({ name, path })
    await globalViewModel.switchWorkflow(flowId)
    await globalViewModel.saveEdges([], flowId)
    notifStore.success(`Created & opened workflow "${name}"`)
  } else {
    globalViewModel.createExplorerItem(name, ItemType.FOLDER, globalViewModel.currentExplorerPath || '/')
    notifStore.success(`Created folder "${name}"`)
  }

  isCreateItemDialogOpen.value = false
  newItemName.value = ''
}

const paletteNodes = [
  {
    type: NodeType.AGENT,
    name: 'AI Agent',
    description: 'Core autonomous reasoning node',
    icon: 'mdi-robot',
    color: 'primary'
  },
  {
    type: NodeType.MODEL,
    name: 'LLM Model',
    description: 'OpenAI, Ollama & custom endpoint',
    icon: 'mdi-brain',
    color: 'info'
  },
  {
    type: NodeType.PROMPT,
    name: 'System Prompt',
    description: 'Persona & instructions injection',
    icon: 'mdi-script-text-outline',
    color: 'warning'
  },
  {
    type: NodeType.MCP_CONNECTOR,
    name: 'MCP Connector',
    description: 'External tools & context protocol',
    icon: 'mdi-connection',
    color: 'success'
  }
]

const paletteArguments = [
  {
    type: NodeType.PROMPT,
    name: 'User Input Argument',
    description: 'Query template: {{input}}',
    icon: 'mdi-variable',
    color: 'amber-darken-1',
    customConfig: {
      systemPrompt: 'Argument input: {{query}}\nAnswer following the defined agent role.'
    }
  },
  {
    type: NodeType.MODEL,
    name: 'OpenAI GPT-4o Preset',
    description: 'gpt-4o, temp 0.7',
    icon: 'mdi-lightning-bolt',
    color: 'blue-darken-1',
    customConfig: {
      modelConfig: { provider: 'OpenAI', modelName: 'gpt-4o', temperature: 0.7 }
    }
  },
  {
    type: NodeType.MODEL,
    name: 'Ollama Local Preset',
    description: 'llama3.2 @ localhost:11434',
    icon: 'mdi-laptop',
    color: 'purple-darken-1',
    customConfig: {
      modelConfig: { provider: 'Ollama', modelName: 'llama3.2', temperature: 0.5 },
      endpoint: 'http://localhost:11434'
    }
  },
  {
    type: NodeType.MCP_CONNECTOR,
    name: 'Web Search Tools MCP',
    description: 'search_web, fetch_page',
    icon: 'mdi-web',
    color: 'teal-darken-1',
    customConfig: {
      mcpConfig: { serverUrl: 'http://localhost:8000/mcp', authToken: '', enabledTools: ['search_web', 'fetch_page'] }
    }
  },
  {
    type: NodeType.MCP_CONNECTOR,
    name: 'File System MCP',
    description: 'read_file, write_file',
    icon: 'mdi-folder-cog',
    color: 'light-green-darken-2',
    customConfig: {
      mcpConfig: { serverUrl: 'http://localhost:8000/mcp', authToken: '', enabledTools: ['read_file', 'write_file', 'list_dir'] }
    }
  }
]
</script>

<template>
  <v-navigation-drawer
      v-model="isOpen"
      location="left"
      elevation="0"
      border="0"
      width="320"
      class="app-left-drawer"
  >
    <v-toolbar density="compact" color="surface" class="px-2">
      <v-toolbar-title class="text-subtitle-2 font-weight-bold">
        {{ props.tab ? t(`layout.sidebar.${props.tab}`) : '' }}
      </v-toolbar-title>
      <v-spacer></v-spacer>

      <v-menu v-if="props.tab === 'files'" offset-y>
        <template v-slot:activator="{ props: menuProps }">
          <v-btn
            icon="mdi-plus"
            variant="tonal"
            size="x-small"
            color="primary"
            v-bind="menuProps"
            title="Utwórz nowy plik lub folder"
            class="mr-1"
          />
        </template>
        <v-list density="compact">
          <v-list-item prepend-icon="mdi-file-plus-outline" @click="openCreateDialog(ItemType.FILE)">
            <v-list-item-title class="text-caption font-weight-medium">Nowy plik</v-list-item-title>
          </v-list-item>
          <v-list-item prepend-icon="mdi-folder-plus-outline" @click="openCreateDialog(ItemType.FOLDER)">
            <v-list-item-title class="text-caption font-weight-medium">Nowy folder</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>

      <v-btn icon="mdi-close" variant="text" size="small" @click="isOpen = false"></v-btn>
    </v-toolbar>

    <v-divider></v-divider>

    <div v-if="props.tab === 'files'" class="fill-height d-flex flex-column">
      <div class="px-3 py-2 bg-grey-darken-4 d-flex align-center justify-space-between border-b">
        <div class="d-flex align-center overflow-hidden">
          <v-btn
            v-if="globalViewModel.currentExplorerPath && globalViewModel.currentExplorerPath !== '/'"
            icon="mdi-arrow-up"
            size="x-small"
            variant="text"
            class="mr-1"
            title="Przejdź w górę"
            @click="handleNavigateUp"
          />
          <v-icon icon="mdi-folder-outline" size="14" class="mr-1 text-grey" />
          <span class="text-caption font-monospace text-truncate text-grey-lighten-1">
            {{ globalViewModel.currentExplorerPath || '/' }}
          </span>
        </div>
        <div class="d-flex align-center">
          <v-btn
            variant="text"
            size="x-small"
            icon="mdi-file-plus"
            title="Nowy plik"
            @click="openCreateDialog(ItemType.FILE)"
          />
          <v-btn
            variant="text"
            size="x-small"
            icon="mdi-folder-plus"
            title="Nowy folder"
            @click="openCreateDialog(ItemType.FOLDER)"
          />
        </div>
      </div>

      <div class="flex-grow-1 overflow-y-auto">
        <v-list v-if="globalViewModel.explorerItems.length > 0" density="compact" nav class="pa-2">
          <v-list-item
            v-for="item in globalViewModel.explorerItems"
            :key="item.path"
            :value="item.path"
            :active="isItemActive(item)"
            active-color="primary"
            class="explorer-list-item mb-1 rounded"
            :class="{ 'active-flow-item': isItemActive(item) }"
            @click="handleItemClick(item)"
          >
            <template v-slot:prepend>
              <v-icon
                :color="isItemActive(item) ? 'primary' : globalViewModel.getExplorerItemColor(item.type)"
                class="mr-2"
                size="18"
              >
                {{ isItemActive(item) ? 'mdi-sitemap' : globalViewModel.getExplorerItemIcon(item.type) }}
              </v-icon>
            </template>
            <v-list-item-title
              class="text-caption"
              :class="{ 'font-weight-bold text-primary': isItemActive(item), 'font-weight-medium': !isItemActive(item) }"
            >
              {{ item.name }}
            </v-list-item-title>
            <template v-slot:append>
              <v-chip
                v-if="item.type === ItemType.FOLDER"
                size="x-small"
                variant="tonal"
                color="warning"
                class="text-caption font-weight-bold"
              >
                DIR
              </v-chip>
              <v-chip
                v-else-if="isItemActive(item)"
                size="x-small"
                variant="flat"
                color="primary"
                class="text-caption font-weight-bold px-1.5"
              >
                <v-icon icon="mdi-check" start size="10" />
                ACTIVE
              </v-chip>
              <v-chip
                v-else
                size="x-small"
                variant="tonal"
                color="grey"
                class="text-caption"
              >
                FLOW
              </v-chip>
            </template>
          </v-list-item>
        </v-list>

        <v-container v-else class="pa-4 text-center">
          <v-icon icon="mdi-folder-open-outline" size="36" color="grey" class="mb-2" />
          <p class="text-body-2 text-medium-emphasis">{{ t('layout.leftDrawer.filesEmpty') }}</p>
          <v-btn
            prepend-icon="mdi-plus"
            variant="tonal"
            size="small"
            color="primary"
            class="mt-2"
            @click="openCreateDialog(ItemType.FILE)"
          >
            Utwórz plik
          </v-btn>
        </v-container>
      </div>
    </div>

    <div v-else-if="props.tab === 'agents'" class="pa-3 fill-height overflow-y-auto">
      <div class="palette-header mb-2 d-flex align-center justify-space-between">
        <span class="text-overline text-grey-lighten-1 font-weight-bold">WĘZŁY (NODES)</span>
        <span class="text-caption text-grey">Przeciągnij na płótno</span>
      </div>

      <div class="palette-container ga-3 mb-4">
        <v-card
          v-for="item in paletteNodes"
          :key="item.name"
          variant="tonal"
          :color="item.color"
          draggable="true"
          class="pa-2 cursor-grab hover-card palette-card"
          @dragstart="onPaletteDragStart($event, item)"
          @click="handleAddNode(item.type, item.name)"
        >
          <div class="d-flex align-center justify-space-between">
            <div class="d-flex align-center overflow-hidden">
              <v-icon icon="mdi-drag-vertical" size="14" class="drag-handle-icon mr-1 text-grey" />
              <v-icon :icon="item.icon" class="mr-2 flex-shrink-0" size="18" />
              <div>
                <div class="text-caption font-weight-bold">{{ item.name }}</div>
                <div class="text-caption text-medium-emphasis" style="font-size: 10px;">{{ item.description }}</div>
              </div>
            </div>
            <v-icon icon="mdi-plus" size="14" class="text-grey" />
          </div>
        </v-card>
      </div>

      <div class="palette-header mb-2 d-flex align-center justify-space-between">
        <span class="text-overline text-grey-lighten-1 font-weight-bold">ARGUMENTY I PARAMETRY</span>
        <v-chip size="x-small" color="secondary" variant="flat">PRESETS</v-chip>
      </div>

      <div class="palette-container ga-3">
        <v-card
          v-for="item in paletteArguments"
          :key="item.name"
          variant="outlined"
          :color="item.color"
          draggable="true"
          class="pa-2 cursor-grab hover-card palette-card bg-surface-variant-subtle"
          @dragstart="onPaletteDragStart($event, item)"
          @click="handleAddNode(item.type, item.name, item.customConfig)"
        >
          <div class="d-flex align-center justify-space-between">
            <div class="d-flex align-center overflow-hidden">
              <v-icon icon="mdi-drag-vertical" size="14" class="drag-handle-icon mr-1 text-grey" />
              <v-icon :icon="item.icon" class="mr-2 flex-shrink-0" size="16" />
              <div>
                <div class="text-caption font-weight-bold">{{ item.name }}</div>
                <div class="text-caption text-grey" style="font-size: 10px;">{{ item.description }}</div>
              </div>
            </div>
            <v-chip size="x-small" variant="tonal" :color="item.color">ARG</v-chip>
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

  <v-dialog v-model="isCreateItemDialogOpen" max-width="400">
    <v-card color="surface" rounded="lg">
      <v-card-title class="d-flex align-center">
        <v-icon
          :icon="newItemType === ItemType.FOLDER ? 'mdi-folder-plus-outline' : 'mdi-file-plus-outline'"
          color="primary"
          class="mr-2"
        />
        <span>{{ newItemType === ItemType.FOLDER ? 'Nowy folder' : 'Nowy plik' }}</span>
      </v-card-title>
      <v-card-text>
        <v-text-field
          v-model="newItemName"
          :label="newItemType === ItemType.FOLDER ? 'Nazwa folderu' : 'Nazwa pliku z rozszerzeniem'"
          placeholder="np. workflow.agentflow"
          variant="outlined"
          density="compact"
          autofocus
          @keydown.enter="confirmCreateItem"
        />
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" size="small" @click="isCreateItemDialogOpen = false">Anuluj</v-btn>
        <v-btn color="primary" variant="flat" size="small" @click="confirmCreateItem">Utwórz</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.app-left-drawer {
  background: rgba(15, 23, 42, 0.98) !important;
  backdrop-filter: blur(16px);
  border-right: 1px solid rgba(255, 255, 255, 0.08);
}
.palette-container {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.palette-card {
  margin-bottom: 4px;
}
.hover-card {
  transition: transform 0.15s ease, box-shadow 0.15s ease;
}
.hover-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.2);
}
.cursor-grab {
  cursor: grab;
}
.cursor-grab:active {
  cursor: grabbing;
}
.drag-handle-icon {
  opacity: 0.5;
}
.hover-card:hover .drag-handle-icon {
  opacity: 1;
}
.explorer-list-item {
  transition: all 0.15s ease;
  border: 1px solid transparent;
  cursor: pointer;
  user-select: none;
}
.explorer-list-item:hover {
  background-color: rgba(255, 255, 255, 0.06);
  border-color: rgba(255, 255, 255, 0.12);
}
.active-flow-item {
  background-color: rgba(230, 57, 70, 0.15) !important;
  border-color: rgba(230, 57, 70, 0.45) !important;
}
</style>
