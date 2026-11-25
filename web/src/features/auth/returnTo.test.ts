import { describe, expect, it } from 'vitest'
import { readReturnTo, withReturnTo } from './returnTo'

const read = (target: string) => readReturnTo(new URLSearchParams({ returnTo: target }))

describe('readReturnTo', () => {
  it('keeps an in-app path with its query', () => {
    expect(read('/lots/1?date=2026-12-01&space=2')).toBe('/lots/1?date=2026-12-01&space=2')
  })

  it('rejects targets that leave the site', () => {
    for (const target of ['https://evil.com', '//evil.com', '/\\evil.com', '/\t/evil.com', 'javascript:alert(1)', 'lots/1']) {
      expect(read(target), target).toBeNull()
    }
  })

  it('is null without a target', () => {
    expect(readReturnTo(new URLSearchParams())).toBeNull()
  })
})

describe('withReturnTo', () => {
  it('encodes the target, so its own query stays inside it', () => {
    const link = withReturnTo('/login', '/lots/1?date=2026-12-01&space=2')
    expect(read(new URL(link, 'https://x').searchParams.get('returnTo')!)).toBe('/lots/1?date=2026-12-01&space=2')
  })

  it('leaves the path alone without a target', () => {
    expect(withReturnTo('/login', null)).toBe('/login')
  })
})
