import { act, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import App from '../../src/HexletCalendar.Web/src/App'

afterEach(() => {
  vi.useRealTimers()
  window.history.replaceState(null, '', '/')
  vi.unstubAllEnvs()
})

it('expires a slot with server time and refreshes the window after Moscow midnight', async () => {
  vi.useFakeTimers()
  vi.setSystemTime(new Date('2026-11-09T06:14:58Z'))
  const first = availability()
  first.serverNow = '2026-11-09T06:14:58Z'
  const shifted = availability()
  shifted.serverNow = '2026-11-09T21:00:01Z'
  shifted.windowStartDate = '2026-11-10'
  shifted.windowEndDate = '2026-11-23'
  shifted.days = shifted.days.map((day, index) => ({ ...day, date: new Date(Date.UTC(2026, 10, 10 + index)).toISOString().slice(0, 10) }))
  const fetchMock = vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ items: [{ id: 77, name: 'Разбор проекта', description: 'Обсудим архитектуру', durationMinutes: 45 }] })))
    .mockResolvedValueOnce(new Response(JSON.stringify(first)))
    .mockResolvedValueOnce(new Response(JSON.stringify(shifted)))
  vi.stubGlobal('fetch', fetchMock)
  window.history.replaceState(null, '', '/booking')
  render(<App />)
  await act(async () => { await Promise.resolve() })
  await act(async () => { await Promise.resolve() })
  fireEvent.click(screen.getByRole('radio', { name: 'Выбрать Разбор проекта' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  await act(async () => { await Promise.resolve() })
  await act(async () => { await Promise.resolve() })
  expect(screen.getByRole('button', { name: '09:15–10:00' })).toBeEnabled()
  await act(async () => { vi.advanceTimersByTime(2_000) })
  expect(screen.getByRole('button', { name: '09:15–10:00' })).toBeDisabled()
  vi.setSystemTime(new Date('2026-11-09T20:59:59Z'))
  await act(async () => { vi.advanceTimersByTime(2_000); await Promise.resolve() })
  await act(async () => { await Promise.resolve() })
  expect(fetchMock).toHaveBeenCalledTimes(3)
  expect(screen.getByRole('button', { name: '2026-11-23' })).toBeEnabled()
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

it.each(['UTC', 'Pacific/Honolulu', 'Asia/Tokyo'])('lets a guest choose a future Moscow slot on a separate calendar screen and continue in %s', async timezone => {
  vi.stubEnv('TZ', timezone)
  const eventType = { id: 77, name: 'Разбор проекта', description: 'Обсудим архитектуру', durationMinutes: 45 }
  vi.stubGlobal('fetch', vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ items: [eventType] })))
    .mockResolvedValueOnce(new Response(JSON.stringify(availability()))))
  window.history.replaceState(null, '', '/booking')
  render(<App />)
  await screen.findByRole('radio', { name: 'Выбрать Разбор проекта' })
  expect(screen.getByRole('button', { name: 'Продолжить' })).toBeDisabled()
  fireEvent.click(screen.getByRole('radio', { name: 'Выбрать Разбор проекта' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  expect(await screen.findByRole('heading', { name: 'Выберите дату и время' })).toBeInTheDocument()
  expect(screen.queryByRole('radio')).not.toBeInTheDocument()
  expect(screen.getByText('Московское время, UTC+3')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: '2026-11-08' })).toBeDisabled()
  expect(screen.getByRole('button', { name: '2026-11-23' })).toBeDisabled()
  fireEvent.click(screen.getByRole('button', { name: '2026-11-22' }))
  expect(screen.getByRole('button', { name: '09:00–09:45' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'Продолжить' })).toBeDisabled()
  fireEvent.click(screen.getByRole('button', { name: '09:15–10:00' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  expect(screen.getByRole('heading', { name: 'Подтвердите бронирование' })).toBeInTheDocument()
  expect(screen.getByText('Разбор проекта')).toBeInTheDocument()
  expect(screen.getByText('22 ноября 2026 г.')).toBeInTheDocument()
  expect(screen.getByText('09:15–10:00')).toBeInTheDocument()
})

function availability() {
  return {
    eventTypeId: 77, timeZone: 'Europe/Moscow', serverNow: '2026-11-09T05:10:37Z', windowStartDate: '2026-11-09', windowEndDate: '2026-11-22',
    days: Array.from({ length: 14 }, (_, offset) => {
      const date = new Date(Date.UTC(2026, 10, 9 + offset)).toISOString().slice(0, 10)
      return { date, slots: [
        { startAt: `${date}T06:00:00Z`, endAt: `${date}T06:45:00Z`, isAvailable: false },
        { startAt: `${date}T06:15:00Z`, endAt: `${date}T07:00:00Z`, isAvailable: true },
      ] }
    }),
  }
}

it.each([false, true])('keeps occupied dates selectable and shows empty availability (whole window: %s)', async wholeWindow => {
  const calendar = availability()
  for (const [index, day] of calendar.days.entries()) {
    if (wholeWindow || index === 0) day.slots.forEach(slot => { slot.isAvailable = false })
  }
  vi.stubGlobal('fetch', vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ items: [{ id: 77, name: 'Разбор проекта', description: 'Обсудим архитектуру', durationMinutes: 45 }] })))
    .mockResolvedValueOnce(new Response(JSON.stringify(calendar))))
  window.history.replaceState(null, '', '/booking')
  render(<App />)
  fireEvent.click(await screen.findByRole('radio', { name: 'Выбрать Разбор проекта' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  expect(await screen.findByText('На эту дату свободного времени нет')).toBeInTheDocument()
  expect(screen.getByRole('group', { name: 'Календарь' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: '2026-11-09' })).toBeEnabled()
  if (wholeWindow) expect(screen.getByText('В ближайшие 14 дней свободного времени нет')).toBeInTheDocument()
  else expect(screen.queryByText('В ближайшие 14 дней свободного времени нет')).not.toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: '2026-11-22' }))
  expect(screen.getByRole('button', { name: '2026-11-22' })).toHaveAttribute('aria-pressed', 'true')
  if (wholeWindow) expect(screen.getByRole('button', { name: '09:15–10:00' })).toBeDisabled()
  else expect(screen.getByRole('button', { name: '09:15–10:00' })).toBeEnabled()
  expect(screen.getByRole('button', { name: 'Продолжить' })).toBeDisabled()
})

it('distinguishes an availability loading failure from empty dates and retries', async () => {
  vi.stubGlobal('fetch', vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ items: [{ id: 77, name: 'Разбор проекта', description: 'Обсудим архитектуру', durationMinutes: 45 }] })))
    .mockRejectedValueOnce(new Error('Offline'))
    .mockResolvedValueOnce(new Response(JSON.stringify(availability()))))
  window.history.replaceState(null, '', '/booking')
  render(<App />)
  fireEvent.click(await screen.findByRole('radio', { name: 'Выбрать Разбор проекта' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Не удалось загрузить доступность')
  expect(screen.queryByText('На эту дату свободного времени нет')).not.toBeInTheDocument()
  expect(screen.queryByText('В ближайшие 14 дней свободного времени нет')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Продолжить' })).toBeDisabled()
  fireEvent.click(screen.getByRole('button', { name: 'Повторить' }))
  expect(await screen.findByRole('button', { name: '2026-11-22' })).toBeEnabled()
  expect(screen.getByRole('button', { name: '09:15–10:00' })).toBeEnabled()
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

it('submits the third screen once and replaces it with the confirmed booking', async () => {
  const eventType = { id: 77, name: 'Разбор проекта', description: 'Обсудим архитектуру', durationMinutes: 45 }
  const confirmed = {
    id: 12, eventTypeId: 77, eventTypeName: eventType.name,
    startAt: '2026-11-09T06:15:00Z', endAt: '2026-11-09T07:00:00Z',
    guestName: 'Анна-Мария', guestEmail: 'Guest@Example.COM',
  }
  let resolveSave: (response: Response) => void = () => undefined
  const save = new Promise<Response>(resolve => { resolveSave = resolve })
  const fetchMock = vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ items: [eventType] })))
    .mockResolvedValueOnce(new Response(JSON.stringify(availability())))
    .mockReturnValueOnce(save)
  vi.stubGlobal('fetch', fetchMock)
  window.history.replaceState(null, '', '/booking')
  render(<App />)
  fireEvent.click(await screen.findByRole('radio', { name: 'Выбрать Разбор проекта' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  fireEvent.click(await screen.findByRole('button', { name: '09:15–10:00' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  fireEvent.change(screen.getByLabelText('Имя'), { target: { value: '  Анна-Мария  ' } })
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: '  Guest@Example.COM  ' } })
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить бронирование' }))
  expect(screen.getByRole('button', { name: 'Отправка…' })).toBeDisabled()
  expect(fetchMock).toHaveBeenCalledTimes(3)
  const request = fetchMock.mock.calls[2]
  expect(request[0]).toBe('/api/bookings')
  expect(request[1].headers['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/)
  expect(JSON.parse(request[1].body)).toEqual({ eventTypeId: 77, startAt: confirmed.startAt, guestName: '  Анна-Мария  ', guestEmail: 'Guest@Example.COM' })
  resolveSave(new Response(JSON.stringify(confirmed), { status: 201 }))
  expect(await screen.findByRole('heading', { name: 'Бронирование создано' })).toBeInTheDocument()
  expect(screen.getByText('Анна-Мария')).toBeInTheDocument()
  expect(screen.getByText('Guest@Example.COM')).toBeInTheDocument()
  expect(screen.getByText('Письмо с подтверждением не отправляется')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Начать новое бронирование' })).toBeInTheDocument()
})

it('shows booking field errors and preserves guest data while navigating back', async () => {
  const eventType = { id: 77, name: 'Разбор проекта', description: 'Обсудим архитектуру', durationMinutes: 45 }
  vi.stubGlobal('fetch', vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ items: [eventType] })))
    .mockResolvedValueOnce(new Response(JSON.stringify(availability())))
    .mockResolvedValueOnce(new Response(JSON.stringify({
      type: 'urn:hexlet-calendar:problem:validation_error', title: 'Ошибка проверки данных', status: 400, code: 'validation_error',
      errors: { guestName: ['Введите имя'], guestEmail: ['Введите корректный email'] },
    }), { status: 400 })))
  window.history.replaceState(null, '', '/booking')
  render(<App />)
  fireEvent.click(await screen.findByRole('radio', { name: 'Выбрать Разбор проекта' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  fireEvent.click(await screen.findByRole('button', { name: '09:15–10:00' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Анна' } })
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'bad' } })
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить бронирование' }))
  expect(await screen.findByText('Введите корректный email')).toBeInTheDocument()
  expect(screen.getByLabelText('Имя')).toHaveValue('Анна')
  fireEvent.click(screen.getByRole('button', { name: 'Назад к выбору времени' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  expect(screen.getByLabelText('Имя')).toHaveValue('Анна')
  expect(screen.getByLabelText('Email')).toHaveValue('bad')
})

it('returns from a taken slot to refreshed availability without losing guest data', async () => {
  const eventType = { id: 77, name: 'Разбор проекта', description: 'Обсудим архитектуру', durationMinutes: 45 }
  const refreshed = availability()
  refreshed.days[0].slots[1].isAvailable = false
  vi.stubGlobal('fetch', vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ items: [eventType] })))
    .mockResolvedValueOnce(new Response(JSON.stringify(availability())))
    .mockResolvedValueOnce(new Response(JSON.stringify({ status: 409, code: 'slot_taken' }), { status: 409 }))
    .mockResolvedValueOnce(new Response(JSON.stringify(refreshed))))
  window.history.replaceState(null, '', '/booking')
  render(<App />)
  fireEvent.click(await screen.findByRole('radio', { name: 'Выбрать Разбор проекта' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  fireEvent.click(await screen.findByRole('button', { name: '09:15–10:00' }))
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }))
  fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Анна' } })
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'guest@example.com' } })
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить бронирование' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Выбранное время уже занято')
  expect(screen.getByRole('heading', { name: 'Выберите дату и время' })).toBeInTheDocument()
  expect(await screen.findByRole('button', { name: '09:15–10:00' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'Продолжить' })).toBeDisabled()
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
