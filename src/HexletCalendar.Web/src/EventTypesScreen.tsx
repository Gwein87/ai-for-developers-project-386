import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import type { components } from '../../HexletCalendar.Contracts/generated/schema'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

type EventType = components['schemas']['EventType']
type EventTypes = components['schemas']['EventTypes']

export default function EventTypesScreen({ owner }: { owner: boolean }) {
  const [items, setItems] = useState<EventType[] | null>(null)
  const [loadFailed, setLoadFailed] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const [selected, setSelected] = useState<number | null>(null)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [duration, setDuration] = useState('30')
  const [message, setMessage] = useState('')
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [saving, setSaving] = useState(false)
  useEffect(() => {
    const controller = new AbortController()
    void fetch('/api/event-types', { signal: controller.signal })
      .then(response => { if (!response.ok) throw new Error('Loading failed'); return response.json() })
      .then((body: EventTypes) => { if (!controller.signal.aborted) setItems(body.items) })
      .catch(() => { if (!controller.signal.aborted) setLoadFailed(true) })
    return () => controller.abort()
  }, [attempt])

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setErrors({})
    setMessage('')
    setSaving(true)
    try {
      const body: components['schemas']['CreateEventType'] = { name, description, durationMinutes: Number(duration) }
      const response = await fetch('/api/event-types', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
      })
      if (response.status === 400) {
        const problem: components['schemas']['ValidationProblem'] = await response.json()
        setErrors(problem.errors)
        return
      }
      if (response.status !== 201) throw new Error('Creation failed')
      const item: EventType = await response.json()
      setItems(current => [...(current ?? []), item].sort((a, b) => a.id - b.id))
      setName('')
      setDescription('')
      setDuration('30')
      setMessage('Тип события создан')
    } catch {
      setErrors({ form: ['Не удалось создать тип события. Попробуйте ещё раз.'] })
    } finally {
      setSaving(false)
    }
  }

  const fieldClass = 'w-full rounded-md border bg-background px-3 py-2'
  return <div className="space-y-8">
    <h1 className="text-3xl font-semibold">{owner ? 'Админка' : 'Выберите тип события'}</h1>
    {owner && <div role="tablist" aria-label="Управление календарём" className="flex gap-3">
      <Button role="tab" aria-selected="true">Типы событий</Button>
      <Button role="tab" aria-selected="false" variant="outline" disabled>Бронирования</Button>
    </div>}
    {loadFailed ? <div role="alert" className="space-y-3">
      <p>Не удалось загрузить типы событий</p>
      <Button onClick={() => { setLoadFailed(false); setItems(null); setAttempt(value => value + 1) }}>Повторить</Button>
    </div> : items === null ? <p role="status">Загрузка типов событий…</p> : items.length === 0
      ? <p>{owner ? 'Создайте первый тип события' : 'Пока нет доступных типов событий'}</p>
      : <ul aria-label="Типы событий" className="grid gap-4 sm:grid-cols-2">
        {items.map(item => <li key={item.id}>
          <Card>
            <CardHeader><CardTitle>{item.name}</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <CardDescription className="whitespace-pre-wrap break-words">{item.description}</CardDescription>
              <p>Длительность: {item.durationMinutes} мин.</p>
              {!owner && <label className="flex items-center gap-2">
                <input type="radio" name="eventType" value={item.id} checked={selected === item.id} onChange={() => setSelected(item.id)} />
                Выбрать {item.name}
              </label>}
            </CardContent>
          </Card>
        </li>)}
      </ul>}
    {owner && <Card className="max-w-xl">
      <CardHeader><CardTitle>Создать тип события</CardTitle></CardHeader>
      <CardContent>
        <form onSubmit={create} noValidate className="space-y-5">
          <div className="space-y-2">
            <label htmlFor="event-name">Название</label>
            <input id="event-name" className={fieldClass} value={name} onChange={event => setName(event.target.value)} required aria-invalid={!!errors.name} aria-describedby={errors.name ? 'name-error' : undefined} />
            {errors.name && <p id="name-error" role="alert">{errors.name.join(' ')}</p>}
          </div>
          <div className="space-y-2">
            <label htmlFor="event-description">Описание</label>
            <textarea id="event-description" className={fieldClass} value={description} onChange={event => setDescription(event.target.value)} required aria-invalid={!!errors.description} aria-describedby={errors.description ? 'description-error' : undefined} />
            {errors.description && <p id="description-error" role="alert">{errors.description.join(' ')}</p>}
          </div>
          <div className="space-y-2">
            <label htmlFor="event-duration">Длительность в минутах</label>
            <input id="event-duration" type="number" min="15" max="540" step="15" className={fieldClass} value={duration} onChange={event => setDuration(event.target.value)} required aria-invalid={!!errors.durationMinutes} aria-describedby={errors.durationMinutes ? 'duration-error' : undefined} />
            {errors.durationMinutes && <p id="duration-error" role="alert">{errors.durationMinutes.join(' ')}</p>}
          </div>
          {errors.form && <p role="alert">{errors.form.join(' ')}</p>}
          {message && <p role="status">{message}</p>}
          <Button type="submit" disabled={saving || items === null}>{saving ? 'Сохранение…' : 'Создать тип события'}</Button>
        </form>
      </CardContent>
    </Card>}
    <Button asChild variant="outline"><a href="/">На главную</a></Button>
  </div>
}
