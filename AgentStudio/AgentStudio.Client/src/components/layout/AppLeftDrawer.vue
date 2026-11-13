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
    icon: 'mdi-robot-outline',
    accent: '#E63946'
  },
  {
    type: NodeType.MODEL,
    name: 'LLM Model',
    description: 'Ollama, Gemini, OpenAI & custom',
    icon: 'mdi-brain',
    accent: '#3B82F6'
  },
  {
    type: NodeType.PROMPT,
    name: 'System Prompt',
    description: 'Persona & instructions injection',
    icon: 'mdi-script-text-outline',
    accent: '#F59E0B'
  },
  {
    type: NodeType.MCP_CONNECTOR,
    name: 'MCP Connector',
    description: 'External tools & context protocol',
    icon: 'mdi-connection',
    accent: '#10B981'
  },
  {
    type: NodeType.SKILL,
    name: 'Agent Skill',
    description: 'Microsoft SKILL.md capability',
    icon: 'mdi-book-open-page-variant-outline',
    accent: '#06B6D4'
  }
]

const paletteArguments = [
  {
    type: NodeType.PROMPT,
    name: 'User Input Argument',
    description: 'Query template: {{input}}',
    icon: 'mdi-variable',
    accent: '#F59E0B',
    tag: 'PROMPT',
    customConfig: {
      systemPrompt: 'Argument input: {{query}}\nAnswer following the defined agent role.'
    }
  },
  {
    type: NodeType.MODEL,
    name: 'OpenAI GPT-4o Preset',
    description: 'gpt-4o · temp 0.7',
    icon: 'mdi-lightning-bolt-outline',
    accent: '#3B82F6',
    tag: 'MODEL',
    customConfig: {
      modelConfig: { provider: 'OpenAI', modelName: 'gpt-4o', temperature: 0.7 }
    }
  },
  {
    type: NodeType.MODEL,
    name: 'Ollama Local Preset',
    description: 'gemma3:12b @ localhost:11434',
    icon: 'mdi-laptop',
    accent: '#8B5CF6',
    tag: 'MODEL',
    customConfig: {
      modelConfig: { provider: 'Ollama', modelName: 'gemma3:12b', temperature: 0.5 },
      endpoint: 'http://localhost:11434'
    }
  },
  {
    type: NodeType.MODEL,
    name: 'Gemini 3.5 Flash-Lite',
    description: 'gemini-3.5-flash-lite (Fast & low-cost)',
    icon: 'mdi-google',
    accent: '#06B6D4',
    tag: 'MODEL',
    customConfig: {
      modelConfig: { provider: 'Gemini', modelName: 'gemini-3.5-flash-lite', temperature: 0.6 },
      endpoint: 'https://generativelanguage.googleapis.com/v1beta/openai/'
    }
  },
  {
    type: NodeType.MCP_CONNECTOR,
    name: 'Math & System MCP',
    description: 'calculate_expression, get_current_time',
    icon: 'mdi-calculator-variant-outline',
    accent: '#10B981',
    tag: 'MCP',
    customConfig: {
      mcpConfig: { serverUrl: 'builtin://tools', authToken: '', enabledTools: ['calculate_expression', 'get_current_time', 'get_system_info'] }
    }
  },
  {
    type: NodeType.MCP_CONNECTOR,
    name: 'Web Search Tools MCP',
    description: 'search_web, fetch_page',
    icon: 'mdi-web',
    accent: '#10B981',
    tag: 'MCP',
    customConfig: {
      mcpConfig: { serverUrl: 'builtin://tools', authToken: '', enabledTools: ['search_web', 'fetch_page'] }
    }
  },
  {
    type: NodeType.MCP_CONNECTOR,
    name: 'File System MCP',
    description: 'read_file, write_file, list_dir',
    icon: 'mdi-folder-cog-outline',
    accent: '#10B981',
    tag: 'MCP',
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
    width="336"
    class="app-left-drawer"
  >
    <!-- Clean Non-Truncating Drawer Header -->
    <div class="drawer-header d-flex align-center justify-space-between px-3">
      <span class="drawer-header-title text-truncate">
        {{ props.tab ? t(`layout.sidebar.${props.tab}`) : '' }}
      </span>

      <div class="d-flex align-center ga-1 flex-shrink-0">
        <v-btn
          v-if="props.tab === 'files'"
          icon="mdi-plus"
          variant="text"
          size="x-small"
          color="grey-lighten-1"
          title="Utwórz nowy plik"
          @click="openCreateDialog"
        />

        <v-btn
          v-if="props.tab === 'skills'"
          icon="mdi-refresh"
          variant="text"
          size="x-small"
          color="grey-lighten-1"
          title="Odśwież skille"
          @click="globalViewModel.loadSkills()"
        />

        <v-btn
          icon="mdi-close"
          variant="text"
          size="x-small"
          color="grey-lighten-1"
          title="Zamknij panel"
          @click="isOpen = false"
        />
      </div>
    </div>

    <!-- TAB 1: FILES / WORKFLOWS -->
    <div v-if="props.tab === 'files'" class="drawer-body d-flex flex-column">
      <div class="sub-toolbar d-flex align-center justify-space-between px-3 py-2">
        <div class="d-flex align-center ga-1.5 overflow-hidden">
          <v-btn
            v-if="globalViewModel.currentExplorerPath && globalViewModel.currentExplorerPath !== '/'"
            icon="mdi-arrow-up"
            size="x-small"
            variant="text"
            color="grey-lighten-1"
            title="Przejdź w górę"
            @click="handleNavigateUp"
          />
          <v-icon icon="mdi-folder-outline" size="14" color="#64748B" />
          <span class="text-caption font-monospace text-truncate" style="color: #94A3B8;">
            {{ globalViewModel.currentExplorerPath || '/' }}
          </span>
        </div>
        <button type="button" class="action-btn-subtle" @click="openCreateDialog">
          <v-icon icon="mdi-plus" size="13" />
          <span>Nowy</span>
        </button>
      </div>

      <div class="flex-grow-1 overflow-y-auto pa-2">
        <div v-if="globalViewModel.explorerItems.length > 0" class="d-flex flex-column ga-1">
          <div
            v-for="item in globalViewModel.explorerItems"
            :key="item.path"
            class="file-item d-flex align-center justify-space-between px-2.5 py-2"
            :class="{ 'file-item-active': isItemActive(item) }"
            @click="handleItemClick(item)"
          >
            <div class="d-flex align-center ga-2 overflow-hidden mr-2">
              <v-icon
                :icon="isItemActive(item) ? 'mdi-sitemap' : globalViewModel.getExplorerItemIcon(item.type)"
                :color="isItemActive(item) ? '#60A5FA' : (item.type === ItemType.FOLDER ? '#F59E0B' : '#64748B')"
                size="16"
                class="flex-shrink-0"
              />
              <span class="file-item-name text-truncate">{{ item.name }}</span>
            </div>

            <span v-if="item.type === ItemType.FOLDER" class="meta-tag meta-tag-dir">DIR</span>
            <span v-else-if="isItemActive(item)" class="meta-tag meta-tag-active">ACTIVE</span>
            <span v-else class="meta-tag">FLOW</span>
          </div>
        </div>

        <div v-else class="pa-6 text-center">
          <v-icon icon="mdi-folder-open-outline" size="32" color="#475569" class="mb-2" />
          <p class="text-caption" style="color: #94A3B8;">{{ t('layout.leftDrawer.filesEmpty') }}</p>
          <button type="button" class="action-btn-primary mt-3" @click="openCreateDialog">
            <v-icon icon="mdi-plus" size="14" />
            <span>Utwórz plik</span>
          </button>
        </div>
      </div>
    </div>

    <!-- TAB 2: SKILLS EXPLORER (SKILL.md) -->
    <div v-else-if="props.tab === 'skills'" class="drawer-body d-flex flex-column">
      <div class="sub-toolbar d-flex align-center justify-space-between px-3 py-2">
        <div class="d-flex align-center ga-1.5 overflow-hidden">
          <v-icon icon="mdi-folder-key-outline" size="14" color="#22D3EE" />
          <span class="text-caption font-monospace" style="color: #94A3B8;">skills/*/SKILL.md</span>
          <span class="count-pill">{{ globalViewModel.skills.length }}</span>
        </div>
        <button type="button" class="action-btn-cyan" @click="openCreateSkillDialog">
          <v-icon icon="mdi-plus" size="13" />
          <span>Nowy Skill</span>
        </button>
      </div>

      <div class="pa-3 flex-grow-1 overflow-y-auto">
        <div class="skill-hint-box mb-3">
          Standard <strong>Microsoft Agent Skills</strong> (<code>SKILL.md</code>). Kliknij <strong>+ Węzeł</strong> lub przeciągnij na płótno.
        </div>

        <div v-if="globalViewModel.skills.length > 0" class="d-flex flex-column ga-2">
          <div
            v-for="skill in globalViewModel.skills"
            :key="skill.name"
            draggable="true"
            class="skill-card cursor-grab"
            @dragstart="onSkillDragStart($event, skill)"
          >
            <div class="d-flex align-center justify-space-between ga-2 mb-1.5">
              <div class="d-flex align-center ga-2 overflow-hidden">
                <v-icon icon="mdi-folder-outline" size="15" color="#22D3EE" class="flex-shrink-0" />
                <span class="skill-card-title font-monospace text-truncate" :title="`${skill.name}/SKILL.md`">
                  {{ skill.name }}
                </span>
              </div>
              <span class="meta-tag meta-tag-cyan font-monospace flex-shrink-0">SKILL.md</span>
            </div>

            <p class="skill-card-desc mb-2.5" :title="skill.description">
              {{ skill.description }}
            </p>

            <div class="skill-card-footer d-flex align-center justify-space-between pt-2">
              <button
                type="button"
                class="skill-add-btn"
                @click.stop="handleAddSkillNodeToCanvas(skill)"
              >
                <v-icon icon="mdi-plus" size="13" />
                <span>Dodaj Węzeł</span>
              </button>

              <div class="d-flex align-center ga-1">
                <button
                  type="button"
                  class="icon-btn-subtle"
                  title="Edytuj SKILL.md"
                  @click.stop="openEditSkillDialog(skill)"
                >
                  <v-icon icon="mdi-pencil-outline" size="14" />
                </button>
                <button
                  type="button"
                  class="icon-btn-subtle icon-btn-danger"
                  title="Usuń folder skilla"
                  @click.stop="handleDeleteSkill(skill.name)"
                >
                  <v-icon icon="mdi-delete-outline" size="14" />
                </button>
              </div>
            </div>
          </div>
        </div>

        <div v-else class="pa-6 text-center">
          <v-icon icon="mdi-book-open-page-variant-outline" size="32" color="#22D3EE" class="mb-2" />
          <p class="text-caption" style="color: #94A3B8;">{{ t('layout.leftDrawer.skillsEmpty') }}</p>
          <button type="button" class="action-btn-cyan mt-3" @click="openCreateSkillDialog">
            <v-icon icon="mdi-plus" size="14" />
            <span>Dodaj pierwszy Skill</span>
          </button>
        </div>
      </div>
    </div>

    <!-- TAB 3: NODES & ARGUMENTS PALETTE -->
    <div v-else-if="props.tab === 'agents'" class="drawer-body pa-3 overflow-y-auto">
      <div class="section-header d-flex align-center justify-space-between mb-2">
        <span class="section-title">WĘZŁY (NODES)</span>
        <span class="section-subtitle">Przeciągnij lub kliknij</span>
      </div>

      <div class="d-flex flex-column ga-2 mb-5">
        <div
          v-for="item in paletteNodes"
          :key="item.name"
          draggable="true"
          class="node-palette-item cursor-grab"
          :style="{ '--node-accent': item.accent }"
          @dragstart="onPaletteDragStart($event, item)"
          @click="handleAddNode(item.type, item.name)"
        >
          <div class="d-flex align-center justify-space-between ga-2">
            <div class="d-flex align-center ga-2.5 overflow-hidden">
              <div class="node-palette-icon-box flex-shrink-0">
                <v-icon :icon="item.icon" size="16" :color="item.accent" />
              </div>
              <div class="overflow-hidden">
                <div class="node-palette-title text-truncate">{{ item.name }}</div>
                <div class="node-palette-desc text-truncate">{{ item.description }}</div>
              </div>
            </div>
            <v-icon icon="mdi-plus" size="15" color="#64748B" class="flex-shrink-0" />
          </div>
        </div>
      </div>

      <div class="section-header d-flex align-center justify-space-between mb-2">
        <span class="section-title">GOTOWE PRESETY</span>
        <span class="meta-tag">7 PRESETS</span>
      </div>

      <div class="d-flex flex-column ga-1.5">
        <div
          v-for="item in paletteArguments"
          :key="item.name"
          draggable="true"
          class="preset-palette-item cursor-grab"
          @dragstart="onPaletteDragStart($event, item)"
          @click="handleAddNode(item.type, item.name, item.customConfig)"
        >
          <div class="d-flex align-center justify-space-between ga-2">
            <div class="d-flex align-center ga-2 overflow-hidden">
              <div class="preset-icon-dot flex-shrink-0" :style="{ color: item.accent }">
                <v-icon :icon="item.icon" size="15" :color="item.accent" />
              </div>
              <div class="overflow-hidden">
                <div class="preset-palette-title text-truncate">{{ item.name }}</div>
                <div class="preset-palette-desc font-monospace text-truncate">{{ item.description }}</div>
              </div>
            </div>
            <span class="meta-tag flex-shrink-0">{{ item.tag }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- TAB 4: SYSTEM SETTINGS -->
    <div v-else-if="props.tab === 'settings'" class="drawer-body pa-3 overflow-y-auto">
      <div class="section-header mb-2">
        <span class="section-title">STATUS POŁĄCZEŃ</span>
      </div>

      <div class="d-flex flex-column ga-2">
        <div class="settings-card d-flex align-center ga-3">
          <v-icon
            :color="globalViewModel.isSignalRConnected ? 'success' : 'error'"
            :icon="globalViewModel.isSignalRConnected ? 'mdi-check-circle-outline' : 'mdi-alert-circle-outline'"
            size="18"
          />
          <div class="overflow-hidden">
            <div class="node-palette-title">SignalR Realtime</div>
            <div class="node-palette-desc">
              {{ globalViewModel.isSignalRConnected ? 'Connected & streaming' : 'Disconnected' }}
            </div>
          </div>
        </div>

        <div class="settings-card d-flex align-center ga-3">
          <v-icon color="info" icon="mdi-server-network" size="18" />
          <div class="overflow-hidden">
            <div class="node-palette-title">Backend gRPC-Web</div>
            <div class="preset-palette-desc font-monospace text-truncate">{{ configStore.grpcUrl }}</div>
          </div>
        </div>

        <div class="settings-card d-flex align-center ga-3">
          <v-icon color="warning" icon="mdi-shield-key-outline" size="18" />
          <div class="overflow-hidden">
            <div class="node-palette-title">Keycloak IAM</div>
            <div class="preset-palette-desc font-monospace text-truncate">
              {{ configStore.authUrl }}/realms/{{ configStore.authRealm }}
            </div>
          </div>
        </div>

        <div v-if="globalViewModel.getFormattedLastSync()" class="settings-card d-flex align-center ga-3">
          <v-icon color="#94A3B8" icon="mdi-clock-outline" size="18" />
          <div class="overflow-hidden">
            <div class="node-palette-title">Last Realtime Sync</div>
            <div class="node-palette-desc">{{ globalViewModel.getFormattedLastSync() }}</div>
          </div>
        </div>
      </div>
    </div>
  </v-navigation-drawer>

  <!-- Create Workflow File Dialog -->
  <v-dialog v-model="isCreateItemDialogOpen" max-width="400">
    <v-card class="solid-dialog-card" rounded="lg">
      <div class="dialog-header d-flex align-center justify-space-between px-4 py-3">
        <div class="d-flex align-center ga-2">
          <v-icon icon="mdi-file-plus-outline" color="info" size="18" />
          <span class="text-subtitle-2 font-weight-bold text-white">Nowy przepływ (.agentflow)</span>
        </div>
        <v-btn icon="mdi-close" variant="text" size="x-small" color="grey" @click="isCreateItemDialogOpen = false" />
      </div>
      <div class="pa-4">
        <div class="as-field-group mb-0">
          <label class="as-field-label">Nazwa pliku (.agentflow)</label>
          <input
            v-model="newItemName"
            type="text"
            class="as-input font-monospace"
            placeholder="np. workflow.agentflow"
            @keydown.enter="confirmCreateItem"
          />
        </div>
      </div>
      <div class="dialog-footer d-flex align-center justify-end ga-2 px-4 py-3">
        <v-btn variant="text" size="small" color="grey-lighten-1" @click="isCreateItemDialogOpen = false">Anuluj</v-btn>
        <v-btn color="info" variant="flat" size="small" @click="confirmCreateItem">Utwórz</v-btn>
      </div>
    </v-card>
  </v-dialog>

  <!-- Create / Edit Skill Dialog -->
  <v-dialog v-model="isSkillDialogOpen" max-width="640">
    <v-card class="solid-dialog-card" rounded="lg">
      <div class="dialog-header d-flex align-center justify-space-between px-4 py-3">
        <div class="d-flex align-center ga-2">
          <v-icon icon="mdi-book-open-page-variant-outline" color="#22D3EE" size="18" />
          <span class="text-subtitle-2 font-weight-bold text-white">
            {{ isEditingSkill ? `Edytuj Skill: ${skillFormName}/SKILL.md` : 'Nowy Microsoft Agent Skill (SKILL.md)' }}
          </span>
        </div>
        <span class="meta-tag meta-tag-cyan font-monospace">agentskills.io</span>
      </div>

      <div class="pa-4">
        <div class="as-field-group">
          <label class="as-field-label">Nazwa folderu i skilla (name)</label>
          <input
            v-model="skillFormName"
            type="text"
            class="as-input font-monospace"
            placeholder="np. code-review, sql-architect"
            :disabled="isEditingSkill"
          />
          <div class="as-field-hint">Małe litery, cyfry i myślniki (plik skills/&lt;nazwa&gt;/SKILL.md)</div>
        </div>

        <div class="as-field-group">
          <label class="as-field-label">Opis skilla (YAML frontmatter: description)</label>
          <textarea
            v-model="skillFormDescription"
            rows="2"
            class="as-textarea"
            placeholder="Opisz kiedy i do czego AI Agent powinien użyć tego skilla..."
          />
        </div>

        <div class="as-field-group">
          <label class="as-field-label">Instrukcje Markdown (Body pliku SKILL.md)</label>
          <textarea
            v-model="skillFormContent"
            rows="6"
            class="as-textarea font-monospace"
            placeholder="# Instrukcje wykonania skilla..."
          />
        </div>

        <div class="text-caption mb-1 font-weight-bold" style="color: #94A3B8;">
          Podgląd generowanego pliku <code>skills/{{ skillFormName || 'skill' }}/SKILL.md</code>:
        </div>
        <pre class="skill-preview-box pa-2 rounded text-caption font-monospace overflow-x-auto">{{ skillMdPreview }}</pre>
      </div>

      <div class="dialog-footer d-flex align-center justify-end ga-2 px-4 py-3">
        <v-btn variant="text" size="small" color="grey-lighten-1" @click="isSkillDialogOpen = false">Anuluj</v-btn>
        <v-btn color="info" variant="flat" size="small" prepend-icon="mdi-content-save-outline" @click="confirmSaveSkill">
          Zapisz SKILL.md
        </v-btn>
      </div>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.app-left-drawer {
  background-color: #111827 !important;
  border-right: 1px solid #1E293B !important;
}

.drawer-header {
  height: 44px;
  background-color: #0D131F;
  border-bottom: 1px solid #1E293B;
}

.drawer-header-title {
  font-size: 13px;
  font-weight: 700;
  color: #F8FAFC;
  letter-spacing: -0.01em;
}

.drawer-body {
  height: calc(100% - 44px);
}

.sub-toolbar {
  background-color: #0D131F;
  border-bottom: 1px solid #1E293B;
}

.count-pill {
  background-color: #1E293B;
  color: #E2E8F0;
  font-size: 10px;
  font-weight: 600;
  padding: 1px 6px;
  border-radius: 4px;
}

/* File Items */
.file-item {
  border-radius: 6px;
  border: 1px solid transparent;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.file-item:hover {
  background-color: #161F30;
  border-color: #26334D;
}

.file-item-active {
  background-color: #172338 !important;
  border-color: #2E476E !important;
}

.file-item-name {
  font-size: 12.5px;
  font-weight: 500;
  color: #E2E8F0;
}

.file-item-active .file-item-name {
  font-weight: 600;
  color: #F8FAFC;
}

/* Section Headers */
.section-title {
  font-size: 11px;
  font-weight: 700;
  color: #94A3B8;
  letter-spacing: 0.05em;
}

.section-subtitle {
  font-size: 11px;
  color: #64748B;
}

/* Node Palette Cards */
.node-palette-item {
  background-color: #161F30;
  border: 1px solid #26334D;
  border-left: 3px solid var(--node-accent, #3B82F6);
  border-radius: 6px;
  padding: 9px 11px;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.node-palette-item:hover {
  background-color: #1B273B;
  border-color: #36486B;
  border-left-color: var(--node-accent, #3B82F6);
}

.node-palette-icon-box {
  width: 30px;
  height: 30px;
  border-radius: 6px;
  background-color: #111827;
  border: 1px solid #26334D;
  display: flex;
  align-items: center;
  justify-content: center;
}

.node-palette-title {
  font-size: 12.5px;
  font-weight: 600;
  color: #F8FAFC;
  line-height: 1.25;
}

.node-palette-desc {
  font-size: 11px;
  color: #94A3B8;
  line-height: 1.3;
  margin-top: 1px;
}

.ga-2\.5 {
  gap: 10px;
}

/* Preset Palette Cards (Clean & Unified, No Rainbow Borders) */
.preset-palette-item {
  background-color: #141C2B;
  border: 1px solid #1E293B;
  border-radius: 6px;
  padding: 8px 10px;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.preset-palette-item:hover {
  background-color: #192336;
  border-color: #334155;
}

.preset-icon-dot {
  width: 26px;
  height: 26px;
  border-radius: 5px;
  background-color: #0D131F;
  border: 1px solid #1E293B;
  display: flex;
  align-items: center;
  justify-content: center;
}

.preset-palette-title {
  font-size: 12px;
  font-weight: 600;
  color: #E2E8F0;
  line-height: 1.25;
}

.preset-palette-desc {
  font-size: 10.5px;
  color: #64748B;
  line-height: 1.3;
  margin-top: 1px;
}

/* Skills Explorer Cards */
.skill-hint-box {
  background-color: #141C2B;
  border: 1px solid #1E293B;
  border-left: 3px solid #06B6D4;
  border-radius: 6px;
  padding: 8px 10px;
  font-size: 11.5px;
  line-height: 1.4;
  color: #94A3B8;
}

.skill-hint-box code {
  color: #67E8F9;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

.skill-card {
  background-color: #161F30;
  border: 1px solid #26334D;
  border-radius: 7px;
  padding: 11px 12px;
  transition: border-color 0.12s ease, background-color 0.12s ease;
}

.skill-card:hover {
  background-color: #192438;
  border-color: #38BDF8;
}

.skill-card-title {
  font-size: 12.5px;
  font-weight: 700;
  color: #F8FAFC;
}

.skill-card-desc {
  font-size: 11.5px;
  line-height: 1.4;
  color: #94A3B8;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin: 0;
}

.skill-card-footer {
  border-top: 1px solid #1E293B;
}

.skill-add-btn {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  background-color: #132A38;
  border: 1px solid #155E75;
  color: #67E8F9;
  font-size: 11px;
  font-weight: 600;
  padding: 4px 10px;
  border-radius: 5px;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.skill-add-btn:hover {
  background-color: #163749;
  border-color: #0891B2;
}

.icon-btn-subtle {
  width: 26px;
  height: 26px;
  border-radius: 5px;
  background-color: transparent;
  border: 1px solid transparent;
  color: #94A3B8;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: background-color 0.12s ease, color 0.12s ease, border-color 0.12s ease;
}

.icon-btn-subtle:hover {
  background-color: #1E293B;
  border-color: #334155;
  color: #F8FAFC;
}

.icon-btn-danger:hover {
  background-color: #2A1215;
  border-color: #7F1D1D;
  color: #F87171;
}

/* Badges & Action Buttons */
.meta-tag {
  font-size: 9.5px;
  font-weight: 600;
  color: #94A3B8;
  background-color: #0D131F;
  border: 1px solid #26334D;
  padding: 1px 6px;
  border-radius: 4px;
  letter-spacing: 0.03em;
}

.meta-tag-active {
  background-color: #1E3A5F;
  border-color: #2563EB;
  color: #93C5FD;
}

.meta-tag-dir {
  background-color: #271E12;
  border-color: #78350F;
  color: #FCD34D;
}

.meta-tag-cyan {
  background-color: #0F2937;
  border-color: #155E75;
  color: #67E8F9;
}

.action-btn-subtle,
.action-btn-cyan,
.action-btn-primary {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  font-weight: 600;
  padding: 3px 9px;
  border-radius: 5px;
  cursor: pointer;
  transition: background-color 0.12s ease, border-color 0.12s ease;
}

.action-btn-subtle {
  background-color: #161F30;
  border: 1px solid #283548;
  color: #E2E8F0;
}

.action-btn-subtle:hover {
  background-color: #1E293B;
  border-color: #3B4D6B;
}

.action-btn-cyan {
  background-color: #132A38;
  border: 1px solid #155E75;
  color: #67E8F9;
}

.action-btn-cyan:hover {
  background-color: #163749;
  border-color: #0891B2;
}

.action-btn-primary {
  background-color: #1E3A5F;
  border: 1px solid #2563EB;
  color: #93C5FD;
}

/* Settings Cards */
.settings-card {
  background-color: #141C2B;
  border: 1px solid #1E293B;
  border-radius: 6px;
  padding: 10px 12px;
}

/* Solid Dialogs */
.solid-dialog-card {
  background-color: #111827 !important;
  border: 1px solid #283548 !important;
}

.dialog-header {
  background-color: #0D131F;
  border-bottom: 1px solid #1E293B;
}

.dialog-footer {
  background-color: #0D131F;
  border-top: 1px solid #1E293B;
}

.skill-preview-box {
  background-color: #090D16;
  border: 1px solid #1E293B;
  color: #67E8F9;
  max-height: 150px;
}

.cursor-grab {
  cursor: grab;
}

.cursor-grab:active {
  cursor: grabbing;
}
</style>
