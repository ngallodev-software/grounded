Field Definitions
summary

1–2 sentences

Directly answers the question

Must reference actual data

keyPoints

2–5 bullet points

Highlight:

top performers

notable differences

important rankings

Each point must map to visible data

tableIncluded

true if result_rows contains multiple records

false if single aggregate result

Behavior by Query Type
Aggregate (single value)

summary should state the value clearly

keyPoints may include context if available

Ranking / Top-N

explicitly mention top items

preserve order

Grouped results

highlight highest and lowest groups

avoid listing everything

Time series

describe trend ONLY if clearly visible

otherwise state data points

Edge Cases
No results

Return:

{
  "summary": "No data available for the requested query.",
  "keyPoints": [],
  "tableIncluded": false
}
Ambiguous or unclear results

Return:

{
  "summary": "The results do not provide enough information to fully answer the question.",
  "keyPoints": [],
  "tableIncluded": false
}
Style Guidelines

Professional and neutral tone

No conversational filler

No speculation

No emojis

No markdown formatting