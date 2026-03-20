import * as React from 'react'
import { cn } from '@/lib/utils'

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {}

const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, type, ...props }, ref) => {
    return (
      <input
        type={type}
        className={cn(
          'flex h-10 w-full bg-zinc-900 border border-zinc-700/60 px-4 py-2 text-sm text-zinc-100 placeholder:text-zinc-500 transition-colors',
          'focus:outline-none focus:border-amber-500/60 focus:ring-0',
          'disabled:cursor-not-allowed disabled:opacity-50',
          'font-mono',
          className
        )}
        ref={ref}
        {...props}
      />
    )
  }
)
Input.displayName = 'Input'

export { Input }
