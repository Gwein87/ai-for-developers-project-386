import { useState } from 'react'
import { Button } from '@/components/ui/button'
import BookingsScreen from './BookingsScreen'
import EventTypesScreen from './EventTypesScreen'

export default function AdminScreen() {
  const [tab, setTab] = useState<'event-types' | 'bookings'>('event-types')

  return <div className="space-y-8">
    <h1 className="text-3xl font-semibold">Админка</h1>
    <div role="tablist" aria-label="Управление календарём" className="flex gap-3">
      <Button role="tab" aria-selected={tab === 'event-types'} variant={tab === 'event-types' ? 'default' : 'outline'} onClick={() => setTab('event-types')}>Типы событий</Button>
      <Button role="tab" aria-selected={tab === 'bookings'} variant={tab === 'bookings' ? 'default' : 'outline'} onClick={() => setTab('bookings')}>Бронирования</Button>
    </div>
    {tab === 'event-types' ? <EventTypesScreen owner embedded /> : <BookingsScreen />}
  </div>
}
