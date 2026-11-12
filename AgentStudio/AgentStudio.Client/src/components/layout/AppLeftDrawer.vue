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

const isSkillDialogOpen = ref(false)
const isEditingSkill = ref(false)
const skillFormName = ref('')
const skillFormDescription = ref('')
const skillFormContent = ref('')

watch([isOpen, () => props.tab], ([newIsOpen, newTab]) => {
  if (newIsOpen && newTab === 'files') {
    globalViewModel.loadExplorer(globalViewModel.currentExplorerPath || '/')
  } else if (newIsOpen && (newTab === 'skills' || newTab === 'agents')) {
    globalViewModel.loadSkills()
  }
}, { immediate: true })

function openCreateSkillDialog() {
  isEditingSkill.value = false
  skillFormName.value = 'my-domain-skill'
  skillFormDescription.value = 'Użyj tego skilla, gdy agent potrzebuje specjalistycznej wiedzy domenowej.'
  skillFormContent.value = '# My Domain Skill\n\n1. Analizuj wymagania krok po kroku.\n2. Zastosuj najlepsze praktyki Microsoft Agent Skills.'
  isSkillDialogOpen.value = true
}

function openEditSkillDialog(skill: any) {
  isEditingSkill.value = true
  skillFormName.value = skill.name || ''
  skillFormDescription.value = skill.description || ''
  skillFormContent.value = skill.content || ''
  isSkillDialogOpen.value = true
}

const skillMdPreview = computed(() => {
  const safeName = (skillFormName.value || 'skill-name')
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9-]+/g, '-')
    .replace(/-{2,}/g, '-')
    .replace(/^-|-$/g, '') || 'skill-name'
  const safeDesc = (skillFormDescription.value || 'Skill description').replace(/\r?\n/g, ' ').trim()
  return `---\nname: ${safeName}\ndescription: ${safeDesc}\n---\n\n${skillFormContent.value || ''}`
})

async function confirmSaveSkill() {
  const name = skillFormName.value.trim()
  if (!name) return
  const saved = await globalViewModel.saveSkill(
    name,
    skillFormDescription.value.trim(),
    skillFormContent.value
  )
  if (saved) {
    isSkillDialogOpen.value = false
  }
}

async function handleDeleteSkill(skillName: string) {
  await globalViewModel.deleteSkill(skillName)
}

function handleAddSkillNodeToCanvas(skill: any) {
  handleAddNode(NodeType.SKILL, `Skill: ${skill.name}`, {
    endpoint: skill.name,
    systemPrompt: skill.description || ''
  })
}

function onSkillDragStart(event: DragEvent, skill: any) {
  onPaletteDragStart(event, {
    type: NodeType.SKILL,
    name: `Skill: ${skill.name}`,
    customConfig: {
      endpoint: skill.name,
      systemPrompt: skill.description || ''
    }
  })
}

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

function openCreateDialog() {
  newItemName.value = 'workflow.agentflow'
  isCreateItemDialogOpen.value = true
}

async function confirmCreateItem() {
  let name = newItemName.value.trim()
  if (!name) return

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
    description: 'Ollama, Gemini, OpenAI & custom',
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
  },
  {
    type: NodeType.SKILL,
    name: 'Agent Skill',
    description: 'Microsoft SKILL.md domain capability',
    icon: 'mdi-book-open-page-variant-outline',
    color: 'cyan'
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
    description: 'gemma3:12b @ localhost:11434',
    icon: 'mdi-laptop',
    color: 'purple-darken-1',
    customConfig: {
      modelConfig: { provider: 'Ollama', modelName: 'gemma3:12b', temperature: 0.5 },
      endpoint: 'http://localhost:11434'
    }
  },
  {
    type: NodeType.MODEL,
    name: 'Google Gemini 2.5 Flash-Lite',
    description: 'gemini-2.5-flash-lite (Cheapest $0.10/1M)',
    icon: 'mdi-google',
    color: 'cyan-darken-1',
    customConfig: {
      modelConfig: { provider: 'Gemini', modelName: 'gemini-2.5-flash-lite', temperature: 0.6 },
      endpoint: 'https://generativelanguage.googleapis.com/v1beta/openai/'
    }
  },
  {
    type: NodeType.MCP_CONNECTOR,
    name: 'Math & System MCP',
    description: 'calculate_expression, get_current_time',
    icon: 'mdi-calculator-variant',
    color: 'green-darken-1',
    customConfig: {
      mcpConfig: { serverUrl: 'builtin://tools', authToken: '', enabledTools: ['calculate_expression', 'get_current_time', 'get_system_info'] }
    }
  },
  {
    type: NodeType.MCP_CONNECTOR,
    name: 'Web Search Tools MCP',
    description: 'search_web, fetch_page',
    icon: 'mdi-web',
    color: 'teal-darken-1',
    customConfig: {
      mcpConfig: { serverUrl: 'builtin://tools', authToken: '', enabledTools: ['search_web', 'fetch_page'] }
    }
  },
  {
    type: NodeType.MCP_CONNECTOR,
    name: 'File System MCP',
    description: 'read_file, write_file, list_dir',
    icon: 'mdi-folder-cog',
    color: 'light-green-darken-2',
    customConfig: {
      mcpConfig: { serverUrl: 'builtin://tools', authToken: '', enabledTools: ['read_file', 'write_file', 'list_dir'] }
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

      <v-btn
          v-if="props.tab === 'files'"
          icon="mdi-plus"
          variant="tonal"
          size="x-small"
          color="primary"
          title="Utwórz nowy plik"
          class="mr-1"
          @click="openCreateDialog"
      />

      <v-btn
          v-if="props.tab === 'skills'"
          icon="mdi-plus"
          variant="tonal"
          size="x-small"
          color="cyan"
          title="Dodaj nowy skill (<skill-name>/SKILL.md)"
          class="mr-1"
          @click="openCreateSkillDialog"
      />

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
              @click="openCreateDialog"
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
              @click="openCreateDialog"
          >
            Utwórz plik
          </v-btn>
        </v-container>
      </div>
    </div>

    <div v-else-if="props.tab === 'skills'" class="fill-height d-flex flex-column">
      <div class="px-3 py-2 bg-grey-darken-4 d-flex align-center justify-space-between border-b">
        <div class="d-flex align-center overflow-hidden">
          <v-icon icon="mdi-folder-key-outline" size="14" class="mr-1.5 text-cyan-lighten-2" />
          <span class="text-caption font-monospace text-truncate text-grey-lighten-1">
            skills/&lt;skill-name&gt;/SKILL.md
          </span>
        </div>
        <div class="d-flex align-center ga-1">
          <v-btn
              variant="text"
              size="x-small"
              icon="mdi-refresh"
              title="Odśwież skille"
              @click="globalViewModel.loadSkills()"
          />
          <v-btn
              variant="tonal"
              color="cyan"
              size="x-small"
              prepend-icon="mdi-folder-plus-outline"
              @click="openCreateSkillDialog"
          >
            Nowy Skill
          </v-btn>
        </div>
      </div>

      <div class="pa-3 flex-grow-1 overflow-y-auto">
        <v-alert
            type="info"
            variant="tonal"
            density="compact"
            color="cyan"
            class="text-caption mb-3"
        >
          Konwencja <strong>Microsoft Agent Skills</strong>: każdy skill posiada osobny folder z plikiem <code>SKILL.md</code> (YAML frontmatter <code>name</code>, <code>description</code> + instrukcje Markdown). Kliknij <strong>+ Węzeł</strong> lub przeciągnij skill na płótno.
        </v-alert>

        <div v-if="globalViewModel.skills.length > 0" class="palette-container ga-2">
          <v-card
              v-for="skill in globalViewModel.skills"
              :key="skill.name"
              variant="outlined"
              color="cyan"
              draggable="true"
              class="pa-2.5 cursor-grab hover-card bg-surface-variant-subtle"
              @dragstart="onSkillDragStart($event, skill)"
          >
            <div class="d-flex align-center justify-space-between mb-1">
              <div class="d-flex align-center overflow-hidden">
                <v-icon icon="mdi-folder" size="16" color="cyan" class="mr-1.5 flex-shrink-0" />
                <span class="text-caption font-weight-bold text-white font-monospace text-truncate">
                  {{ skill.name }}/SKILL.md
                </span>
              </div>
              <v-chip size="x-small" color="cyan" variant="tonal" class="font-monospace">
                SKILL.md
              </v-chip>
            </div>

            <div class="text-caption text-grey-lighten-1 mb-2" style="font-size: 11px; line-height: 1.35;">
              {{ skill.description }}
            </div>

            <div class="d-flex align-center justify-space-between pt-1 border-t">
              <v-btn
                  size="x-small"
                  variant="tonal"
                  color="cyan"
                  prepend-icon="mdi-plus-box-outline"
                  @click.stop="handleAddSkillNodeToCanvas(skill)"
              >
                Dodaj Węzeł
              </v-btn>
              <div class="d-flex align-center ga-1">
                <v-btn
                    icon="mdi-pencil-outline"
                    size="x-small"
                    variant="text"
                    color="grey-lighten-1"
                    title="Edytuj SKILL.md"
                    @click.stop="openEditSkillDialog(skill)"
                />
                <v-btn
                    icon="mdi-delete-outline"
                    size="x-small"
                    variant="text"
                    color="error"
                    title="Usuń folder skilla"
                    @click.stop="handleDeleteSkill(skill.name)"
                />
              </div>
            </div>
          </v-card>
        </div>

        <v-container v-else class="pa-4 text-center">
          <v-icon icon="mdi-book-open-page-variant-outline" size="36" color="cyan" class="mb-2" />
          <p class="text-body-2 text-medium-emphasis">{{ t('layout.leftDrawer.skillsEmpty') }}</p>
          <v-btn
              prepend-icon="mdi-folder-plus-outline"
              variant="tonal"
              size="small"
              color="cyan"
              class="mt-2"
              @click="openCreateSkillDialog"
          >
            Dodaj pierwszy Skill
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
            icon="mdi-file-plus-outline"
            color="primary"
            class="mr-2"
        />
        <span>Nowy plik</span>
      </v-card-title>
      <v-card-text>
        <v-text-field
            v-model="newItemName"
            label="Nazwa pliku z rozszerzeniem"
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

  <v-dialog v-model="isSkillDialogOpen" max-width="640">
    <v-card color="surface" rounded="lg">
      <v-card-title class="d-flex align-center justify-space-between bg-grey-darken-4 py-3 px-4">
        <div class="d-flex align-center">
          <v-icon icon="mdi-book-open-page-variant-outline" color="cyan" class="mr-2" />
          <span class="text-subtitle-1 font-weight-bold">
            {{ isEditingSkill ? `Edytuj Skill: ${skillFormName}/SKILL.md` : 'Nowy Microsoft Agent Skill (SKILL.md)' }}
          </span>
        </div>
        <v-chip size="x-small" color="cyan" variant="tonal" class="font-monospace">
          agentskills.io
        </v-chip>
      </v-card-title>

      <v-divider />

      <v-card-text class="pt-4">
        <v-text-field
            v-model="skillFormName"
            label="Nazwa folderu i skilla (name)"
            placeholder="np. code-review, sql-architect"
            hint="Małe litery, cyfry i myślniki (zostanie utworzony plik skills/<nazwa>/SKILL.md)"
            persistent-hint
            variant="outlined"
            density="compact"
            :disabled="isEditingSkill"
            prepend-inner-icon="mdi-folder-outline"
            class="mb-3"
        />

        <v-textarea
            v-model="skillFormDescription"
            label="Opis skilla (YAML frontmatter: description)"
            placeholder="Opisz kiedy i do czego AI Agent powinien użyć tego skilla..."
            rows="2"
            auto-grow
            variant="outlined"
            density="compact"
            class="mb-3"
        />

        <v-textarea
            v-model="skillFormContent"
            label="Instrukcje Markdown (Body pliku SKILL.md)"
            placeholder="# Instrukcje wykonania skilla..."
            rows="6"
            auto-grow
            variant="outlined"
            density="compact"
            class="font-monospace mb-3"
        />

        <div class="text-caption text-grey-lighten-1 mb-1 font-weight-bold">
          Podgląd generowanego pliku <code>skills/{{ skillFormName || 'skill' }}/SKILL.md</code>:
        </div>
        <pre class="pa-2 rounded bg-grey-darken-4 text-caption font-monospace text-cyan-lighten-3 overflow-x-auto" style="max-height: 150px; border: 1px solid rgba(6, 182, 212, 0.25);">{{ skillMdPreview }}</pre>
      </v-card-text>

      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" size="small" @click="isSkillDialogOpen = false">Anuluj</v-btn>
        <v-btn color="cyan" variant="flat" size="small" prepend-icon="mdi-content-save" @click="confirmSaveSkill">
          Zapisz SKILL.md
        </v-btn>
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