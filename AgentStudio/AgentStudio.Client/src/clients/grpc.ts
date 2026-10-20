import { GrpcWebFetchTransport } from '@protobuf-ts/grpcweb-transport'
import type { RpcOptions } from '@protobuf-ts/runtime-rpc'
import { AgentFlowServiceClient } from './generated/agent_node.client'
import { useConfig } from '../config'
import { useAuth } from '../auth'

let transportInstance: GrpcWebFetchTransport | null = null
let clientInstance: AgentFlowServiceClient | null = null
let currentBaseUrl: string = ''

export function getGrpcTransport(): GrpcWebFetchTransport {
    const config = useConfig()
    const targetUrl = (config.getGrpcUrl as string) || 'http://localhost:5000'

    if (!transportInstance || currentBaseUrl !== targetUrl) {
        currentBaseUrl = targetUrl
        transportInstance = new GrpcWebFetchTransport({
            baseUrl: targetUrl
        })
        clientInstance = new AgentFlowServiceClient(transportInstance)
    }

    return transportInstance
}

export function getGrpcClient(): AgentFlowServiceClient {
    getGrpcTransport()
    return clientInstance!
}

export function createGrpcOptions(overrideToken?: string): RpcOptions {
    const auth = useAuth()
    const token = overrideToken || auth.token.value

    const meta: Record<string, string> = {
        'content-type': 'application/grpc-web+proto',
        'x-user-agent': 'AgentStudio-Client/1.0'
    }

    if (token) {
        meta['Authorization'] = `Bearer ${token}`
    }

    return { meta }
}

export async function executeGrpcCall<T>(
    action: (client: AgentFlowServiceClient, options: RpcOptions) => Promise<T>
): Promise<{ data: T | null; error: Error | null }> {
    try {
        const client = getGrpcClient()
        const options = createGrpcOptions()
        const result = await action(client, options)
        return { data: result, error: null }
    } catch (err: any) {
        console.error('[gRPC-Web Error]', err)
        return { data: null, error: err instanceof Error ? err : new Error(String(err)) }
    }
}
