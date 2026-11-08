import { render, screen, within } from '@testing-library/react'
import { afterEach, expect, it } from 'vitest'
import App from '../../src/HexletCalendar.Web/src/App'

afterEach(() => {
  window.history.replaceState(null, '', '/')
})

it('renders the welcome page with the service description and navigation', () => {
  render(<App />)

  expect(
    screen.getByRole('heading', { level: 1, name: 'Календарь звонков' }),
  ).toBeInTheDocument()
  expect(screen.getByText(
    'Выберите тип встречи и удобное свободное время — сервис поможет записаться на звонок без долгих согласований.',
  )).toBeInTheDocument()

  const navigation = within(screen.getByRole('navigation', { name: 'Основная навигация' }))
  expect(navigation.getByRole('link', { name: 'Записаться' })).toHaveAttribute('href', '/booking')
  expect(navigation.getByRole('link', { name: 'Админка' })).toHaveAttribute('href', '/admin')
  expect(screen.queryByRole('button', { name: 'Проверить API' })).not.toBeInTheDocument()
})

it.each([
  ['/booking', 'Запись на звонок', 'Записаться'],
  ['/admin', 'Админка', 'Админка'],
  ['/booking/', 'Запись на звонок', 'Записаться'],
])('renders a placeholder at %s with a way back home', (path, section, linkLabel) => {
  window.history.replaceState(null, '', path)
  render(<App />)

  expect(screen.getByRole('heading', { level: 1, name: 'Раздел готовится' })).toBeInTheDocument()
  expect(screen.getByRole('heading', { level: 2, name: section })).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/')
  expect(screen.getByRole('link', { name: linkLabel })).toHaveAttribute('aria-current', 'page')
})
