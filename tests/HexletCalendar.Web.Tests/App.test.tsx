import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
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
    'Выберите тип события и удобное свободное время — сервис поможет записаться на звонок без долгих согласований.',
  )).toBeInTheDocument()

  const navigation = within(screen.getByRole('navigation', { name: 'Основная навигация' }))
  expect(navigation.getByRole('link', { name: 'Записаться' })).toHaveAttribute('href', '/booking')
  expect(navigation.getByRole('link', { name: 'Админка' })).toHaveAttribute('href', '/admin')
  expect(screen.queryByRole('button', { name: 'Проверить API' })).not.toBeInTheDocument()
})

it('shows a distinct loading and empty state to the guest', async () => {
  window.history.replaceState(null, '', '/booking')
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ items: [] }))))
  render(<App />)
  expect(screen.getByRole('status')).toHaveTextContent('Загрузка типов событий')
  expect(await screen.findByText('Пока нет доступных типов событий')).toBeInTheDocument()
  expect(screen.getByRole('heading', { level: 1, name: 'Выберите тип события' })).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/')
})

it('lets the owner create a type and offers it to a guest', async () => {
  const items: { id: number, name: string, description: string, durationMinutes: number }[] = []
  vi.stubGlobal('fetch', vi.fn(async (_url: string, options?: RequestInit) => {
    if (options?.method === 'POST') {
      expect(JSON.parse(String(options.body))).toEqual({ name: 'Обсуждение проекта', description: 'Поговорим о календаре', durationMinutes: 45 })
      const item = { id: 1, name: 'Обсуждение проекта', description: 'Поговорим о календаре', durationMinutes: 45 }
      items.push(item)
      return new Response(JSON.stringify(item), { status: 201 })
    }
    return new Response(JSON.stringify({ items }))
  }))
  window.history.replaceState(null, '', '/admin')
  const owner = render(<App />)
  expect(await screen.findByText('Создайте первый тип события')).toBeInTheDocument()
  expect(screen.getByRole('tab', { name: 'Типы событий' })).toHaveAttribute('aria-selected', 'true')
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Обсуждение проекта' } })
  fireEvent.change(screen.getByLabelText('Описание'), { target: { value: 'Поговорим о календаре' } })
  fireEvent.change(screen.getByLabelText('Длительность в минутах'), { target: { value: '45' } })
  fireEvent.click(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(await screen.findByText('Тип события создан')).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Обсуждение проекта' })).toBeInTheDocument()
  owner.unmount()
  window.history.replaceState(null, '', '/booking')
  render(<App />)
  expect(await screen.findByRole('heading', { name: 'Обсуждение проекта' })).toBeInTheDocument()
  expect(screen.getByText('Поговорим о календаре')).toBeInTheDocument()
  expect(screen.getByText('Длительность: 45 мин.')).toBeInTheDocument()
  fireEvent.click(screen.getByRole('radio', { name: 'Выбрать Обсуждение проекта' }))
  expect(screen.getByRole('radio', { name: 'Выбрать Обсуждение проекта' })).toBeChecked()
})

it.each(['/booking', '/admin'])('offers a retry after loading fails at %s', async path => {
  vi.stubGlobal('fetch', vi.fn()
    .mockRejectedValueOnce(new Error('Offline'))
    .mockResolvedValue(new Response(JSON.stringify({ items: [] }))))
  window.history.replaceState(null, '', path)
  render(<App />)
  expect(await screen.findByRole('alert')).toHaveTextContent('Не удалось загрузить типы событий')
  expect(screen.queryByText('Пока нет доступных типов событий')).not.toBeInTheDocument()
  expect(screen.queryByText('Создайте первый тип события')).not.toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Повторить' }))
  expect(await screen.findByText(path === '/admin' ? 'Создайте первый тип события' : 'Пока нет доступных типов событий')).toBeInTheDocument()
})

it('shows field errors beside the controls and preserves entered values', async () => {
  vi.stubGlobal('fetch', vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ items: [] })))
    .mockResolvedValueOnce(new Response(JSON.stringify({
      type: 'urn:hexlet-calendar:problem:validation_error', title: 'Ошибка проверки данных', status: 400, code: 'validation_error',
      errors: { name: ['Введите название'], description: ['Введите описание'], durationMinutes: ['Длительность должна быть кратна 15'] },
    }), { status: 400, headers: { 'Content-Type': 'application/problem+json' } })))
  window.history.replaceState(null, '', '/admin')
  render(<App />)
  await screen.findByText('Создайте первый тип события')
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: '  ' } })
  fireEvent.change(screen.getByLabelText('Описание'), { target: { value: 'Мой текст' } })
  fireEvent.change(screen.getByLabelText('Длительность в минутах'), { target: { value: '16' } })
  fireEvent.click(screen.getByRole('button', { name: 'Создать тип события' }))
  await screen.findByText('Введите название')
  expect(screen.getByLabelText('Название')).toHaveAccessibleDescription('Введите название')
  expect(screen.getByLabelText('Описание')).toHaveAccessibleDescription('Введите описание')
  expect(screen.getByLabelText('Длительность в минутах')).toHaveAccessibleDescription('Длительность должна быть кратна 15')
  expect(screen.getByLabelText('Название')).toHaveValue('  ')
  expect(screen.getByLabelText('Описание')).toHaveValue('Мой текст')
  expect(screen.getByLabelText('Длительность в минутах')).toHaveValue(16)
  expect(screen.queryByText('Тип события создан')).not.toBeInTheDocument()
})

it.each(['network', 'server'])('preserves the form after a %s save failure and allows retry', async failure => {
  const item = { id: 1, name: 'Консультация', description: 'Обсудим проект', durationMinutes: 30 }
  vi.stubGlobal('fetch', vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ items: [] })))
    .mockImplementationOnce(() => failure === 'network'
      ? Promise.reject(new Error('Offline'))
      : Promise.resolve(new Response(JSON.stringify({ status: 500, code: 'internal_error' }), { status: 500 })))
    .mockResolvedValueOnce(new Response(JSON.stringify(item), { status: 201 })))
  window.history.replaceState(null, '', '/admin')
  render(<App />)
  await screen.findByText('Создайте первый тип события')
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Консультация' } })
  fireEvent.change(screen.getByLabelText('Описание'), { target: { value: 'Обсудим проект' } })
  fireEvent.click(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Не удалось создать тип события. Попробуйте ещё раз.')
  expect(screen.getByLabelText('Название')).toHaveValue('Консультация')
  expect(screen.getByLabelText('Описание')).toHaveValue('Обсудим проект')
  expect(screen.getByLabelText('Длительность в минутах')).toHaveValue(30)
  expect(screen.queryByText('Тип события создан')).not.toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(await screen.findByText('Тип события создан')).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Консультация' })).toBeInTheDocument()
})
