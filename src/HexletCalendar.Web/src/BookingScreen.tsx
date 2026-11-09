import { useState } from 'react'
import type { components } from '../../HexletCalendar.Contracts/generated/schema'
import EventTypesScreen from './EventTypesScreen'
import AvailabilityScreen from './AvailabilityScreen'

export default function BookingScreen() {
  const [eventType, setEventType] = useState<components['schemas']['EventType'] | null>(null)
  const [guestName, setGuestName] = useState('')
  const [guestEmail, setGuestEmail] = useState('')
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
      />
    : <EventTypesScreen owner={false} onContinue={setEventType} />
}
