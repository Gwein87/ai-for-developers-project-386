import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import type { components } from '../../HexletCalendar.Contracts/generated/schema'
import { createBooking, getAvailability } from '../../HexletCalendar.Contracts/generated/sdk/sdk.gen'
import './apiClient'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { moscowCalendarDate, moscowDateLabel, moscowShortDate, moscowTime } from './moscowTime'
import BookingConfirmation from './BookingConfirmation'

type EventType = components['schemas']['EventType']
type Availability = components['schemas']['Availability']
type Slot = components['schemas']['AvailabilitySlot']
type Booking = components['schemas']['Booking']
type ValidationProblem = components['schemas']['ValidationProblem']
type BookingRequest = components['schemas']['CreateBooking']

export const pendingBookingKey = 'hexlet-calendar.pending-booking'

export type PendingBooking = {
  key: string
  body: BookingRequest
  eventType: EventType
  endAt: string
}

function adjacentDate(value: string, offset: number) {
  const result = new Date(`${value}T12:00:00Z`)
  result.setUTCDate(result.getUTCDate() + offset)
  return result.toISOString().slice(0, 10)
}

type Props = {
  eventType: EventType
  guestName: string
  guestEmail: string
  onGuestNameChange: (value: string) => void
  onGuestEmailChange: (value: string) => void
  onBack: () => void
  onNewBooking: () => void
  onAttemptResolved: () => void
  recoveredBooking?: PendingBooking | null
}

export default function AvailabilityScreen({
  eventType, guestName, guestEmail, onGuestNameChange, onGuestEmailChange, onBack, onNewBooking,
  onAttemptResolved, recoveredBooking = null,
}: Props) {
  const [calendar, setCalendar] = useState<Availability | null>(null)
  const [failed, setFailed] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const [serverOffset, setServerOffset] = useState(0)
  const [now, setNow] = useState(0)
  const [selectedDate, setSelectedDate] = useState('')
  const [slot, setSlot] = useState<Slot | null>(recoveredBooking
    ? { startAt: recoveredBooking.body.startAt, endAt: recoveredBooking.endAt, isAvailable: true }
    : null)
  const [confirming, setConfirming] = useState(recoveredBooking !== null)
  const [saving, setSaving] = useState(false)
  const [booking, setBooking] = useState<Booking | null>(null)
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [bookingMessage, setBookingMessage] = useState('')
  const [pendingBooking, setPendingBooking] = useState<PendingBooking | null>(recoveredBooking)
  const [unknownResult, setUnknownResult] = useState(recoveredBooking !== null)
  const [restoring, setRestoring] = useState(recoveredBooking !== null)
  const idempotencyAttempt = useRef<{ fingerprint: string, key: string } | null>(recoveredBooking
    ? { fingerprint: JSON.stringify(recoveredBooking.body), key: recoveredBooking.key }
    : null)
  useEffect(() => {
    if (restoring) return
    const controller = new AbortController()
    void getAvailability({ path: { id: String(eventType.id) }, signal: controller.signal, throwOnError: true, parseAs: 'json' })
      .then(({ data }) => {
        const body = data as Availability
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
  }, [attempt, eventType.id, restoring])
  useEffect(() => {
    if (!calendar) return
    const timer = window.setInterval(() => {
      const current = Date.now() + serverOffset
      setNow(current)
      if (moscowCalendarDate(current) !== calendar.windowStartDate) {
        setCalendar(null)
        setSlot(null)
        setAttempt(value => value + 1)
      }
    }, 1_000)
    return () => window.clearInterval(timer)
  }, [calendar, serverOffset])

  async function submitBooking(currentAttempt: PendingBooking) {
    setSaving(true)
    setErrors({})
    setBookingMessage('')
    try {
      const response = await createBooking({ body: currentAttempt.body, headers: { 'Idempotency-Key': currentAttempt.key }, parseAs: 'json' })
      if (response.response?.status === 201 && response.data) {
        window.sessionStorage.removeItem(pendingBookingKey)
        setPendingBooking(null)
        setUnknownResult(false)
        onAttemptResolved()
        setBooking(response.data as Booking)
        return
      }
      const problem = response.error as components['schemas']['Problem'] | ValidationProblem
      if (response.response?.status === 400 && problem?.code === 'validation_error' && 'errors' in problem) {
        window.sessionStorage.removeItem(pendingBookingKey)
        setPendingBooking(null)
        onAttemptResolved()
        setErrors(problem.errors)
        return
      }
      if (problem.code === 'slot_taken' || problem.code === 'invalid_slot') {
        window.sessionStorage.removeItem(pendingBookingKey)
        setPendingBooking(null)
        onAttemptResolved()
        setBookingMessage(problem.code === 'slot_taken' ? 'Выбранное время уже занято. Выберите другое.' : 'Выбранное время больше недоступно. Выберите другое.')
        setConfirming(false)
        setSlot(null)
        setCalendar(null)
        setRestoring(false)
        idempotencyAttempt.current = null
        setAttempt(value => value + 1)
        return
      }
      if (problem.code === 'event_type_not_found' || problem.code === 'idempotency_key_reused') {
        window.sessionStorage.removeItem(pendingBookingKey)
        setPendingBooking(null)
        setUnknownResult(false)
        setRestoring(false)
        onAttemptResolved()
        setBookingMessage(problem.code === 'event_type_not_found'
          ? 'Тип события больше не существует. Выберите другой тип события.'
          : 'Не удалось проверить эту попытку бронирования. Начните новое бронирование.')
        setConfirming(false)
        setSlot(null)
        idempotencyAttempt.current = null
        return
      }
      setUnknownResult(true)
    } catch {
      setUnknownResult(true)
    } finally {
      setSaving(false)
    }
  }

  async function confirm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!slot) return
    const body: BookingRequest = { eventTypeId: eventType.id, startAt: slot.startAt, guestName, guestEmail }
    const fingerprint = JSON.stringify(body)
    let identity = idempotencyAttempt.current
    if (identity?.fingerprint !== fingerprint) {
      identity = { fingerprint, key: crypto.randomUUID() }
      idempotencyAttempt.current = identity
    }
    const currentAttempt = { key: identity.key, body, eventType, endAt: slot.endAt }
    setPendingBooking(currentAttempt)
    window.sessionStorage.setItem(pendingBookingKey, JSON.stringify(currentAttempt))
    await submitBooking(currentAttempt)
  }

  if (booking) return <div className="max-w-xl space-y-6">
    <h1 className="text-3xl font-semibold">Бронирование создано</h1>
    <Card>
      <CardHeader><CardTitle>{booking.eventTypeName}</CardTitle></CardHeader>
      <CardContent className="space-y-3">
        <p>{moscowDateLabel(booking.startAt)}</p>
        <p>{moscowTime(booking.startAt)}–{moscowTime(booking.endAt)} · Московское время, UTC+3</p>
        <p>{booking.guestName}</p>
        <p>{booking.guestEmail}</p>
        <p>Письмо с подтверждением не отправляется</p>
      </CardContent>
    </Card>
    <Button onClick={onNewBooking}>Начать новое бронирование</Button>
  </div>

  if (unknownResult && pendingBooking) return <div className="max-w-xl space-y-6">
    <h1 className="text-3xl font-semibold">Не удалось подтвердить результат</h1>
    <Card>
      <CardHeader><CardTitle>{pendingBooking.eventType.name}</CardTitle></CardHeader>
      <CardContent className="space-y-3">
        <p>{moscowDateLabel(pendingBooking.body.startAt)}</p>
        <p>{moscowTime(pendingBooking.body.startAt)}–{moscowTime(pendingBooking.endAt)}</p>
        <p>{pendingBooking.body.guestName}</p>
        <p>{pendingBooking.body.guestEmail}</p>
      </CardContent>
    </Card>
    <p role="alert">Ответ не получен. Повтор безопасно проверит прежнюю попытку.</p>
    <Button disabled={saving} onClick={() => void submitBooking(pendingBooking)}>{saving ? 'Отправка…' : 'Повторить'}</Button>
  </div>

  if (confirming && slot) return <BookingConfirmation
    eventType={eventType}
    slot={slot}
    guestName={guestName}
    guestEmail={guestEmail}
    errors={errors}
    saving={saving}
    onGuestNameChange={onGuestNameChange}
    onGuestEmailChange={onGuestEmailChange}
    onBack={() => setConfirming(false)}
    onSubmit={confirm}
  />

  const day = calendar?.days.find(day => day.date === selectedDate)
  const available = (item: Slot) => item.isAvailable && Date.parse(item.startAt) > now
  return <div className="space-y-6">
    <h1 className="text-3xl font-semibold">Выберите дату и время</h1>
    <p>{eventType.name} · {eventType.durationMinutes} мин.</p>
    <p>Московское время, UTC+3</p>
    {bookingMessage && <p role="alert">{bookingMessage}</p>}
    {calendar && !calendar.days.some(day => day.slots.some(available)) && <p role="status">В ближайшие 14 дней нет доступных слотов</p>}
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
                  {moscowShortDate(value)}
                </Button>)}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>{moscowDateLabel(`${selectedDate}T12:00:00Z`)}</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            {day && !day.slots.some(available) && <p role="status">На эту дату нет доступных слотов</p>}
            <div role="group" aria-label="Время звонка" className="grid grid-cols-2 gap-2">
              {day?.slots.map(item => <Button key={item.startAt} disabled={!available(item)} aria-pressed={slot?.startAt === item.startAt}
                variant={slot?.startAt === item.startAt ? 'default' : 'outline'} onClick={() => setSlot(item)}>
                {moscowTime(item.startAt)}–{moscowTime(item.endAt)}
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
