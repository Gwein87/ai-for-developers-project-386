import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

function Card({ className, ...props }: ComponentProps<'section'>) {
  return <section className={cn('rounded-xl border bg-card text-card-foreground shadow-sm', className)} {...props} />
}

function CardHeader({ className, ...props }: ComponentProps<'div'>) {
  return <div className={cn('flex flex-col gap-2 p-6', className)} {...props} />
}

function CardTitle({ className, ...props }: ComponentProps<'h2'>) {
  return <h2 className={cn('text-xl font-semibold tracking-tight', className)} {...props} />
}

function CardDescription({ className, ...props }: ComponentProps<'p'>) {
  return <p className={cn('text-sm leading-6 text-muted-foreground', className)} {...props} />
}

function CardContent({ className, ...props }: ComponentProps<'div'>) {
  return <div className={cn('p-6 pt-0', className)} {...props} />
}

export { Card, CardHeader, CardTitle, CardDescription, CardContent }
