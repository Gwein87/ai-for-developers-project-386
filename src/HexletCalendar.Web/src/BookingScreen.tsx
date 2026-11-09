import { useState } from 'react'
import type { components } from '../../HexletCalendar.Contracts/generated/schema'
import EventTypesScreen from './EventTypesScreen'
import AvailabilityScreen, { pendingBookingKey } from './AvailabilityScreen'
import type { PendingBooking } from './AvailabilityScreen'

function loadPendingBooking(): PendingBooking | null {
  const stored = window.sessionStorage.getItem(pendingBookingKey)
  if (!stored) return null
  try {
    const value = JSON.parse(stored) as PendingBooking
    if (typeof value.key === 'string'
      && typeof value.endAt === 'string'
      && typeof value.body?.eventTypeId === 'number'
      && typeof value.body.startAt === 'string'
      && typeof value.body.guestName === 'string'
      && typeof value.body.guestEmail === 'string'
      && value.eventType?.id === value.body.eventTypeId
      && typeof value.eventType.name === 'string') return value
  } catch {
    // Ignore invalid data left by an older application version.
  }
  window.sessionStorage.removeItem(pendingBookingKey)
  return null
}

export default function BookingScreen() {
  const [recoveredBooking, setRecoveredBooking] = useState(loadPendingBooking)
  const [eventType, setEventType] = useState<components['schemas']['EventType'] | null>(recoveredBooking?.eventType ?? null)
  const [guestName, setGuestName] = useState(recoveredBooking?.body.guestName ?? '')
  const [guestEmail, setGuestEmail] = useState(recoveredBooking?.body.guestEmail ?? '')
  return eventType
    ? <AvailabilityScreen
        key={eventType.id}
        eventType={eventType}
        guestName={guestName}
        guestEmail={guestEmail}
        onGuestNameChange={setGuestName}
        onGuestEmailChange={setGuestEmail}
        onBack={() => setEventType(null)}
        onNewBooking={() => { setEventType(null); setGuestName(''); setGuestEmail('') }}
        onAttemptResolved={() => setRecoveredBooking(null)}
        recoveredBooking={recoveredBooking}
      />
    : <EventTypesScreen owner={false} onContinue={setEventType} />
}
