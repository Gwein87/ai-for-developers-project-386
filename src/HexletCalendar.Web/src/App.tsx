import { useState } from 'react'
import { CalendarDays } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

type ApiStatus = 'idle' | 'checking' | 'healthy' | 'unavailable'

const statusLabels: Record<ApiStatus, string> = {
  idle: 'Соединение с API ещё не проверено.',
  checking: 'Проверяем соединение…',
  healthy: 'API доступен.',
  unavailable: 'API недоступен. Убедитесь, что бэкенд запущен.',
}

export default function App() {
  const [status, setStatus] = useState<ApiStatus>('idle')

  async function checkApi() {
    setStatus('checking')
    try {
      const response = await fetch('/api/health', { signal: AbortSignal.timeout(5000) })
      const body = await response.text()
      setStatus(response.ok && body.trim() === 'Healthy' ? 'healthy' : 'unavailable')
    } catch {
      setStatus('unavailable')
    }
  }

  return (
    <main className="mx-auto flex min-h-svh max-w-3xl flex-col justify-center gap-8 px-6 py-16">
      <header className="space-y-4">
        <div className="flex items-center gap-3 text-sm font-medium text-muted-foreground">
          <CalendarDays aria-hidden="true" className="size-5" />
          Hexlet Calendar
        </div>
        <h1 className="text-4xl font-semibold tracking-tight sm:text-5xl">Календарь звонков</h1>
        <p className="max-w-xl leading-7 text-muted-foreground">
          Здесь появятся типы встреч, свободные интервалы и запись на звонок.
        </p>
      </header>
      <Card>
        <CardHeader>
          <CardTitle>Проект готов к разработке</CardTitle>
          <CardDescription>
            React, TypeScript, Vite и shadcn/ui. Бэкенд — ASP.NET Core 10.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col items-start gap-4">
          <Button onClick={checkApi} disabled={status === 'checking'}>
            {status === 'checking' ? 'Проверяем…' : 'Проверить API'}
          </Button>
          <p role="status" className="text-sm text-muted-foreground">{statusLabels[status]}</p>
        </CardContent>
      </Card>
    </main>
  )
}
