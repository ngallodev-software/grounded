import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import type { QuerySuccessResponse, QueryErrorResponse } from '@/types/api'

interface AnswerPanelProps {
  response: QuerySuccessResponse | QueryErrorResponse | null
  isLoading: boolean
  onSelectQuestion?: (question: string) => void
}

const SUGGESTED_QUESTIONS = [
  'Total revenue last month',
  'Revenue by product category last quarter',
  'Top 10 products by units sold this year',
  'Monthly revenue for last 6 months',
  'Average order value in 2024',
  'Revenue by shipping region last year',
  'Units sold by sales channel last 90 days',
  'Top 5 customers by order count last year',
]

const SCHEMA_SECTIONS = [
  {
    label: 'Metrics',
    color: 'text-amber-400',
    items: [
      { name: 'revenue', desc: 'sum of (quantity × unit_price − discount)' },
      { name: 'order_count', desc: 'number of orders' },
      { name: 'units_sold', desc: 'total quantity across completed orders' },
      { name: 'average_order_value', desc: 'revenue ÷ order count' },
      { name: 'new_customer_count', desc: 'customers placing their first order' },
    ],
  },
  {
    label: 'Dimensions',
    color: 'text-emerald-400',
    items: [
      { name: 'product_category', desc: 'Electronics · Home · Office · Fitness · Accessories' },
      { name: 'product_subcategory', desc: 'sub-level within a category' },
      { name: 'product_name', desc: 'individual product (use for ranking)' },
      { name: 'sales_channel', desc: 'Web · Mobile · Marketplace' },
      { name: 'shipping_region', desc: 'West · East · South · Central' },
      { name: 'customer_segment', desc: 'Consumer · SMB · Enterprise' },
      { name: 'customer_region', desc: 'West · East · South · Central' },
      { name: 'customer_name', desc: 'individual customer (use for ranking)' },
      { name: 'acquisition_channel', desc: 'Organic · Paid Search · Email · Affiliate · Social — new customers only' },
    ],
  },
  {
    label: 'Time presets',
    color: 'text-zinc-400',
    items: [
      { name: 'last 7 / 30 / 90 days', desc: '' },
      { name: 'last 6 / 12 months', desc: '' },
      { name: 'last month / quarter / year', desc: '' },
      { name: 'month / quarter / year to date', desc: '' },
      { name: 'specific year', desc: 'e.g. "in 2024" or "for 2025"' },
      { name: 'all time', desc: '' },
    ],
  },
  {
    label: 'Filters',
    color: 'text-zinc-400',
    items: [
      { name: 'sales_channel', desc: 'Web · Mobile · Marketplace' },
      { name: 'product_category', desc: 'Electronics · Home · Office · Fitness · Accessories' },
      { name: 'customer_segment', desc: 'Consumer · SMB · Enterprise' },
      { name: 'customer_region / shipping_region', desc: 'West · East · South · Central' },
      { name: 'acquisition_channel', desc: 'Organic · Paid Search · Email · Affiliate · Social' },
    ],
  },
]

function SchemaPanel() {
  return (
    <div className="border border-zinc-800/60 bg-zinc-900/40 divide-y divide-zinc-800/40">
      <div className="px-4 py-2.5 flex items-center gap-2">
        <span className="text-[10px] font-mono text-zinc-500 uppercase tracking-widest">Data schema</span>
        <span className="text-[10px] font-mono text-zinc-400">— what you can ask about</span>
      </div>
      {SCHEMA_SECTIONS.map((section) => (
        <div key={section.label} className="px-4 py-3 space-y-1.5">
          <span className={`text-[10px] font-mono uppercase tracking-widest ${section.color}`}>
            {section.label}
          </span>
          <div className="space-y-1 mt-1">
            {section.items.map((item) => (
              <div key={item.name} className="flex items-baseline gap-2">
                <span className="text-xs font-mono text-zinc-300 shrink-0">{item.name}</span>
                {item.desc && (
                  <span className="text-[11px] font-mono text-zinc-400 leading-tight">{item.desc}</span>
                )}
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}

export function AnswerPanel({ response, isLoading, onSelectQuestion }: AnswerPanelProps) {
  const [schemaOpen, setSchemaOpen] = useState(false)

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
      <div className="flex flex-col items-center py-12 px-8 gap-6">
        {/* Schema panel — collapsible above the ? */}
        <div className="w-full max-w-sm space-y-2">
          <div
            className={`overflow-hidden transition-all duration-300 ease-in-out ${
              schemaOpen ? 'max-h-[600px] opacity-100' : 'max-h-0 opacity-0'
            }`}
          >
            <SchemaPanel />
          </div>

          {/* ? button */}
          <div className="flex justify-center">
            <button
              onClick={() => setSchemaOpen((o) => !o)}
              className="w-10 h-10 rounded-full border border-zinc-800 hover:border-amber-500/50 flex items-center justify-center transition-all duration-200 group"
              aria-label={schemaOpen ? 'Hide schema' : 'Show data schema'}
            >
              <span
                className={`text-base font-mono transition-colors duration-200 ${
                  schemaOpen ? 'text-amber-400' : 'text-zinc-600 group-hover:text-zinc-400'
                }`}
              >
                {schemaOpen ? '×' : '?'}
              </span>
            </button>
          </div>
        </div>

        <p className="text-zinc-400 text-sm font-mono leading-relaxed text-center max-w-xs">
          Ask a natural language question about your analytics data.
        </p>

        {/* Suggested questions — clickable */}
        <div className="flex flex-col gap-2 w-full max-w-sm">
          {SUGGESTED_QUESTIONS.map((q) => (
            <button
              key={q}
              onClick={() => onSelectQuestion?.(q)}
              className="text-xs font-mono text-zinc-300 border border-zinc-800/60 px-3 py-2 text-left hover:border-amber-500/40 hover:text-zinc-100 hover:bg-zinc-900/60 transition-all duration-150"
            >
              {q}
            </button>
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
