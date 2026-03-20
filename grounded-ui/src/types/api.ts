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

export interface QueryTrace {
  requestId: string
  traceId: string
  plannerStatus: string
  failureCategory?: string
  durationMs?: number
  llmLatencyMs?: number
  rowCount?: number
  compiledSql?: string
  queryPlan?: unknown
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
