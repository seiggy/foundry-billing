import type { PropsWithChildren, ReactNode } from 'react'

interface PanelProps extends PropsWithChildren {
  title: string
  subtitle?: string
  aside?: ReactNode
}

export function Panel({ title, subtitle, aside, children }: PanelProps) {
  return (
    <section className="panel">
      <header className="panel-header">
        <div>
          <h3 className="panel-title">{title}</h3>
          {subtitle ? <p className="panel-subtitle">{subtitle}</p> : null}
        </div>
        {aside}
      </header>
      {children}
    </section>
  )
}
