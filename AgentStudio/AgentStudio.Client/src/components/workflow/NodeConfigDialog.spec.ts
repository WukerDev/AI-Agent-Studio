import { describe, it, expect, beforeAll } from 'vitest'
import { mount } from '@vue/test-utils'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import NodeConfigDialog from './NodeConfigDialog.vue'
import { NodeType } from '../../clients/generated/agent_node'

beforeAll(() => {
    if (typeof window !== 'undefined' && !window.visualViewport) {
        (window as any).visualViewport = {
            width: 1024,
            height: 768,
            offsetLeft: 0,
            offsetTop: 0,
            pageLeft: 0,
            pageTop: 0,
            scale: 1,
            addEventListener: () => {},
            removeEventListener: () => {}
        }
    }
})

const vuetify = createVuetify({
    components,
    directives
})

describe('NodeConfigDialog Component (Agent & Model Configuration)', () => {
    it('should emit save with updated model configuration when editing an AI Model node', async () => {
        // Arrange
        const initialNode = {
            id: 'node_model_1',
            name: 'GPT-4o Mini',
            type: NodeType.MODEL,
            modelConfig: {
                provider: 'OpenAI',
                modelName: 'gpt-4o-mini',
                temperature: 0.5
            },
            endpoint: 'https://api.openai.com/v1'
        }

        const wrapper = mount(NodeConfigDialog, {
            props: {
                modelValue: true,
                nodeData: initialNode
            },
            global: {
                plugins: [vuetify]
            }
        })

        // Act
        const buttons = wrapper.findAllComponents(components.VBtn)
        const saveButton = buttons.find(b => b.text().includes('Save Changes'))
        await saveButton!.trigger('click')

        // Assert
        expect(wrapper.exists()).toBe(true)
        expect(saveButton).toBeDefined()
        const emittedSave = wrapper.emitted('save')
        expect(emittedSave).toBeTruthy()
        expect(emittedSave![0][0]).toMatchObject({
            id: 'node_model_1',
            name: 'GPT-4o Mini',
            type: NodeType.MODEL,
            modelConfig: {
                provider: 'OpenAI',
                modelName: 'gpt-4o-mini'
            }
        })
    })

    it('should configure MCP server URL and enabled tools for MCP connector node', async () => {
        // Arrange
        const mcpNode = {
            id: 'node_mcp_1',
            name: 'Web Tools Connector',
            type: NodeType.MCP_CONNECTOR,
            mcpConfig: {
                serverUrl: 'http://localhost:8000/mcp',
                authToken: 'sec-token',
                enabledTools: ['search_web', 'fetch_page']
            }
        }

        const wrapper = mount(NodeConfigDialog, {
            props: {
                modelValue: true,
                nodeData: mcpNode
            },
            global: {
                plugins: [vuetify]
            }
        })

        // Act
        const buttons = wrapper.findAllComponents(components.VBtn)
        const saveButton = buttons.find(b => b.text().includes('Save Changes'))
        await saveButton!.trigger('click')

        // Assert
        expect(saveButton).toBeDefined()
        const emittedSave = wrapper.emitted('save')
        expect(emittedSave).toBeTruthy()
        const savedPayload = emittedSave![0][0] as any
        expect(savedPayload.mcpConfig.serverUrl).toBe('http://localhost:8000/mcp')
        expect(savedPayload.mcpConfig.enabledTools).toEqual(['search_web', 'fetch_page'])
    })
    it('should configure sticky note content and color', async () => {
        const noteNode = {
            id: 'node_note_1',
            name: 'Architecture Notes',
            type: NodeType.NOTE,
            systemPrompt: 'Keep all agents asynchronous',
            endpoint: '#F59E0B'
        }

        const wrapper = mount(NodeConfigDialog, {
            props: {
                modelValue: true,
                nodeData: noteNode
            },
            global: {
                plugins: [vuetify]
            }
        })

        const buttons = wrapper.findAllComponents(components.VBtn)
        const saveButton = buttons.find(b => b.text().includes('Save Changes'))
        await saveButton!.trigger('click')

        const emittedSave = wrapper.emitted('save')
        expect(emittedSave).toBeTruthy()
        const saved = emittedSave![0][0] as any
        expect(saved.id).toBe('node_note_1')
        expect(saved.type).toBe(NodeType.NOTE)
        expect(saved.name).toBe('Architecture Notes')
        expect(saved.systemPrompt).toBe('Keep all agents asynchronous')
        expect(saved.endpoint).toBe('#F59E0B')
    })

    it('should configure canvas label text, subtitle and size', async () => {
        const labelNode = {
            id: 'node_label_1',
            name: 'Data Pipeline',
            type: NodeType.LABEL,
            systemPrompt: 'Stage 1 processing',
            endpoint: 'large',
            modelConfig: {
                provider: '#8B5CF6'
            }
        }

        const wrapper = mount(NodeConfigDialog, {
            props: {
                modelValue: true,
                nodeData: labelNode
            },
            global: {
                plugins: [vuetify]
            }
        })

        const buttons = wrapper.findAllComponents(components.VBtn)
        const saveButton = buttons.find(b => b.text().includes('Save Changes'))
        await saveButton!.trigger('click')

        const emittedSave = wrapper.emitted('save')
        expect(emittedSave).toBeTruthy()
        const saved = emittedSave![0][0] as any
        expect(saved.id).toBe('node_label_1')
        expect(saved.type).toBe(NodeType.LABEL)
        expect(saved.name).toBe('Data Pipeline')
        expect(saved.systemPrompt).toBe('Stage 1 processing')
        expect(saved.endpoint).toBe('large')
        expect(saved.modelConfig.provider).toBe('#8B5CF6')
    })
})