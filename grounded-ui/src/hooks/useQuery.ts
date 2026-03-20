import { useMutation } from '@tanstack/react-query'
import { postQuery } from '@/lib/api'
import type { QueryRequest } from '@/types/api'

export function useAnalyticsQuery() {
  return useMutation({
    mutationFn: (req: QueryRequest) => postQuery(req),
  })
}
