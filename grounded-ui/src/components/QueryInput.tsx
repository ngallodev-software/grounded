import React, { useState, useRef, useEffect } from 'react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

interface QueryInputProps {
  onSubmit: (question: string) => void
  isLoading: boolean
  prefill?: string | null
  onPrefillConsumed?: () => void
  resetKey?: number
}

export function QueryInput({ onSubmit, isLoading, prefill, onPrefillConsumed, resetKey }: QueryInputProps) {
  const [value, setValue] = useState('')

  useEffect(() => {
    if (resetKey !== undefined) {
      setValue('')
      textareaRef.current?.focus()
    }
  }, [resetKey])
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto'
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`
    }
  }, [value])

  useEffect(() => {
    if (prefill) {
      setValue(prefill)
      onPrefillConsumed?.()
      textareaRef.current?.focus()
    }
  }, [prefill, onPrefillConsumed])

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const q = value.trim()
    if (!q || isLoading) return
    onSubmit(q)
  }

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      const q = value.trim()
      if (!q || isLoading) return
      onSubmit(q)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="relative group">
      <div
        className={cn(
          'relative flex items-end gap-0 border transition-all duration-200',
          'border-zinc-700/60 bg-zinc-900/80',
          'focus-within:border-amber-500/50 focus-within:bg-zinc-900',
          'shadow-lg shadow-black/20'
        )}
      >
        <textarea
          ref={textareaRef}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Ask an analytics question — e.g. Revenue by category last quarter"
          rows={1}
          maxLength={500}
          disabled={isLoading}
          className={cn(
            'flex-1 resize-none bg-transparent px-5 py-4 text-sm text-zinc-100',
            'placeholder:text-zinc-600 font-mono leading-relaxed',
            'focus:outline-none min-h-[52px] max-h-[200px] overflow-y-auto',
            'disabled:opacity-50 disabled:cursor-not-allowed'
          )}
        />
        <div className="flex items-end pb-2 pr-2 shrink-0 gap-2">
          {value.length > 200 && (
            <span className={cn(
              'text-[10px] font-mono tabular-nums self-end pb-2.5',
              value.length >= 490 ? 'text-red-400' : value.length >= 400 ? 'text-amber-500/70' : 'text-zinc-600'
            )}>
              {value.length}/500
            </span>
          )}
          <Button
            type="submit"
            disabled={isLoading || !value.trim()}
            size="sm"
            className="h-8 px-4 text-xs"
          >
            {isLoading ? (
              <span className="flex items-center gap-2">
                <span className="inline-block w-3 h-3 border border-zinc-950/30 border-t-zinc-950 rounded-full animate-spin" />
                Running
              </span>
            ) : (
              'Run'
            )}
          </Button>
        </div>
      </div>
      <div className="absolute bottom-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-amber-500/0 to-transparent group-focus-within:via-amber-500/40 transition-all duration-500 pointer-events-none" />
    </form>
  )
}
