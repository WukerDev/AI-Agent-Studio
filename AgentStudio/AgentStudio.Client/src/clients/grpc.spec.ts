import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { getGrpcClient, getGrpcTransport, createGrpcOptions } from './grpc'

describe('Central gRPC Client (src/clients/grpc.ts)', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('should create and return a singleton GrpcWebFetchTransport', () => {
        const transport1 = getGrpcTransport()
        const transport2 = getGrpcTransport()
        expect(transport1).toBeDefined()
        expect(transport1).toBe(transport2)
    })

    it('should return a valid AgentFlowServiceClient instance', () => {
        const client = getGrpcClient()
        expect(client).toBeDefined()
        expect(typeof client.saveAgentNode).toBe('function')
        expect(typeof client.getAgentNodes).toBe('function')
        expect(typeof client.saveWorkflowEdges).toBe('function')
        expect(typeof client.getWorkflowEdges).toBe('function')
        expect(typeof client.executeWorkflow).toBe('function')
        expect(typeof client.getExplorer).toBe('function')
    })

    it('should create RPC options with grpc-web headers', () => {
        const options = createGrpcOptions()
        expect(options.meta).toBeDefined()
        expect(options.meta!['content-type']).toBe('application/grpc-web+proto')
    })

    it('should include Authorization Bearer token when token is passed', () => {
        const options = createGrpcOptions('test-jwt-token-123')
        expect(options.meta!['Authorization']).toBe('Bearer test-jwt-token-123')
    })
})
