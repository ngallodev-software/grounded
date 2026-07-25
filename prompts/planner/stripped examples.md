
Question: What was total revenue last month?
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"revenue","timeRange":{"preset":"last_month","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: How many orders were placed last quarter?
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"order_count","timeRange":{"preset":"last_quarter","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: What is the average order value this year?
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"average_order_value","timeRange":{"preset":"year_to_date","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: Show revenue by product category for the last 90 days where sales channel is Web.
{"version":"1.0","questionType":"grouped_breakdown","dimension":"product_category","filters":[{"field":"sales_channel","operator":"eq","values":["Web"]}],"metric":"revenue","timeRange":{"preset":"last_90_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: Units sold by channel last 30 days.
{"version":"1.0","questionType":"grouped_breakdown","dimension":"sales_channel","filters":[],"metric":"units_sold","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: Revenue by shipping region last year.
{"version":"1.0","questionType":"grouped_breakdown","dimension":"shipping_region","filters":[],"metric":"revenue","timeRange":{"preset":"last_year","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: Revenue by customer region last quarter.
{"version":"1.0","questionType":"grouped_breakdown","dimension":"customer_region","filters":[],"metric":"revenue","timeRange":{"preset":"last_quarter","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: Top 5 products by units sold this year.
{"version":"1.0","questionType":"ranking","dimension":"product_name","filters":[],"metric":"units_sold","timeRange":{"preset":"year_to_date","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false}

Question: Top products by units sold this year.
{"version":"1.0","questionType":"ranking","dimension":"product_name","filters":[],"metric":"units_sold","timeRange":{"preset":"year_to_date","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false}

Question: Top 10 products by revenue last month.
{"version":"1.0","questionType":"ranking","dimension":"product_name","filters":[],"metric":"revenue","timeRange":{"preset":"last_month","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":10,"usePriorState":false}

Question: Top 5 customers by order count last year.
{"version":"1.0","questionType":"ranking","dimension":"customer_name","filters":[],"metric":"order_count","timeRange":{"preset":"last_year","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false}

Question: Which 3 categories had the lowest units sold last quarter?
{"version":"1.0","questionType":"ranking","dimension":"product_category","filters":[],"metric":"units_sold","timeRange":{"preset":"last_quarter","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"asc"},"limit":3,"usePriorState":false}

Question: Monthly revenue for the last 6 months.
{"version":"1.0","questionType":"time_series","dimension":null,"filters":[],"metric":"revenue","timeRange":{"preset":"last_6_months","startDate":null,"endDate":null},"timeGrain":"month","sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: What was total revenue in 2024?
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"revenue","timeRange":{"preset":"custom_range","startDate":"2024-01-01","endDate":"2024-12-31"},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: Average order value in 2024.
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"average_order_value","timeRange":{"preset":"custom_range","startDate":"2024-01-01","endDate":"2024-12-31"},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: Show units sold by category for 2025.
{"version":"1.0","questionType":"grouped_breakdown","dimension":"product_category","filters":[],"metric":"units_sold","timeRange":{"preset":"custom_range","startDate":"2025-01-01","endDate":"2025-12-31"},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: Show gross margin by channel last month.
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"__unsupported__","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: Forecast revenue for next quarter.
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"__unsupported__","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: SELECT product_name, SUM(quantity) FROM order_items GROUP BY product_name;
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"__unsupported__","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: Show revenue by product category where country is Canada.
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"__unsupported__","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}

Question: Show revenue by product category where sales channel is Retail.
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"__unsupported__","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
