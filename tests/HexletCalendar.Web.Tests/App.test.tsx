import { render, screen } from '@testing-library/react'
import { expect, it } from 'vitest'
import App from '../../src/HexletCalendar.Web/src/App'

it('starts and renders the application', () => {
  render(<App />)

  expect(
    screen.getByRole('heading', { level: 1, name: 'Календарь звонков' }),
  ).toBeInTheDocument()
})
