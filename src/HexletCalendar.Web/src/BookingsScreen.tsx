import { useEffect, useState } from 'react'
import type { components } from '../../HexletCalendar.Contracts/generated/schema'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { moscowCalendarDate, moscowDateLabel, moscowTime } from './moscowTime'

type Bookings = components['schemas']['Bookings']
type Booking = components['schemas']['Booking']

function bookingState(booking: Booking, now: number) {
  if (Date.parse(booking.endAt) <= now) return 'Завершено'
  if (Date.parse(booking.startAt) <= now) return 'Идёт'
  return 'Предстоит'
}

export default function BookingsScreen() {
  const [bookings, setBookings] = useState<Bookings | null>(null)
  const [failed, setFailed] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const [serverOffset, setServerOffset] = useState(0)
  const [now, setNow] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    void fetch('/api/bookings', { signal: controller.signal })
      .then(response => { if (!response.ok) throw new Error('Loading failed'); return response.json() })
      .then((body: Bookings) => {
        if (!controller.signal.aborted) {
          const receivedNow = Date.parse(body.serverNow)
          setServerOffset(receivedNow - Date.now())
          setNow(receivedNow)
          setBookings(body)
        }
      })
      .catch(() => { if (!controller.signal.aborted) setFailed(true) })
    return () => controller.abort()
  }, [attempt])

  useEffect(() => {
    if (!bookings) return
    const responseDate = moscowCalendarDate(Date.parse(bookings.serverNow))
    const timer = window.setInterval(() => {
      const current = Date.now() + serverOffset
      setNow(current)
      if (moscowCalendarDate(current) !== responseDate) {
        setBookings(null)
        setAttempt(value => value + 1)
      }
    }, 1_000)
    return () => window.clearInterval(timer)
  }, [bookings, serverOffset])

  if (failed) return <div className="space-y-3">
    <p role="alert">Не удалось загрузить бронирования</p>
    <Button onClick={() => { setFailed(false); setBookings(null); setAttempt(value => value + 1) }}>Повторить</Button>
  </div>
  if (!bookings) return <p role="status">Загрузка бронирований…</p>

  return <div className="space-y-6">
    <p>Московское время, UTC+3</p>
    {bookings.items.length === 0 ? <p>Бронирований пока нет</p> : <ul aria-label="Бронирования" className="grid gap-4">
      {bookings.items.map(booking => {
        const state = bookingState(booking, now)
        const future = state === 'Предстоит'
        return <li key={booking.id} aria-label={future ? 'Предстоящее бронирование' : undefined}>
          <Card className={future ? 'bg-accent ring-2 ring-primary' : undefined}>
            <CardHeader><CardTitle>{booking.eventTypeName}</CardTitle></CardHeader>
            <CardContent className="grid gap-2 sm:grid-cols-2">
              <p>{moscowDateLabel(booking.startAt)}</p>
              <p>{moscowTime(booking.startAt)}–{moscowTime(booking.endAt)}</p>
              <p>{booking.guestName}</p>
              <a className="underline" href={`mailto:${booking.guestEmail}`}>{booking.guestEmail}</a>
              <p className="font-semibold">{state}</p>
            </CardContent>
          </Card>
        </li>
      })}
    </ul>}
  </div>
}
