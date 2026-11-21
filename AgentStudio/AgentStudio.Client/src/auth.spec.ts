import { describe, it, expect, afterEach } from 'vitest';
import { ensureCryptoRandomUuid, resolvePkceMethod } from './auth';

describe('auth non-secure HTTP context compatibility', () => {
    const originalCryptoDescriptor = Object.getOwnPropertyDescriptor(globalThis, 'crypto');

    afterEach(() => {
        if (originalCryptoDescriptor) {
            Object.defineProperty(globalThis, 'crypto', originalCryptoDescriptor);
        }
    });

    it('polyfills crypto.randomUUID using crypto.getRandomValues when missing in HTTP contexts', () => {
        const fakeCrypto: Partial<Crypto> = {
            getRandomValues: (<T extends ArrayBufferView | null>(array: T): T => {
                if (array instanceof Uint8Array) {
                    for (let i = 0; i < array.length; i++) {
                        array[i] = (i * 17 + 3) & 0xff;
                    }
                }
                return array;
            }) as Crypto['getRandomValues']
        };

        Object.defineProperty(globalThis, 'crypto', {
            value: fakeCrypto,
            configurable: true,
            writable: true
        });

        expect(typeof globalThis.crypto.randomUUID).toBe('undefined');

        ensureCryptoRandomUuid();

        expect(typeof globalThis.crypto.randomUUID).toBe('function');
        const uuid = globalThis.crypto.randomUUID();
        expect(uuid).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    });

    it('disables PKCE S256 when crypto.subtle is unavailable (plain HTTP over Tailscale/LAN)', () => {
        Object.defineProperty(globalThis, 'crypto', {
            value: {
                getRandomValues: globalThis.crypto.getRandomValues.bind(globalThis.crypto)
            },
            configurable: true,
            writable: true
        });

        expect(resolvePkceMethod()).toBe(false);
    });

    it('uses PKCE S256 when crypto.subtle.digest is available (HTTPS or localhost)', () => {
        Object.defineProperty(globalThis, 'crypto', {
            value: {
                subtle: {
                    digest: async () => new ArrayBuffer(32)
                }
            },
            configurable: true,
            writable: true
        });

        expect(resolvePkceMethod()).toBe('S256');
    });
});
