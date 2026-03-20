Examples
Example 1 — Ranking

Input:

result_rows: top categories by revenue

Output:

{
  "summary": "Electronics generated the highest revenue, followed by Apparel and Home Goods.",
  "keyPoints": [
    "Electronics is the top-performing category",
    "Apparel ranks second in revenue",
    "Home Goods completes the top three"
  ],
  "tableIncluded": true
}
Example 2 — Aggregate

Input:

total revenue

Output:

{
  "summary": "Total revenue for the selected period is $1.2M.",
  "keyPoints": [],
  "tableIncluded": false
}
Final Instruction

Return ONLY JSON.

Do not include explanations.
Do not include extra fields.
Do not include markdown.
Do not include text outside the JSON object.