using Microsoft.Data.SqlClient;
using System.Data;

namespace LogicaSharp.Tests;

/// <summary>
/// Integration tests that compile Logica to T-SQL and execute against LocalDB.
/// These tests require SQL Server LocalDB to be installed.
/// </summary>
public class LocalDbIntegrationTests : IDisposable
{
    private readonly string _connectionString;
    private readonly SqlConnection _connection;
    private readonly string _databaseName;

    public LocalDbIntegrationTests()
    {
        _databaseName = $"LogicaTest_{Guid.NewGuid():N}";
        _connectionString = $@"Server=(localdb)\MSSQLLocalDB;Database={_databaseName};Integrated Security=true;TrustServerCertificate=true;";

        // Create the test database
        using var masterConnection = new SqlConnection(@"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;");
        masterConnection.Open();
        using var createCmd = masterConnection.CreateCommand();
        createCmd.CommandText = $"CREATE DATABASE [{_databaseName}]";
        createCmd.ExecuteNonQuery();

        _connection = new SqlConnection(_connectionString);
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();

        // Drop the test database
        using var masterConnection = new SqlConnection(@"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;");
        masterConnection.Open();
        using var dropCmd = masterConnection.CreateCommand();
        dropCmd.CommandText = $@"
            ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [{_databaseName}];";
        dropCmd.ExecuteNonQuery();
    }

    private DataTable ExecuteSql(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        using var adapter = new SqlDataAdapter(cmd);
        var result = new DataTable();
        adapter.Fill(result);
        return result;
    }

    private DataTable CompileAndExecute(string logicaSource, string predicateName)
    {
        var sql = Logica.Compile(logicaSource, predicateName, "mssql");
        return ExecuteSql(sql);
    }

    #region Simple Facts Tests

    [Fact]
    public void SimpleFact_ReturnsCorrectData()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Person(""Alice"", 30);
Person(""Bob"", 25);
Person(""Carol"", 35);
";

        // Act
        var result = CompileAndExecute(source, "Person");

        // Assert
        Assert.Equal(3, result.Rows.Count);
    }

    [Fact]
    public void NamedFieldFacts_ReturnsCorrectData()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Employee(name: ""Alice"", department: ""Engineering"", salary: 75000);
Employee(name: ""Bob"", department: ""Marketing"", salary: 65000);
Employee(name: ""Carol"", department: ""Engineering"", salary: 80000);
";

        // Act
        var result = CompileAndExecute(source, "Employee");

        // Assert
        Assert.Equal(3, result.Rows.Count);
        Assert.Contains(result.Columns.Cast<DataColumn>(), c => c.ColumnName == "name");
        Assert.Contains(result.Columns.Cast<DataColumn>(), c => c.ColumnName == "department");
        Assert.Contains(result.Columns.Cast<DataColumn>(), c => c.ColumnName == "salary");
    }

    #endregion

    #region Simple Rules Tests

    [Fact]
    public void SimpleRule_FiltersData()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Employee(name: ""Alice"", salary: 75000);
Employee(name: ""Bob"", salary: 65000);
Employee(name: ""Carol"", salary: 80000);
Employee(name: ""David"", salary: 55000);

HighEarner(name:) :- Employee(name:, salary:), salary > 70000;
";

        // Act
        var result = CompileAndExecute(source, "HighEarner");

        // Assert
        Assert.Equal(2, result.Rows.Count);
        var names = result.Rows.Cast<DataRow>().Select(r => r["name"].ToString()).ToList();
        Assert.Contains("Alice", names);
        Assert.Contains("Carol", names);
    }

    [Fact]
    public void RuleWithStringFilter_FiltersCorrectly()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Employee(name: ""Alice"", department: ""Engineering"");
Employee(name: ""Bob"", department: ""Marketing"");
Employee(name: ""Carol"", department: ""Engineering"");
Employee(name: ""David"", department: ""Sales"");

Engineer(name:) :- Employee(name:, department: ""Engineering"");
";

        // Act
        var result = CompileAndExecute(source, "Engineer");

        // Assert
        Assert.Equal(2, result.Rows.Count);
        var names = result.Rows.Cast<DataRow>().Select(r => r["name"].ToString()).ToList();
        Assert.Contains("Alice", names);
        Assert.Contains("Carol", names);
    }

    #endregion

    #region Join Tests

    [Fact]
    public void JoinRule_CombinesData()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Parent(parent: ""Alice"", child: ""Bob"");
Parent(parent: ""Alice"", child: ""Carol"");
Parent(parent: ""Bob"", child: ""David"");
Parent(parent: ""Carol"", child: ""Eve"");

Grandparent(grandparent: gp, grandchild: gc) :-
    Parent(parent: gp, child: p),
    Parent(parent: p, child: gc);
";

        // Act
        var result = CompileAndExecute(source, "Grandparent");

        // Assert
        Assert.Equal(2, result.Rows.Count);
        var pairs = result.Rows.Cast<DataRow>()
            .Select(r => (r["grandparent"].ToString(), r["grandchild"].ToString()))
            .ToList();
        Assert.Contains(("Alice", "David"), pairs);
        Assert.Contains(("Alice", "Eve"), pairs);
    }

    [Fact]
    public void SelfJoin_FindsSiblings()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Parent(parent: ""Alice"", child: ""Bob"");
Parent(parent: ""Alice"", child: ""Carol"");
Parent(parent: ""Alice"", child: ""David"");
Parent(parent: ""Eve"", child: ""Frank"");

Sibling(person1: p1, person2: p2) :-
    Parent(parent: parent, child: p1),
    Parent(parent: parent, child: p2),
    p1 != p2;
";

        // Act
        var result = CompileAndExecute(source, "Sibling");

        // Assert
        // Alice's 3 children form 6 sibling pairs (3*2 = 6 ordered pairs)
        Assert.Equal(6, result.Rows.Count);
    }

    #endregion

    #region Recursive CTE Tests

    [Fact]
    public void RecursiveAncestor_FindsAllAncestors()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Parent(""Alice"", ""Bob"");
Parent(""Bob"", ""Carol"");
Parent(""Carol"", ""David"");

Ancestor(a, d) :- Parent(a, d);
Ancestor(a, d) :- Parent(a, c), Ancestor(c, d);
";

        // Act
        var result = CompileAndExecute(source, "Ancestor");

        // Assert
        // Direct: Alice->Bob, Bob->Carol, Carol->David (3)
        // Indirect: Alice->Carol, Alice->David, Bob->David (3)
        // Total: 6
        Assert.Equal(6, result.Rows.Count);
    }

    [Fact]
    public void RecursiveAncestor_QueenVictoriaStyle()
    {
        // Arrange - simplified version of queen_victoria.l
        var source = @"
@Engine(""mssql"");
Parent(""Queen Victoria"", ""King Edward VII"");
Parent(""King Edward VII"", ""King George V"");
Parent(""King George V"", ""King George VI"");
Parent(""King George VI"", ""Queen Elizabeth II"");
Parent(""Queen Elizabeth II"", ""Prince Charles"");

Ancestor(ancestor:a, descendant:d) :- Parent(a, d);
Ancestor(ancestor:a, descendant:d) :- Parent(a, c), Ancestor(c, d);
";

        // Act
        var result = CompileAndExecute(source, "Ancestor");

        // Assert
        // Should find all ancestor-descendant pairs
        // Direct pairs: 5 (each parent link)
        // 2-step: 4 (Victoria->George V, Edward->George VI, George V->Elizabeth, George VI->Charles)
        // 3-step: 3 (Victoria->George VI, Edward->Elizabeth, George V->Charles)
        // 4-step: 2 (Victoria->Elizabeth, Edward->Charles)
        // 5-step: 1 (Victoria->Charles)
        // Total: 5+4+3+2+1 = 15
        Assert.Equal(15, result.Rows.Count);

        // Verify Queen Victoria is ancestor of Prince Charles
        var queenToCharles = result.Rows.Cast<DataRow>()
            .Any(r => r["ancestor"].ToString() == "Queen Victoria" &&
                      r["descendant"].ToString() == "Prince Charles");
        Assert.True(queenToCharles);
    }

    #endregion

    #region Aggregation Tests

    [Fact]
    public void CountAggregation_CountsCorrectly()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Sale(product: ""Widget"", amount: 100);
Sale(product: ""Widget"", amount: 150);
Sale(product: ""Gadget"", amount: 200);
Sale(product: ""Widget"", amount: 120);

ProductSaleCount(product:, count? += 1) :- Sale(product:, amount:);
";

        // Act
        var result = CompileAndExecute(source, "ProductSaleCount");

        // Assert
        Assert.Equal(2, result.Rows.Count); // Widget and Gadget

        var widgetRow = result.Rows.Cast<DataRow>().FirstOrDefault(r => r["product"].ToString() == "Widget");
        var gadgetRow = result.Rows.Cast<DataRow>().FirstOrDefault(r => r["product"].ToString() == "Gadget");

        Assert.NotNull(widgetRow);
        Assert.NotNull(gadgetRow);
        Assert.Equal(3, Convert.ToInt32(widgetRow["count"]));
        Assert.Equal(1, Convert.ToInt32(gadgetRow["count"]));
    }

    [Fact]
    public void SumAggregation_SumsCorrectly()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Sale(product: ""Widget"", amount: 100);
Sale(product: ""Widget"", amount: 150);
Sale(product: ""Gadget"", amount: 200);
Sale(product: ""Widget"", amount: 120);

ProductTotal(product:, total? += amount) :- Sale(product:, amount:);
";

        // Act
        var result = CompileAndExecute(source, "ProductTotal");

        // Assert
        Assert.Equal(2, result.Rows.Count);

        var widgetRow = result.Rows.Cast<DataRow>().FirstOrDefault(r => r["product"].ToString() == "Widget");
        var gadgetRow = result.Rows.Cast<DataRow>().FirstOrDefault(r => r["product"].ToString() == "Gadget");

        Assert.NotNull(widgetRow);
        Assert.NotNull(gadgetRow);
        Assert.Equal(370, Convert.ToInt32(widgetRow["total"])); // 100 + 150 + 120
        Assert.Equal(200, Convert.ToInt32(gadgetRow["total"]));
    }

    #endregion

    #region Arithmetic and Comparison Tests

    [Fact]
    public void ArithmeticInRule_CalculatesCorrectly()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Rectangle(name: ""A"", width: 10, height: 5);
Rectangle(name: ""B"", width: 8, height: 6);
Rectangle(name: ""C"", width: 4, height: 3);

RectangleArea(name:, area: width * height) :- Rectangle(name:, width:, height:);
";

        // Act
        var result = CompileAndExecute(source, "RectangleArea");

        // Assert
        Assert.Equal(3, result.Rows.Count);

        var areaA = result.Rows.Cast<DataRow>().First(r => r["name"].ToString() == "A")["area"];
        var areaB = result.Rows.Cast<DataRow>().First(r => r["name"].ToString() == "B")["area"];
        var areaC = result.Rows.Cast<DataRow>().First(r => r["name"].ToString() == "C")["area"];

        Assert.Equal(50, Convert.ToInt32(areaA));
        Assert.Equal(48, Convert.ToInt32(areaB));
        Assert.Equal(12, Convert.ToInt32(areaC));
    }

    [Fact]
    public void MultipleConditions_FilterCorrectly()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Employee(name: ""Alice"", age: 35, salary: 80000);
Employee(name: ""Bob"", age: 28, salary: 60000);
Employee(name: ""Carol"", age: 45, salary: 90000);
Employee(name: ""David"", age: 32, salary: 75000);

SeniorHighEarner(name:) :-
    Employee(name:, age:, salary:),
    age > 30,
    salary > 70000;
";

        // Act
        var result = CompileAndExecute(source, "SeniorHighEarner");

        // Assert
        Assert.Equal(3, result.Rows.Count); // Alice (35, 80k), Carol (45, 90k), David (32, 75k)
        var names = result.Rows.Cast<DataRow>().Select(r => r["name"].ToString()).ToList();
        Assert.Contains("Alice", names);
        Assert.Contains("Carol", names);
        Assert.Contains("David", names);
        Assert.DoesNotContain("Bob", names);
    }

    #endregion

    #region Negation Tests

    [Fact]
    public void Negation_ExcludesMatchingRows()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Employee(name: ""Alice"");
Employee(name: ""Bob"");
Employee(name: ""Carol"");
Manager(name: ""Alice"");

NonManager(name:) :- Employee(name:), ~Manager(name:);
";

        // Act
        var result = CompileAndExecute(source, "NonManager");

        // Assert
        Assert.Equal(2, result.Rows.Count);
        var names = result.Rows.Cast<DataRow>().Select(r => r["name"].ToString()).ToList();
        Assert.Contains("Bob", names);
        Assert.Contains("Carol", names);
        Assert.DoesNotContain("Alice", names);
    }

    #endregion

    #region Multiple Rules for Same Predicate (UNION) Tests

    [Fact]
    public void MultipleRules_UnionResults()
    {
        // Arrange
        var source = @"
@Engine(""mssql"");
Dog(name: ""Buddy"");
Dog(name: ""Max"");
Cat(name: ""Whiskers"");
Cat(name: ""Mittens"");

Pet(name:) :- Dog(name:);
Pet(name:) :- Cat(name:);
";

        // Act
        var result = CompileAndExecute(source, "Pet");

        // Assert
        Assert.Equal(4, result.Rows.Count);
        var names = result.Rows.Cast<DataRow>().Select(r => r["name"].ToString()).ToList();
        Assert.Contains("Buddy", names);
        Assert.Contains("Max", names);
        Assert.Contains("Whiskers", names);
        Assert.Contains("Mittens", names);
    }

    #endregion

    #region Higher-Order Predicate (Functor) Tests

    [Fact]
    public void Functor_FilterByProductOs_AppliedToDailyActiveUsers()
    {
        // Arrange - Define a functor template that filters by product and os,
        // then instantiate it with DailyActiveUsers table
        var source = @"
@Engine(""mssql"");

# Mock data for DailyActiveUsers
DailyActiveUsers(user: ""alice"", product: ""App"", os: ""Windows"", sessions: 5);
DailyActiveUsers(user: ""bob"", product: ""App"", os: ""Mac"", sessions: 3);
DailyActiveUsers(user: ""carol"", product: ""Web"", os: ""Windows"", sessions: 8);
DailyActiveUsers(user: ""dave"", product: ""App"", os: ""Windows"", sessions: 2);
DailyActiveUsers(user: ""eve"", product: ""Web"", os: ""Linux"", sessions: 4);

# Define the functor template - filters source by product and os
@Functor(""FilterByProductOs"");
FilterByProductOs(user:, sessions:) :-
    source(user:, product:, os:, sessions:),
    product == filterProduct,
    os == filterOs;

# Instantiate the functor with DailyActiveUsers, filtering for App on Windows
FilteredDAU := FilterByProductOs(source: DailyActiveUsers, filterProduct: ""App"", filterOs: ""Windows"");
";

        // Act
        var result = CompileAndExecute(source, "FilteredDAU");

        // Assert - Should return alice (5 sessions) and dave (2 sessions)
        Assert.Equal(2, result.Rows.Count);
        var users = result.Rows.Cast<DataRow>().Select(r => r["user"].ToString()).ToList();
        Assert.Contains("alice", users);
        Assert.Contains("dave", users);
    }

    [Fact]
    public void Functor_FilterByProductOs_AppliedToWeeklyActiveUsers()
    {
        // Arrange - Define a functor template and apply it to WeeklyActiveUsers
        var source = @"
@Engine(""mssql"");

# Mock data for WeeklyActiveUsers
WeeklyActiveUsers(user: ""alice"", product: ""App"", os: ""Windows"", sessions: 25);
WeeklyActiveUsers(user: ""bob"", product: ""App"", os: ""Mac"", sessions: 18);
WeeklyActiveUsers(user: ""carol"", product: ""Web"", os: ""Windows"", sessions: 42);
WeeklyActiveUsers(user: ""dave"", product: ""App"", os: ""Windows"", sessions: 15);
WeeklyActiveUsers(user: ""eve"", product: ""Web"", os: ""Mac"", sessions: 30);
WeeklyActiveUsers(user: ""frank"", product: ""Web"", os: ""Mac"", sessions: 22);

# Define the functor template
@Functor(""FilterByProductOs"");
FilterByProductOs(user:, sessions:) :-
    source(user:, product:, os:, sessions:),
    product == filterProduct,
    os == filterOs;

# Instantiate the functor with WeeklyActiveUsers, filtering for Web on Mac
FilteredWAU := FilterByProductOs(source: WeeklyActiveUsers, filterProduct: ""Web"", filterOs: ""Mac"");
";

        // Act
        var result = CompileAndExecute(source, "FilteredWAU");

        // Assert - Should return eve (30 sessions) and frank (22 sessions)
        Assert.Equal(2, result.Rows.Count);
        var users = result.Rows.Cast<DataRow>().Select(r => r["user"].ToString()).ToList();
        Assert.Contains("eve", users);
        Assert.Contains("frank", users);
    }

    [Fact]
    public void Functor_FilterByProductOs_AppliedToBothTables()
    {
        // Arrange - Use the same functor template with two different tables
        var source = @"
@Engine(""mssql"");

# Mock data for DailyActiveUsers
DailyActiveUsers(user: ""alice"", product: ""App"", os: ""Windows"", sessions: 5);
DailyActiveUsers(user: ""bob"", product: ""App"", os: ""Mac"", sessions: 3);
DailyActiveUsers(user: ""carol"", product: ""Web"", os: ""Windows"", sessions: 8);

# Mock data for WeeklyActiveUsers
WeeklyActiveUsers(user: ""alice"", product: ""App"", os: ""Windows"", sessions: 25);
WeeklyActiveUsers(user: ""bob"", product: ""App"", os: ""Mac"", sessions: 18);
WeeklyActiveUsers(user: ""dave"", product: ""App"", os: ""Windows"", sessions: 15);

# Define the functor template once
@Functor(""FilterByProductOs"");
FilterByProductOs(user:, sessions:) :-
    source(user:, product:, os:, sessions:),
    product == filterProduct,
    os == filterOs;

# Instantiate for DailyActiveUsers - App on Windows
FilteredDAU := FilterByProductOs(source: DailyActiveUsers, filterProduct: ""App"", filterOs: ""Windows"");

# Instantiate for WeeklyActiveUsers - App on Windows
FilteredWAU := FilterByProductOs(source: WeeklyActiveUsers, filterProduct: ""App"", filterOs: ""Windows"");

# Combine results from both filtered tables
CombinedAppWindows(user:, daily_sessions: d, weekly_sessions: w) :-
    FilteredDAU(user:, sessions: d),
    FilteredWAU(user:, sessions: w);
";

        // Act
        var result = CompileAndExecute(source, "CombinedAppWindows");

        // Assert - Should return alice (daily=5, weekly=25) as she's in both filtered results
        Assert.Equal(1, result.Rows.Count);
        var row = result.Rows[0];
        Assert.Equal("alice", row["user"].ToString());
        Assert.Equal(5, Convert.ToInt32(row["daily_sessions"]));
        Assert.Equal(25, Convert.ToInt32(row["weekly_sessions"]));
    }

    [Fact]
    public void Functor_WithAggregation_SumSessionsByProduct()
    {
        // Arrange - Functor that aggregates sessions by product
        var source = @"
@Engine(""mssql"");

# Mock data
DailyActiveUsers(user: ""alice"", product: ""App"", os: ""Windows"", sessions: 5);
DailyActiveUsers(user: ""bob"", product: ""App"", os: ""Mac"", sessions: 3);
DailyActiveUsers(user: ""carol"", product: ""Web"", os: ""Windows"", sessions: 8);
DailyActiveUsers(user: ""dave"", product: ""App"", os: ""Windows"", sessions: 2);

WeeklyActiveUsers(user: ""alice"", product: ""App"", os: ""Windows"", sessions: 25);
WeeklyActiveUsers(user: ""bob"", product: ""Web"", os: ""Mac"", sessions: 18);

# Functor that sums sessions by product from any source table
@Functor(""SumByProduct"");
SumByProduct(product:, total_sessions? += sessions) :- source(product:, sessions:);

# Apply to both tables
DailyByProduct := SumByProduct(source: DailyActiveUsers);
WeeklyByProduct := SumByProduct(source: WeeklyActiveUsers);
";

        // Act
        var dailyResult = CompileAndExecute(source, "DailyByProduct");
        var weeklyResult = CompileAndExecute(source, "WeeklyByProduct");

        // Assert Daily - App: 5+3+2=10, Web: 8
        Assert.Equal(2, dailyResult.Rows.Count);
        var dailyApp = dailyResult.Rows.Cast<DataRow>().First(r => r["product"].ToString() == "App");
        var dailyWeb = dailyResult.Rows.Cast<DataRow>().First(r => r["product"].ToString() == "Web");
        Assert.Equal(10, Convert.ToInt32(dailyApp["total_sessions"]));
        Assert.Equal(8, Convert.ToInt32(dailyWeb["total_sessions"]));

        // Assert Weekly - App: 25, Web: 18
        Assert.Equal(2, weeklyResult.Rows.Count);
        var weeklyApp = weeklyResult.Rows.Cast<DataRow>().First(r => r["product"].ToString() == "App");
        var weeklyWeb = weeklyResult.Rows.Cast<DataRow>().First(r => r["product"].ToString() == "Web");
        Assert.Equal(25, Convert.ToInt32(weeklyApp["total_sessions"]));
        Assert.Equal(18, Convert.ToInt32(weeklyWeb["total_sessions"]));
    }

    #endregion
}
