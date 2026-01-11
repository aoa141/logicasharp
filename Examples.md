# LogicaSharp Examples

This document shows Logica programs and their generated T-SQL for each integration test.

## Table of Contents

1. [Simple Facts](#1-simple-facts)
2. [Named Field Facts](#2-named-field-facts)
3. [Simple Rule with Filter](#3-simple-rule-with-filter)
4. [Rule with String Filter](#4-rule-with-string-filter)
5. [Join Rule (Grandparent)](#5-join-rule-grandparent)
6. [Self-Join (Siblings)](#6-self-join-siblings)
7. [Recursive CTE (Ancestors)](#7-recursive-cte-ancestors)
8. [Recursive CTE (Royal Lineage)](#8-recursive-cte-royal-lineage)
9. [Count Aggregation](#9-count-aggregation)
10. [Sum Aggregation](#10-sum-aggregation)
11. [Arithmetic in Rules](#11-arithmetic-in-rules)
12. [Multiple Conditions](#12-multiple-conditions)
13. [Negation](#13-negation)
14. [Multiple Rules (UNION)](#14-multiple-rules-union)

---

## 1. Simple Facts

Simple positional facts without named fields.

### Logica

```logica
@Engine("mssql");
Person("Alice", 30);
Person("Bob", 25);
Person("Carol", 35);
```

### Generated T-SQL

```sql
(SELECT 'Alice' AS [col0], 30 AS [col1])
UNION ALL
(SELECT 'Bob' AS [col0], 25 AS [col1])
UNION ALL
(SELECT 'Carol' AS [col0], 35 AS [col1])
```

---

## 2. Named Field Facts

Facts with explicitly named fields.

### Logica

```logica
@Engine("mssql");
Employee(name: "Alice", department: "Engineering", salary: 75000);
Employee(name: "Bob", department: "Marketing", salary: 65000);
Employee(name: "Carol", department: "Engineering", salary: 80000);
```

### Generated T-SQL

```sql
(SELECT 'Alice' AS [name], 'Engineering' AS [department], 75000 AS [salary])
UNION ALL
(SELECT 'Bob' AS [name], 'Marketing' AS [department], 65000 AS [salary])
UNION ALL
(SELECT 'Carol' AS [name], 'Engineering' AS [department], 80000 AS [salary])
```

---

## 3. Simple Rule with Filter

A rule that filters data based on a numeric condition.

### Logica

```logica
@Engine("mssql");
Employee(name: "Alice", salary: 75000);
Employee(name: "Bob", salary: 65000);
Employee(name: "Carol", salary: 80000);
Employee(name: "David", salary: 55000);

HighEarner(name:) :- Employee(name:, salary:), salary > 70000;
```

### Generated T-SQL

```sql
SELECT employee0.[name] AS [name]
FROM ((SELECT 'Alice' AS [name], 75000 AS [salary])
UNION ALL
(SELECT 'Bob' AS [name], 65000 AS [salary])
UNION ALL
(SELECT 'Carol' AS [name], 80000 AS [salary])
UNION ALL
(SELECT 'David' AS [name], 55000 AS [salary])) AS [employee0]
WHERE (employee0.[salary]) > (70000)
```

**Result:** Returns Alice and Carol (salaries > 70000)

---

## 4. Rule with String Filter

A rule that filters data based on a string equality condition.

### Logica

```logica
@Engine("mssql");
Employee(name: "Alice", department: "Engineering");
Employee(name: "Bob", department: "Marketing");
Employee(name: "Carol", department: "Engineering");
Employee(name: "David", department: "Sales");

Engineer(name:) :- Employee(name:, department: "Engineering");
```

### Generated T-SQL

```sql
SELECT employee0.[name] AS [name]
FROM ((SELECT 'Alice' AS [name], 'Engineering' AS [department])
UNION ALL
(SELECT 'Bob' AS [name], 'Marketing' AS [department])
UNION ALL
(SELECT 'Carol' AS [name], 'Engineering' AS [department])
UNION ALL
(SELECT 'David' AS [name], 'Sales' AS [department])) AS [employee0]
WHERE employee0.[department] = 'Engineering'
```

**Result:** Returns Alice and Carol (department = "Engineering")

---

## 5. Join Rule (Grandparent)

A rule that joins two instances of the same predicate to find grandparent relationships.

### Logica

```logica
@Engine("mssql");
Parent(parent: "Alice", child: "Bob");
Parent(parent: "Alice", child: "Carol");
Parent(parent: "Bob", child: "David");
Parent(parent: "Carol", child: "Eve");

Grandparent(grandparent: gp, grandchild: gc) :-
    Parent(parent: gp, child: p),
    Parent(parent: p, child: gc);
```

### Generated T-SQL

```sql
SELECT parent0.[parent] AS [grandparent], parent1.[child] AS [grandchild]
FROM ((SELECT 'Alice' AS [parent], 'Bob' AS [child])
UNION ALL
(SELECT 'Alice' AS [parent], 'Carol' AS [child])
UNION ALL
(SELECT 'Bob' AS [parent], 'David' AS [child])
UNION ALL
(SELECT 'Carol' AS [parent], 'Eve' AS [child])) AS [parent0],
((SELECT 'Alice' AS [parent], 'Bob' AS [child])
UNION ALL
(SELECT 'Alice' AS [parent], 'Carol' AS [child])
UNION ALL
(SELECT 'Bob' AS [parent], 'David' AS [child])
UNION ALL
(SELECT 'Carol' AS [parent], 'Eve' AS [child])) AS [parent1]
WHERE parent1.[parent] = parent0.[child]
```

**Result:** Returns (Alice, David) and (Alice, Eve)

---

## 6. Self-Join (Siblings)

A self-join rule that finds siblings (different children with the same parent).

### Logica

```logica
@Engine("mssql");
Parent(parent: "Alice", child: "Bob");
Parent(parent: "Alice", child: "Carol");
Parent(parent: "Alice", child: "David");
Parent(parent: "Eve", child: "Frank");

Sibling(person1: p1, person2: p2) :-
    Parent(parent: parent, child: p1),
    Parent(parent: parent, child: p2),
    p1 != p2;
```

### Generated T-SQL

```sql
SELECT parent0.[child] AS [person1], parent1.[child] AS [person2]
FROM ((SELECT 'Alice' AS [parent], 'Bob' AS [child])
UNION ALL
(SELECT 'Alice' AS [parent], 'Carol' AS [child])
UNION ALL
(SELECT 'Alice' AS [parent], 'David' AS [child])
UNION ALL
(SELECT 'Eve' AS [parent], 'Frank' AS [child])) AS [parent0],
((SELECT 'Alice' AS [parent], 'Bob' AS [child])
UNION ALL
(SELECT 'Alice' AS [parent], 'Carol' AS [child])
UNION ALL
(SELECT 'Alice' AS [parent], 'David' AS [child])
UNION ALL
(SELECT 'Eve' AS [parent], 'Frank' AS [child])) AS [parent1]
WHERE parent1.[parent] = parent0.[parent] AND (parent0.[child]) <> (parent1.[child])
```

**Result:** Returns 6 sibling pairs (Bob-Carol, Bob-David, Carol-Bob, Carol-David, David-Bob, David-Carol)

---

## 7. Recursive CTE (Ancestors)

A recursive rule that uses Common Table Expressions to find all ancestors.

### Logica

```logica
@Engine("mssql");
Parent("Alice", "Bob");
Parent("Bob", "Carol");
Parent("Carol", "David");

Ancestor(a, d) :- Parent(a, d);
Ancestor(a, d) :- Parent(a, c), Ancestor(c, d);
```

### Generated T-SQL

```sql
WITH [Ancestor] AS (
    SELECT parent0.[col0] AS [col0], parent0.[col1] AS [col1]
    FROM ((SELECT 'Alice' AS [col0], 'Bob' AS [col1])
    UNION ALL
    (SELECT 'Bob' AS [col0], 'Carol' AS [col1])
    UNION ALL
    (SELECT 'Carol' AS [col0], 'David' AS [col1])) AS [parent0]
    UNION ALL
    SELECT parent1.[col0] AS [col0], ancestor2.[col1] AS [col1]
    FROM ((SELECT 'Alice' AS [col0], 'Bob' AS [col1])
    UNION ALL
    (SELECT 'Bob' AS [col0], 'Carol' AS [col1])
    UNION ALL
    (SELECT 'Carol' AS [col0], 'David' AS [col1])) AS [parent1],
    (SELECT * FROM [Ancestor]) AS [ancestor2]
    WHERE ancestor2.[col0] = parent1.[col1]
)
SELECT [col0], [col1] FROM [Ancestor]
```

**Result:** Returns 6 ancestor pairs (3 direct + 3 indirect)

---

## 8. Recursive CTE (Royal Lineage)

A more complex recursive example with named fields, inspired by Queen Victoria's descendants.

### Logica

```logica
@Engine("mssql");
Parent("Queen Victoria", "King Edward VII");
Parent("King Edward VII", "King George V");
Parent("King George V", "King George VI");
Parent("King George VI", "Queen Elizabeth II");
Parent("Queen Elizabeth II", "Prince Charles");

Ancestor(ancestor:a, descendant:d) :- Parent(a, d);
Ancestor(ancestor:a, descendant:d) :- Parent(a, c), Ancestor(c, d);
```

### Generated T-SQL

```sql
WITH [Ancestor] AS (
    SELECT parent0.[col0] AS [ancestor], parent0.[col1] AS [descendant]
    FROM ((SELECT 'Queen Victoria' AS [col0], 'King Edward VII' AS [col1])
    UNION ALL
    (SELECT 'King Edward VII' AS [col0], 'King George V' AS [col1])
    UNION ALL
    (SELECT 'King George V' AS [col0], 'King George VI' AS [col1])
    UNION ALL
    (SELECT 'King George VI' AS [col0], 'Queen Elizabeth II' AS [col1])
    UNION ALL
    (SELECT 'Queen Elizabeth II' AS [col0], 'Prince Charles' AS [col1])) AS [parent0]
    UNION ALL
    SELECT parent1.[col0] AS [ancestor], ancestor2.[descendant] AS [descendant]
    FROM ((SELECT 'Queen Victoria' AS [col0], 'King Edward VII' AS [col1])
    UNION ALL
    (SELECT 'King Edward VII' AS [col0], 'King George V' AS [col1])
    UNION ALL
    (SELECT 'King George V' AS [col0], 'King George VI' AS [col1])
    UNION ALL
    (SELECT 'King George VI' AS [col0], 'Queen Elizabeth II' AS [col1])
    UNION ALL
    (SELECT 'Queen Elizabeth II' AS [col0], 'Prince Charles' AS [col1])) AS [parent1],
    (SELECT * FROM [Ancestor]) AS [ancestor2]
    WHERE ancestor2.[ancestor] = parent1.[col1]
)
SELECT [ancestor], [descendant] FROM [Ancestor]
```

**Result:** Returns 15 ancestor-descendant pairs (including Queen Victoria -> Prince Charles)

---

## 9. Count Aggregation

Counting occurrences using the `? +=` aggregation syntax.

### Logica

```logica
@Engine("mssql");
Sale(product: "Widget", amount: 100);
Sale(product: "Widget", amount: 150);
Sale(product: "Gadget", amount: 200);
Sale(product: "Widget", amount: 120);

ProductSaleCount(product:, count? += 1) :- Sale(product:, amount:);
```

### Generated T-SQL

```sql
SELECT sale0.[product] AS [product], SUM(1) AS [count]
FROM ((SELECT 'Widget' AS [product], 100 AS [amount])
UNION ALL
(SELECT 'Widget' AS [product], 150 AS [amount])
UNION ALL
(SELECT 'Gadget' AS [product], 200 AS [amount])
UNION ALL
(SELECT 'Widget' AS [product], 120 AS [amount])) AS [sale0]
GROUP BY sale0.[product]
```

**Result:** Widget=3, Gadget=1

---

## 10. Sum Aggregation

Summing values using the `? +=` aggregation syntax.

### Logica

```logica
@Engine("mssql");
Sale(product: "Widget", amount: 100);
Sale(product: "Widget", amount: 150);
Sale(product: "Gadget", amount: 200);
Sale(product: "Widget", amount: 120);

ProductTotal(product:, total? += amount) :- Sale(product:, amount:);
```

### Generated T-SQL

```sql
SELECT sale0.[product] AS [product], SUM(sale0.[amount]) AS [total]
FROM ((SELECT 'Widget' AS [product], 100 AS [amount])
UNION ALL
(SELECT 'Widget' AS [product], 150 AS [amount])
UNION ALL
(SELECT 'Gadget' AS [product], 200 AS [amount])
UNION ALL
(SELECT 'Widget' AS [product], 120 AS [amount])) AS [sale0]
GROUP BY sale0.[product]
```

**Result:** Widget=370 (100+150+120), Gadget=200

---

## 11. Arithmetic in Rules

Using arithmetic expressions in rule heads.

### Logica

```logica
@Engine("mssql");
Rectangle(name: "A", width: 10, height: 5);
Rectangle(name: "B", width: 8, height: 6);
Rectangle(name: "C", width: 4, height: 3);

RectangleArea(name:, area: width * height) :- Rectangle(name:, width:, height:);
```

### Generated T-SQL

```sql
SELECT rectangle0.[name] AS [name], (rectangle0.[width]) * (rectangle0.[height]) AS [area]
FROM ((SELECT 'A' AS [name], 10 AS [width], 5 AS [height])
UNION ALL
(SELECT 'B' AS [name], 8 AS [width], 6 AS [height])
UNION ALL
(SELECT 'C' AS [name], 4 AS [width], 3 AS [height])) AS [rectangle0]
```

**Result:** A=50, B=48, C=12

---

## 12. Multiple Conditions

Rules with multiple filter conditions combined with AND.

### Logica

```logica
@Engine("mssql");
Employee(name: "Alice", age: 35, salary: 80000);
Employee(name: "Bob", age: 28, salary: 60000);
Employee(name: "Carol", age: 45, salary: 90000);
Employee(name: "David", age: 32, salary: 75000);

SeniorHighEarner(name:) :-
    Employee(name:, age:, salary:),
    age > 30,
    salary > 70000;
```

### Generated T-SQL

```sql
SELECT employee0.[name] AS [name]
FROM ((SELECT 'Alice' AS [name], 35 AS [age], 80000 AS [salary])
UNION ALL
(SELECT 'Bob' AS [name], 28 AS [age], 60000 AS [salary])
UNION ALL
(SELECT 'Carol' AS [name], 45 AS [age], 90000 AS [salary])
UNION ALL
(SELECT 'David' AS [name], 32 AS [age], 75000 AS [salary])) AS [employee0]
WHERE (employee0.[age]) > (30) AND (employee0.[salary]) > (70000)
```

**Result:** Returns Alice, Carol, and David (age > 30 AND salary > 70000)

---

## 13. Negation

Using negation (`~`) to exclude matching rows via NOT EXISTS.

### Logica

```logica
@Engine("mssql");
Employee(name: "Alice");
Employee(name: "Bob");
Employee(name: "Carol");
Manager(name: "Alice");

NonManager(name:) :- Employee(name:), ~Manager(name:);
```

### Generated T-SQL

```sql
SELECT employee0.[name] AS [name]
FROM ((SELECT 'Alice' AS [name])
UNION ALL
(SELECT 'Bob' AS [name])
UNION ALL
(SELECT 'Carol' AS [name])) AS [employee0]
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 'Alice' AS [name]) AS neg_sub
    WHERE neg_sub.[name] = employee0.[name])
```

**Result:** Returns Bob and Carol (employees who are not managers)

---

## 14. Multiple Rules (UNION)

Multiple rules for the same predicate are combined with UNION ALL.

### Logica

```logica
@Engine("mssql");
Dog(name: "Buddy");
Dog(name: "Max");
Cat(name: "Whiskers");
Cat(name: "Mittens");

Pet(name:) :- Dog(name:);
Pet(name:) :- Cat(name:);
```

### Generated T-SQL

```sql
(SELECT dog0.[name] AS [name]
FROM ((SELECT 'Buddy' AS [name])
UNION ALL
(SELECT 'Max' AS [name])) AS [dog0])
UNION ALL
(SELECT cat0.[name] AS [name]
FROM ((SELECT 'Whiskers' AS [name])
UNION ALL
(SELECT 'Mittens' AS [name])) AS [cat0])
```

**Result:** Returns all 4 pets (Buddy, Max, Whiskers, Mittens)

---

## Summary

| Feature | Logica Syntax | T-SQL Translation |
|---------|---------------|-------------------|
| Facts | `Pred(val1, val2)` | `SELECT val1, val2` with UNION ALL |
| Named Fields | `Pred(field: val)` | `SELECT val AS [field]` |
| Rules | `Head :- Body` | `SELECT ... FROM ... WHERE ...` |
| Joins | `A(x), B(x)` | Cartesian product with WHERE equality |
| Recursion | Multiple rules referencing self | `WITH RECURSIVE` CTE |
| Aggregation | `field? += expr` | `SUM(expr) ... GROUP BY` |
| Negation | `~Pred(x)` | `NOT EXISTS (SELECT ...)` |
| Multiple Rules | Same predicate, multiple rules | `UNION ALL` |
