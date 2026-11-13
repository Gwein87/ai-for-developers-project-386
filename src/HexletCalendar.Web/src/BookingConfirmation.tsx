import type { FormEvent } from 'react'
import type { components } from '../../HexletCalendar.Contracts/generated/schema'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { moscowDateLabel, moscowTime } from './moscowTime'

type EventType = components['schemas']['EventType']
type Slot = components['schemas']['AvailabilitySlot']

type Props = {
  eventType: EventType
  slot: Slot
  guestName: string
  guestEmail: string
  errors: Record<string, string[]>
  saving: boolean
  onGuestNameChange: (value: string) => void
  onGuestEmailChange: (value: string) => void
  onBack: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}

export default function BookingConfirmation({
  eventType, slot, guestName, guestEmail, errors, saving, onGuestNameChange, onGuestEmailChange, onBack, onSubmit,
}: Props) {
  return <div className="max-w-xl space-y-6">
    <h1 className="text-3xl font-semibold">Подтвердите бронирование</h1>
    <Card>
      <CardHeader><CardTitle>{eventType.name}</CardTitle></CardHeader>
      <CardContent className="space-y-3">
        <p>{moscowDateLabel(slot.startAt)}</p>
        <p>{moscowTime(slot.startAt)}–{moscowTime(slot.endAt)}</p>
        <p>Московское время, UTC+3</p>
      </CardContent>
    </Card>
    <form onSubmit={onSubmit} noValidate className="space-y-5">
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
        <Button type="button" variant="outline" onClick={onBack}>Назад к выбору времени</Button>
        <Button type="submit" disabled={saving}>{saving ? 'Отправка…' : 'Подтвердить бронирование'}</Button>
      </div>
    </form>
  </div>
}
