@Engine("mssql");

Parent("Alice", "Bob");
Parent("Alice", "Carol");
Parent("Bob", "David");
Parent("Carol", "Eve");

Ancestor(a, d) :- Parent(a, d);
Ancestor(a, d) :- Parent(a, c), Ancestor(c, d);

CommonAncestor(ancestor: a, descendant1: d1, descendant2: d2) :-
    Ancestor(a, d1),
    Ancestor(a, d2),
    d1 != d2;
