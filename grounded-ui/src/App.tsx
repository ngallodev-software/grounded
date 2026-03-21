import { useState, useCallback, useRef } from 'react'
import { v4 as uuidv4 } from 'uuid'
import { QueryInput } from '@/components/QueryInput'
import { AnswerPanel } from '@/components/AnswerPanel'
import { InternalsPanel } from '@/components/InternalsPanel'
import { AuthGate } from '@/components/AuthGate'
import { useAnalyticsQuery } from '@/hooks/useQuery'
import type { QuerySuccessResponse, QueryErrorResponse } from '@/types/api'

const AUTH_ENABLED = import.meta.env.VITE_AUTH_ENABLED === 'true'

// Stable conversation ID per browser session
const SESSION_CONVERSATION_ID = uuidv4()

function useAuth() {
  const [unlocked, setUnlocked] = useState(!AUTH_ENABLED)
  return { unlocked, unlock: () => setUnlocked(true) }
}

export default function App() {
  const { unlocked, unlock } = useAuth()
  const { mutate, isPending, data, reset } = useAnalyticsQuery()
  const [lastQuestion, setLastQuestion] = useState<string | null>(null)
  const [pendingQuestion, setPendingQuestion] = useState<string | null>(null)
  const [inputResetKey, setInputResetKey] = useState(0)
  const resultRef = useRef<HTMLDivElement>(null)

  const handleReset = useCallback(() => {
    reset()
    setLastQuestion(null)
    setPendingQuestion(null)
    setInputResetKey(k => k + 1)
  }, [reset])

  const handleSubmit = useCallback(
    (question: string) => {
      setLastQuestion(question)
      setPendingQuestion(null)
      mutate(
        { question, conversationId: SESSION_CONVERSATION_ID },
        {
          onSuccess: () => {
            setTimeout(() => {
              resultRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
            }, 100)
          },
        }
      )
    },
    [mutate]
  )

  if (!unlocked) {
    return <AuthGate onUnlock={unlock} />
  }

  const response = data as QuerySuccessResponse | QueryErrorResponse | undefined

  return (
    <div className="min-h-screen bg-zinc-950 flex flex-col">
      {/* Header */}
      <header className="border-b border-zinc-800/60 px-6 py-4 flex items-center justify-between shrink-0">
        <button
          onClick={handleReset}
          className="flex items-center gap-3 hover:opacity-70 transition-opacity"
          aria-label="Reset to home"
        >
          <span className="text-zinc-100 font-[Fraunces] font-light text-lg tracking-tight">
            Grounded
          </span>
          <span className="text-zinc-800 font-mono text-xs">·</span>
          <span className="text-zinc-600 font-mono text-xs">analytics</span>
        </button>
        <div className="flex items-center gap-2">
          {lastQuestion && (
            <span className="text-[11px] font-mono text-zinc-700 max-w-[300px] truncate hidden sm:block">
              {lastQuestion}
            </span>
          )}
          {isPending && (
            <span className="flex items-center gap-1.5 text-[11px] font-mono text-amber-500/70">
              <span className="inline-block w-1.5 h-1.5 rounded-full bg-amber-500 animate-pulse" />
              running
            </span>
          )}
        </div>
      </header>

      {/* Query input */}
      <div className="px-6 py-5 border-b border-zinc-800/40 shrink-0">
        <div className="max-w-4xl mx-auto">
          <QueryInput
            onSubmit={handleSubmit}
            isLoading={isPending}
            prefill={pendingQuestion}
            onPrefillConsumed={() => setPendingQuestion(null)}
            resetKey={inputResetKey}
          />
        </div>
      </div>

      {/* Results */}
      <div ref={resultRef} className="flex-1 flex flex-col lg:flex-row min-h-0">
        {/* Left: Answer + Table */}
        <div className="flex-1 min-w-0 border-b lg:border-b-0 lg:border-r border-zinc-800/60 overflow-auto">
          <div className="h-full">
            <AnswerPanel
              response={response ?? null}
              isLoading={isPending}
              onSelectQuestion={setPendingQuestion}
            />
          </div>
        </div>

        {/* Right: Internals */}
        <div className="w-full lg:w-[420px] xl:w-[480px] shrink-0 flex flex-col min-h-[300px] lg:min-h-0">
          <InternalsPanel response={response ?? null} isLoading={isPending} />
        </div>
      </div>

      {/* Footer */}
      <footer className="border-t border-zinc-800/40 px-6 py-3 flex items-center justify-between shrink-0">
        <span className="text-[11px] font-mono text-zinc-700">
          NL → QueryPlan → SQL → Answer
        </span>
        <span className="text-[11px] font-mono text-zinc-700 hidden sm:block">
          conv:{SESSION_CONVERSATION_ID.slice(0, 8)}
        </span>
      </footer>
    </div>
  )
}
