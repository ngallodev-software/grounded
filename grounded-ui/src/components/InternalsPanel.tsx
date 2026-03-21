import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Skeleton } from '@/components/ui/skeleton'
import { Badge } from '@/components/ui/badge'
import { EvalPanel } from '@/components/EvalPanel'
import type { QuerySuccessResponse, QueryErrorResponse, LlmStageTrace } from '@/types/api'

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

// Cost per 1M tokens for known models (input / output)
const MODEL_PRICING: Record<string, { input: number; output: number; cached?: number }> = {
  'gpt-4o':           { input: 2.50,  output: 10.00, cached: 1.25 },
  'gpt-4o-mini':      { input: 0.15,  output: 0.60,  cached: 0.075 },
  'gpt-4-turbo':      { input: 10.00, output: 30.00 },
  'gpt-4':            { input: 30.00, output: 60.00 },
  'gpt-3.5-turbo':    { input: 0.50,  output: 1.50 },
  'claude-opus-4-6':     { input: 15.00, output: 75.00, cached: 1.50 },
  'claude-sonnet-4-6':   { input: 3.00,  output: 15.00, cached: 0.30 },
  'claude-haiku-4-5-20251001':    { input: 0.80,  output: 4.00,  cached: 0.08 },
}

function estimateCost(model: string, tokensIn: number, tokensOut: number): string | null {
  const pricing = MODEL_PRICING[model]
  if (!pricing) return null
  const cost = (tokensIn / 1_000_000) * pricing.input + (tokensOut / 1_000_000) * pricing.output
  if (cost < 0.000001) return '<$0.000001'
  return `$${cost.toFixed(6)}`
}

function StageTokenBlock({ label, stage }: { label: string; stage: LlmStageTrace }) {
  const cost = estimateCost(stage.modelName, stage.tokensIn, stage.tokensOut)
  const isDeterministic = stage.provider === 'deterministic'

  return (
    <div className="border-b border-zinc-800/40 last:border-0">
      {/* Stage header */}
      <div className="flex items-center gap-3 px-5 pt-3 pb-2">
        <span className="text-[10px] font-mono text-zinc-500 uppercase tracking-widest">{label}</span>
        <span className="text-[10px] font-mono text-zinc-700">{stage.modelName}</span>
        {isDeterministic && (
          <span className="text-[10px] font-mono text-zinc-700 italic">deterministic</span>
        )}
        {stage.latencyMs !== undefined && (
          <span className="ml-auto text-[10px] font-mono text-zinc-600">{stage.latencyMs}ms</span>
        )}
      </div>

      {/* Token grid */}
      <div className="grid grid-cols-3 gap-px bg-zinc-800/30 mx-5 mb-3 border border-zinc-800/40">
        <div className="bg-zinc-950 px-3 py-2">
          <div className="text-[9px] font-mono text-zinc-600 uppercase tracking-widest mb-0.5">tokens in</div>
          <div className="text-xs font-mono text-zinc-300">{stage.tokensIn.toLocaleString()}</div>
        </div>
        <div className="bg-zinc-950 px-3 py-2">
          <div className="text-[9px] font-mono text-zinc-600 uppercase tracking-widest mb-0.5">tokens out</div>
          <div className="text-xs font-mono text-zinc-300">{stage.tokensOut.toLocaleString()}</div>
        </div>
        <div className="bg-zinc-950 px-3 py-2">
          <div className="text-[9px] font-mono text-zinc-600 uppercase tracking-widest mb-0.5">est. cost</div>
          <div className={`text-xs font-mono ${cost ? 'text-amber-400/80' : 'text-zinc-600'}`}>
            {isDeterministic ? 'n/a' : (cost ?? 'unknown')}
          </div>
        </div>
      </div>
    </div>
  )
}

function TotalCostRow({ planner, synthesizer }: { planner?: LlmStageTrace; synthesizer?: LlmStageTrace }) {
  if (!planner && !synthesizer) return null

  let totalCost = 0
  let hasKnownCost = false

  for (const stage of [planner, synthesizer]) {
    if (!stage || stage.provider === 'deterministic') continue
    const pricing = MODEL_PRICING[stage.modelName]
    if (!pricing) continue
    hasKnownCost = true
    totalCost += (stage.tokensIn / 1_000_000) * pricing.input + (stage.tokensOut / 1_000_000) * pricing.output
  }

  if (!hasKnownCost) return null

  return (
    <div className="flex items-center gap-4 px-5 py-3 border-b border-zinc-800/40 bg-zinc-900/30">
      <span className="text-[10px] font-mono text-zinc-600 uppercase tracking-widest w-32 shrink-0">
        total est.
      </span>
      <span className="text-xs font-mono text-amber-400">
        {totalCost < 0.000001 ? '<$0.000001' : `$${totalCost.toFixed(6)}`}
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
        <TabsTrigger value="eval">Eval</TabsTrigger>
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
            {/* IDs + status */}
            <TraceRow label="request id" value={response.trace.requestId} />
            <TraceRow label="trace id" value={response.trace.traceId} />
            <TraceRow
              label="planner"
              value={response.trace.plannerStatus}
              accent={response.trace.plannerStatus === 'completed'}
            />
            <TraceRow
              label="synthesis"
              value={response.trace.synthesisStatus}
              accent={response.trace.synthesisStatus === 'completed'}
            />
            <TraceRow label="status" value={response.trace.finalStatus ?? response.status} accent={(response.trace.finalStatus ?? response.status) === 'success'} />
            {response.trace.failureCategory && response.trace.failureCategory !== 'none' && (
              <div className="px-5 py-3 border-b border-zinc-800/40">
                <span className="text-[10px] font-mono text-zinc-600 uppercase tracking-widest block mb-2">failure</span>
                <Badge variant="error">{response.trace.failureCategory}</Badge>
              </div>
            )}

            {/* Timing */}
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
              label="rows"
              value={
                response.trace.rowCount !== undefined
                  ? String(response.trace.rowCount)
                  : response.status === 'success'
                  ? String(response.metadata.rowCount)
                  : undefined
              }
            />

            {/* LLM stage token metrics */}
            {(response.trace.planner || response.trace.synthesizer) && (
              <div className="pt-1">
                <div className="px-5 py-2">
                  <span className="text-[10px] font-mono text-zinc-600 uppercase tracking-widest">
                    LLM stages
                  </span>
                </div>
                {response.trace.planner && (
                  <StageTokenBlock label="planner" stage={response.trace.planner} />
                )}
                {response.trace.synthesizer && (
                  <StageTokenBlock label="synthesizer" stage={response.trace.synthesizer} />
                )}
                <TotalCostRow
                  planner={response.trace.planner}
                  synthesizer={response.trace.synthesizer}
                />
              </div>
            )}
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
      <TabsContent value="eval" className="flex-1 overflow-hidden">
        <EvalPanel />
      </TabsContent>
    </Tabs>
  )
}
