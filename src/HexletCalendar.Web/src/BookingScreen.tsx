import { useState } from 'react'
import type { components } from '../../HexletCalendar.Contracts/generated/schema'
import EventTypesScreen from './EventTypesScreen'
import AvailabilityScreen from './AvailabilityScreen'

export default function BookingScreen() {
  const [eventType, setEventType] = useState<components['schemas']['EventType'] | null>(null)
  return eventType
    ? <AvailabilityScreen key={eventType.id} eventType={eventType} onBack={() => setEventType(null)} />
    : <EventTypesScreen owner={false} onContinue={setEventType} />
}
