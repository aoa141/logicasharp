HighValueSales(order_id:, amount:) :-
  `warehouse.sales_data.fact_orders`(order_id:, amount:, region: "US-West"),
  amount > 5000;
