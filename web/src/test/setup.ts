import { configure } from '@testing-library/react'

// How long findBy* and waitFor keep retrying before they fail.
configure({ asyncUtilTimeout: 10_000 })
