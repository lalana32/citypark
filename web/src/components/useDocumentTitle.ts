import { useEffect } from 'react'

/** Sets the browser tab title to "<page> · CityPark". */
export function useDocumentTitle(page: string) {
  useEffect(() => {
    document.title = `${page} · CityPark`
  }, [page])
}
