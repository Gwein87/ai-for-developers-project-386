import { ArrowLeft, CalendarDays } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export default function App() {
  const pathname = window.location.pathname.replace(/\/$/, '')
  const section = pathname === '/booking'
    ? 'Запись на звонок'
    : pathname === '/admin'
      ? 'Админка'
      : null

  return (
    <div className="min-h-svh bg-background">
      <header className="border-b">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-4 px-6 py-5">
          <a href="/" className="flex items-center gap-2 font-semibold tracking-tight">
            <CalendarDays aria-hidden="true" className="size-5" />
            Hexlet Calendar
          </a>
          <nav aria-label="Основная навигация" className="flex items-center gap-2">
            <Button asChild>
              <a href="/booking" aria-current={section === 'Запись на звонок' ? 'page' : undefined}>
                Записаться
              </a>
            </Button>
            <Button asChild variant="outline">
              <a href="/admin" aria-current={section === 'Админка' ? 'page' : undefined}>
                Админка
              </a>
            </Button>
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-6 py-20 sm:py-32">
        {section ? (
          <Card className="max-w-xl">
            <CardHeader>
              <h1 className="text-2xl font-semibold tracking-tight">Раздел готовится</h1>
              <CardTitle>{section}</CardTitle>
              <CardDescription>Этот раздел скоро появится в сервисе.</CardDescription>
            </CardHeader>
            <CardContent>
              <Button asChild variant="outline">
                <a href="/">
                  <ArrowLeft aria-hidden="true" />
                  На главную
                </a>
              </Button>
            </CardContent>
          </Card>
        ) : (
          <div className="max-w-2xl space-y-6">
            <div className="flex size-14 items-center justify-center rounded-2xl bg-muted">
              <CalendarDays aria-hidden="true" className="size-7" />
            </div>
            <h1 className="text-4xl font-semibold tracking-tight sm:text-6xl sm:leading-tight">
              Календарь звонков
            </h1>
            <p className="max-w-xl text-lg leading-8 text-muted-foreground">
              Выберите тип встречи и удобное свободное время — сервис поможет записаться
              на звонок без долгих согласований.
            </p>
          </div>
        )}
      </main>
    </div>
  )
}
