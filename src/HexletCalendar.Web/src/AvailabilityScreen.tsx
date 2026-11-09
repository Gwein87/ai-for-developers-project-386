import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import type { components } from '../../HexletCalendar.Contracts/generated/schema'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

type EventType = components['schemas']['EventType']
type Availability = components['schemas']['Availability']
type Slot = components['schemas']['AvailabilitySlot']
type Booking = components['schemas']['Booking']
type ValidationProblem = components['schemas']['ValidationProblem']

function time(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { timeZone: 'Europe/Moscow', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function date(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { timeZone: 'Europe/Moscow', day: 'numeric', month: 'long', year: 'numeric' }).format(new Date(value))
}

function adjacentDate(value: string, offset: number) {
  const result = new Date(`${value}T12:00:00Z`)
  result.setUTCDate(result.getUTCDate() + offset)
  return result.toISOString().slice(0, 10)
}

function moscowDate(value: number) {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Europe/Moscow', year: 'numeric', month: '2-digit', day: '2-digit',
  }).formatToParts(new Date(value))
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(item => item.type === type)?.value ?? ''
  return `${part('year')}-${part('month')}-${part('day')}`
}

type Props = {
  eventType: EventType
  guestName: string
  guestEmail: string
  onGuestNameChange: (value: string) => void
  onGuestEmailChange: (value: string) => void
  onBack: () => void
  onNewBooking: () => void
}

export default function AvailabilityScreen({
  eventType, guestName, guestEmail, onGuestNameChange, onGuestEmailChange, onBack, onNewBooking,
}: Props) {
  const [calendar, setCalendar] = useState<Availability | null>(null)
  const [failed, setFailed] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const [serverOffset, setServerOffset] = useState(0)
  const [now, setNow] = useState(0)
  const [selectedDate, setSelectedDate] = useState('')
  const [slot, setSlot] = useState<Slot | null>(null)
  const [confirming, setConfirming] = useState(false)
  const [saving, setSaving] = useState(false)
  const [booking, setBooking] = useState<Booking | null>(null)
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [bookingMessage, setBookingMessage] = useState('')
  const idempotencyAttempt = useRef<{ fingerprint: string, key: string } | null>(null)
  useEffect(() => {
    const controller = new AbortController()
    void fetch(`/api/event-types/${eventType.id}/availability`, { signal: controller.signal })
      .then(response => { if (!response.ok) throw new Error('Loading failed'); return response.json() })
      .then((body: Availability) => {
        if (!controller.signal.aborted) {
          const receivedNow = Date.parse(body.serverNow)
          setServerOffset(receivedNow - Date.now())
          setNow(receivedNow)
          setCalendar(body)
          setSelectedDate(body.windowStartDate)
        }
      })
      .catch(() => { if (!controller.signal.aborted) setFailed(true) })
    return () => controller.abort()
  }, [attempt, eventType.id])
  useEffect(() => {
    if (!calendar) return
    const timer = window.setInterval(() => {
      const current = Date.now() + serverOffset
      setNow(current)
      if (moscowDate(current) !== calendar.windowStartDate) {
        setCalendar(null)
        setSlot(null)
        setAttempt(value => value + 1)
      }
    }, 1_000)
    return () => window.clearInterval(timer)
  }, [calendar, serverOffset])

  async function confirm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!slot) return
    setSaving(true)
    setErrors({})
    setBookingMessage('')
    const body: components['schemas']['CreateBooking'] = {
      eventTypeId: eventType.id, startAt: slot.startAt, guestName, guestEmail,
    }
    const fingerprint = JSON.stringify(body)
    let currentAttempt = idempotencyAttempt.current
    if (currentAttempt?.fingerprint !== fingerprint) {
      currentAttempt = { fingerprint, key: crypto.randomUUID() }
      idempotencyAttempt.current = currentAttempt
    }
    try {
      const response = await fetch('/api/bookings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Idempotency-Key': currentAttempt.key },
        body: fingerprint,
      })
      if (response.status === 201) {
        setBooking(await response.json() as Booking)
        return
      }
      const problem = await response.json() as components['schemas']['Problem'] | ValidationProblem
      if (response.status === 400 && problem.code === 'validation_error' && 'errors' in problem) {
        setErrors(problem.errors)
        return
      }
      if (problem.code === 'slot_taken' || problem.code === 'invalid_slot') {
        setBookingMessage(problem.code === 'slot_taken' ? 'Выбранное время уже занято. Выберите другое.' : 'Выбранное время больше недоступно. Выберите другое.')
        setConfirming(false)
        setSlot(null)
        setCalendar(null)
        idempotencyAttempt.current = null
        setAttempt(value => value + 1)
        return
      }
      setErrors({ form: [problem.code === 'temporarily_unavailable'
        ? 'Сервис временно недоступен. Повторите попытку.'
        : 'Результат отправки неизвестен. Безопасно повторите попытку.'] })
    } catch {
      setErrors({ form: ['Результат отправки неизвестен. Безопасно повторите попытку.'] })
    } finally {
      setSaving(false)
    }
  }

  if (booking) return <div className="max-w-xl space-y-6">
    <h1 className="text-3xl font-semibold">Бронирование создано</h1>
    <Card>
      <CardHeader><CardTitle>{booking.eventTypeName}</CardTitle></CardHeader>
      <CardContent className="space-y-3">
        <p>{date(booking.startAt)}</p>
        <p>{time(booking.startAt)}–{time(booking.endAt)}</p>
        <p>{booking.guestName}</p>
        <p>{booking.guestEmail}</p>
        <p>Письмо с подтверждением не отправляется</p>
      </CardContent>
    </Card>
    <Button onClick={onNewBooking}>Начать новое бронирование</Button>
  </div>

  if (confirming && slot) return <div className="max-w-xl space-y-6">
    <h1 className="text-3xl font-semibold">Подтвердите бронирование</h1>
    <Card>
      <CardHeader><CardTitle>{eventType.name}</CardTitle></CardHeader>
      <CardContent className="space-y-3">
        <p>{date(slot.startAt)}</p>
        <p>{time(slot.startAt)}–{time(slot.endAt)}</p>
        <p>Московское время, UTC+3</p>
      </CardContent>
    </Card>
    <form onSubmit={confirm} noValidate className="space-y-5">
      <div className="space-y-2">
        <label htmlFor="guest-name">Имя</label>
        <input id="guest-name" className="w-full rounded-md border bg-background px-3 py-2" value={guestName}
          onChange={event => onGuestNameChange(event.target.value)} aria-invalid={!!errors.guestName}
          aria-describedby={errors.guestName ? 'guest-name-error' : undefined} />
        {errors.guestName && <p id="guest-name-error" role="alert">{errors.guestName.join(' ')}</p>}
      </div>
      <div className="space-y-2">
        <label htmlFor="guest-email">Email</label>
        <input id="guest-email" type="email" className="w-full rounded-md border bg-background px-3 py-2" value={guestEmail}
          onChange={event => onGuestEmailChange(event.target.value)} aria-invalid={!!errors.guestEmail}
          aria-describedby={errors.guestEmail ? 'guest-email-error' : undefined} />
        {errors.guestEmail && <p id="guest-email-error" role="alert">{errors.guestEmail.join(' ')}</p>}
      </div>
      {errors.form && <p role="alert">{errors.form.join(' ')}</p>}
      <div className="flex flex-wrap gap-3">
        <Button type="button" variant="outline" onClick={() => setConfirming(false)}>Назад к выбору времени</Button>
        <Button type="submit" disabled={saving}>{saving ? 'Отправка…' : 'Подтвердить бронирование'}</Button>
      </div>
    </form>
  </div>

  const day = calendar?.days.find(day => day.date === selectedDate)
  const available = (item: Slot) => item.isAvailable && Date.parse(item.startAt) > now
  return <div className="space-y-6">
    <h1 className="text-3xl font-semibold">Выберите дату и время</h1>
    <p>{eventType.name} · {eventType.durationMinutes} мин.</p>
    <p>Московское время, UTC+3</p>
    {bookingMessage && <p role="alert">{bookingMessage}</p>}
    {calendar && !calendar.days.some(day => day.slots.some(available)) && <p role="status">В ближайшие 14 дней свободного времени нет</p>}
    {failed ? <div className="space-y-3">
      <p role="alert">Не удалось загрузить доступность</p>
      <Button onClick={() => { setFailed(false); setCalendar(null); setAttempt(value => value + 1) }}>Повторить</Button>
    </div> : !calendar
      ? <p role="status">Загрузка доступности…</p>
      : <div className="grid items-start gap-6 md:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Дата звонка</CardTitle></CardHeader>
          <CardContent>
            <div role="group" aria-label="Календарь" className="grid grid-cols-4 gap-2 sm:grid-cols-7 md:grid-cols-4">
              {[adjacentDate(calendar.windowStartDate, -1), ...calendar.days.map(day => day.date), adjacentDate(calendar.windowEndDate, 1)].map(value =>
                <Button key={value} aria-label={value} aria-pressed={selectedDate === value} variant={selectedDate === value ? 'default' : 'outline'}
                  className="h-auto flex-col px-1 py-3" disabled={!calendar.days.some(day => day.date === value)}
                  onClick={() => { setSelectedDate(value); setSlot(null) }}>
                  {new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short', timeZone: 'Europe/Moscow' }).format(new Date(`${value}T12:00:00Z`))}
                </Button>)}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>{date(`${selectedDate}T12:00:00Z`)}</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            {day && !day.slots.some(available) && <p role="status">На эту дату свободного времени нет</p>}
            <div role="group" aria-label="Время звонка" className="grid grid-cols-2 gap-2">
              {day?.slots.map(item => <Button key={item.startAt} disabled={!available(item)} aria-pressed={slot?.startAt === item.startAt}
                variant={slot?.startAt === item.startAt ? 'default' : 'outline'} onClick={() => setSlot(item)}>
                {time(item.startAt)}–{time(item.endAt)}
              </Button>)}
            </div>
          </CardContent>
        </Card>
      </div>}
    <div className="flex flex-wrap gap-3">
      <Button variant="outline" onClick={onBack}>Назад к типам событий</Button>
      <Button disabled={!slot} onClick={() => setConfirming(true)}>Продолжить</Button>
    </div>
  </div>
}
