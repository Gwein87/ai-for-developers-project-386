const zone = 'Europe/Moscow'

export function moscowTime(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { timeZone: zone, hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

export function moscowDateLabel(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { timeZone: zone, day: 'numeric', month: 'long', year: 'numeric' }).format(new Date(value))
}

export function moscowCalendarDate(value: number) {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit',
  }).formatToParts(new Date(value))
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(item => item.type === type)?.value ?? ''
  return `${part('year')}-${part('month')}-${part('day')}`
}

export function moscowShortDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short', timeZone: zone })
    .format(new Date(`${value}T12:00:00Z`))
}
