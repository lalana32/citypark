import bascarsija from '../../assets/lots/bascarsija.webp'
import centar from '../../assets/lots/centar.webp'
import marijinDvor from '../../assets/lots/marijin-dvor.webp'

// Keyed by the seeded lot names; any other lot falls back to the illustration.
const photos: Record<string, string> = {
  Baščaršija: bascarsija,
  Centar: centar,
  'Marijin Dvor': marijinDvor,
}

export function lotPhoto(name: string): string | undefined {
  return photos[name]
}
