import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Skeleton } from '@/components/ui/skeleton'
import { Badge } from '@/components/ui/badge'
import type { QuerySuccessResponse, QueryErrorResponse } from '@/types/api'

interface InternalsPanelProps {
  response: QuerySuccessResponse | QueryErrorResponse | null
  isLoading: boolean
}

function CodeBlock({ content }: { content: string }) {
  return (
    <pre className="text-xs font-mono text-zinc-400 leading-relaxed overflow-auto p-5 whitespace-pre-wrap break-all">
      {content}
    </pre>
  )
}

function TraceRow({ label, value, accent }: { label: string; value: string | undefined; accent?: boolean }) {
  if (value === undefined || value === null) return null
  return (
    <div className="flex items-start gap-4 py-3 border-b border-zinc-800/40 last:border-0 px-5">
      <span className="text-[10px] font-mono text-zinc-600 uppercase tracking-widest w-32 shrink-0 pt-0.5">
        {label}
      </span>
      <span className={`text-xs font-mono break-all ${accent ? 'text-amber-400' : 'text-zinc-300'}`}>
        {value}
      </span>
    </div>
  )
}

export function InternalsPanel({ response, isLoading }: InternalsPanelProps) {
  return (
    <Tabs defaultValue="trace" className="flex flex-col h-full">
      <TabsList className="shrink-0">
        <TabsTrigger value="trace">Trace</TabsTrigger>
        <TabsTrigger value="plan">Plan</TabsTrigger>
        <TabsTrigger value="sql">SQL</TabsTrigger>
      </TabsList>

      <TabsContent value="trace" className="flex-1 overflow-auto">
        {isLoading ? (
          <div className="space-y-3 p-5">
            <Skeleton className="h-6 w-full" />
            <Skeleton className="h-6 w-3/4" />
            <Skeleton className="h-6 w-5/6" />
            <Skeleton className="h-6 w-2/3" />
          </div>
        ) : !response ? (
          <div className="flex items-center justify-center h-full py-12">
            <span className="text-zinc-700 text-xs font-mono">no trace yet</span>
          </div>
        ) : (
          <div className="divide-y divide-zinc-800/0">
            <TraceRow label="request id" value={response.trace.requestId} />
            <TraceRow label="trace id" value={response.trace.traceId} />
            <TraceRow
              label="planner"
              value={response.trace.plannerStatus}
              accent={response.trace.plannerStatus === 'completed'}
            />
            {response.trace.failureCategory && (
              <div className="px-5 py-3 border-b border-zinc-800/40">
                <span className="text-[10px] font-mono text-zinc-600 uppercase tracking-widest block mb-2">failure</span>
                <Badge variant="error">{response.trace.failureCategory}</Badge>
              </div>
            )}
            <TraceRow
              label="duration"
              value={
                response.trace.durationMs !== undefined
                  ? `${response.trace.durationMs}ms`
                  : response.status === 'success'
                  ? `${response.metadata.durationMs}ms`
                  : undefined
              }
            />
            <TraceRow
              label="llm latency"
              value={
                response.trace.llmLatencyMs !== undefined
                  ? `${response.trace.llmLatencyMs}ms`
                  : response.status === 'success'
                  ? `${response.metadata.llmLatencyMs}ms`
                  : undefined
              }
            />
            <TraceRow
              label="rows"
              value={
                response.trace.rowCount !== undefined
                  ? String(response.trace.rowCount)
                  : response.status === 'success'
                  ? String(response.metadata.rowCount)
                  : undefined
              }
            />
            <TraceRow label="status" value={response.status} accent={response.status === 'success'} />
          </div>
        )}
      </TabsContent>

      <TabsContent value="plan" className="flex-1 overflow-auto">
        {isLoading ? (
          <div className="space-y-2 p-5">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-4/5" />
            <Skeleton className="h-4 w-3/5" />
          </div>
        ) : !response ? (
          <div className="flex items-center justify-center h-full py-12">
            <span className="text-zinc-700 text-xs font-mono">no plan yet</span>
          </div>
        ) : response.status === 'success' && response.trace.queryPlan ? (
          <CodeBlock content={JSON.stringify(response.trace.queryPlan, null, 2)} />
        ) : response.status === 'error' ? (
          <div className="p-5">
            <span className="text-xs font-mono text-zinc-600">
              No plan — request did not reach the planner or planning failed.
            </span>
          </div>
        ) : (
          <div className="p-5">
            <span className="text-xs font-mono text-zinc-600">
              Plan not included in trace response.
            </span>
          </div>
        )}
      </TabsContent>

      <TabsContent value="sql" className="flex-1 overflow-auto">
        {isLoading ? (
          <div className="space-y-2 p-5">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-3/4" />
          </div>
        ) : !response ? (
          <div className="flex items-center justify-center h-full py-12">
            <span className="text-zinc-700 text-xs font-mono">no sql yet</span>
          </div>
        ) : response.trace.compiledSql ? (
          <CodeBlock content={response.trace.compiledSql} />
        ) : (
          <div className="p-5">
            <span className="text-xs font-mono text-zinc-600">
              Compiled SQL not included in trace response.
            </span>
          </div>
        )}
      </TabsContent>
    </Tabs>
  )
}
