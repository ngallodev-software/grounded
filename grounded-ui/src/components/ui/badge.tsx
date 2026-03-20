import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const badgeVariants = cva(
  'inline-flex items-center px-2 py-0.5 text-xs font-mono font-medium transition-colors',
  {
    variants: {
      variant: {
        default: 'bg-zinc-800 text-zinc-300 border border-zinc-700',
        success: 'bg-emerald-950/60 text-emerald-400 border border-emerald-800/60',
        error: 'bg-red-950/60 text-red-400 border border-red-800/60',
        warning: 'bg-amber-950/60 text-amber-400 border border-amber-800/60',
        amber: 'bg-amber-500/10 text-amber-400 border border-amber-500/30',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  }
)

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {
  children?: React.ReactNode
}

function Badge({ className, variant, children, ...props }: BadgeProps) {
  return <div className={cn(badgeVariants({ variant }), className)} {...props}>{children}</div>
}

export { Badge, badgeVariants }
