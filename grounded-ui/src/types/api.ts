export interface QueryRequest {
  question: string
  conversationId?: string
}

export interface QueryMetadata {
  rowCount: number
  durationMs: number
  llmLatencyMs: number
}

export interface QueryAnswer {
  summary: string
  keyPoints: string[]
  tableIncluded: boolean
}

export interface LlmStageTrace {
  provider: string
  modelName: string
  latencyMs?: number
  tokensIn: number
  tokensOut: number
  failureCategory: string
  // planner-specific
  promptKey?: string
  promptVersion?: string
  parseSucceeded?: boolean
  repairAttempted?: boolean
  cacheHit?: boolean
  failureMessage?: string | null
  // synthesizer-specific
  errorMessage?: string | null
}

export interface QueryTrace {
  requestId: string
  traceId: string
  plannerStatus: string
  synthesisStatus?: string
  finalStatus?: string
  failureCategory?: string
  durationMs?: number
  llmLatencyMs?: number
  rowCount?: number
  compiledSql?: string
  queryPlan?: unknown
  planner?: LlmStageTrace
  synthesizer?: LlmStageTrace
  startedAt?: string
  completedAt?: string
}

export interface QuerySuccessResponse {
  status: 'success'
  rows: Record<string, unknown>[]
  metadata: QueryMetadata
  answer: QueryAnswer
  trace: QueryTrace
}

export interface ValidationError {
  code: string
  message: string
}

export interface QueryErrorResponse {
  status: 'error'
  failureCategory: string
  errors: ValidationError[]
  trace: QueryTrace
}

export type QueryResponse = QuerySuccessResponse | QueryErrorResponse
