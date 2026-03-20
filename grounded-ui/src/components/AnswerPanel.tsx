import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import type { QuerySuccessResponse, QueryErrorResponse } from '@/types/api'

interface AnswerPanelProps {
  response: QuerySuccessResponse | QueryErrorResponse | null
  isLoading: boolean
}

export function AnswerPanel({ response, isLoading }: AnswerPanelProps) {
  if (isLoading) {
    return (
      <div className="space-y-4 p-6">
        <Skeleton className="h-4 w-3/4" />
        <Skeleton className="h-4 w-1/2" />
        <Skeleton className="h-4 w-5/6" />
        <div className="pt-2 space-y-2">
          <Skeleton className="h-8 w-full" />
          <Skeleton className="h-8 w-full" />
          <Skeleton className="h-8 w-full" />
          <Skeleton className="h-8 w-full" />
        </div>
      </div>
    )
  }

  if (!response) {
    return (
      <div className="flex flex-col items-center justify-center h-full py-20 text-center px-8">
        <div className="w-12 h-12 mb-6 rounded-full border border-zinc-800 flex items-center justify-center">
          <span className="text-zinc-600 text-lg font-mono">?</span>
        </div>
        <p className="text-zinc-600 text-sm font-mono leading-relaxed max-w-xs">
          Ask a natural language question about your analytics data.
        </p>
        <div className="mt-8 flex flex-col gap-2 w-full max-w-xs">
          {[
            'Revenue by category last quarter',
            'Top 10 products by units sold',
            'Monthly revenue for last 6 months',
          ].map((ex) => (
            <div
              key={ex}
              className="text-xs font-mono text-zinc-600 border border-zinc-800/60 px-3 py-2 text-left"
            >
              {ex}
            </div>
          ))}
        </div>
      </div>
    )
  }

  if (response.status === 'error') {
    return (
      <div className="p-6 space-y-4">
        <div className="flex items-center gap-3">
          <Badge variant="error">error</Badge>
          <span className="text-xs font-mono text-zinc-500">{response.failureCategory}</span>
        </div>
        <div className="space-y-2">
          {response.errors.map((err, i) => (
            <div key={i} className="border border-red-900/30 bg-red-950/20 px-4 py-3">
              <span className="text-xs font-mono text-red-400 block">{err.code}</span>
              <span className="text-sm text-zinc-400 mt-1 block">{err.message}</span>
            </div>
          ))}
        </div>
      </div>
    )
  }

  const { answer, rows, metadata } = response

  return (
    <div className="flex flex-col h-full">
      {/* Answer summary */}
      <div className="p-6 border-b border-zinc-800/60">
        <p className="text-zinc-200 text-sm leading-relaxed font-[Fraunces] text-base">
          {answer.summary}
        </p>
        {answer.keyPoints.length > 0 && (
          <ul className="mt-4 space-y-1.5">
            {answer.keyPoints.map((point, i) => (
              <li key={i} className="flex items-start gap-2.5 text-sm text-zinc-400">
                <span className="text-amber-500/70 mt-0.5 font-mono text-xs shrink-0">—</span>
                <span>{point}</span>
              </li>
            ))}
          </ul>
        )}
        <div className="flex items-center gap-3 mt-4">
          <span className="text-xs font-mono text-zinc-600">
            {metadata.rowCount} row{metadata.rowCount !== 1 ? 's' : ''}
          </span>
          <span className="text-zinc-800">·</span>
          <span className="text-xs font-mono text-zinc-600">{metadata.durationMs}ms</span>
          <span className="text-zinc-800">·</span>
          <span className="text-xs font-mono text-zinc-600">{metadata.llmLatencyMs}ms llm</span>
        </div>
      </div>

      {/* Results table */}
      {rows.length > 0 && (
        <div className="flex-1 overflow-auto">
          <table className="w-full text-xs font-mono">
            <thead className="sticky top-0 bg-zinc-950/95 backdrop-blur-sm">
              <tr className="border-b border-zinc-800">
                {Object.keys(rows[0]).map((col) => (
                  <th
                    key={col}
                    className="px-4 py-3 text-left text-zinc-500 font-medium uppercase tracking-widest text-[10px] whitespace-nowrap"
                  >
                    {col}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((row, i) => (
                <tr
                  key={i}
                  className="border-b border-zinc-800/40 hover:bg-zinc-900/60 transition-colors"
                >
                  {Object.values(row).map((val, j) => (
                    <td key={j} className="px-4 py-3 text-zinc-300 whitespace-nowrap">
                      {String(val ?? '')}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
