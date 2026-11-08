import { expect, it } from 'vitest'
import type { components } from '../../src/HexletCalendar.Contracts/generated/schema.js'

it('consumes generated reference and nullable types', () => {
  const item: components['schemas']['ProbeItem'] = {
    id: 7,
    createdAt: '2026-11-08T09:00:00Z',
    detail: { note: null },
  }
  expect(item.detail.note).toBeNull()
})
