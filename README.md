# Phoesion.EFCore.PrecomputedViews

Phoesion.EFCore.PrecomputedViews is an Entity Framework Core library that enables the automatic computation and updating of dependent views based on changes in the database.
This approach is particularly useful for scenarios where derived or computed properties need to stay in sync with related entities without manual intervention.

 Phoesion.EFCore.PrecomputedViews was originally developed as an internal component of [Phoesion Glow](https://glow.phoesion.com), a comprehensive microservice development solution.
 Since the codebase proved to be sufficiently independent and reusable as a standalone library, I decided to open-source it.

[![NuGet](https://img.shields.io/nuget/v/Phoesion.EFCore.PrecomputedViews.svg)](https://www.nuget.org/packages/Phoesion.EFCore.PrecomputedViews)


## Features

- **Automatic View Updates:** Automatically recompute and update dependent properties or views when related entities are modified.
- **Seamless Integration:** Built on EFCore's SaveChanges pipeline, requiring minimal configuration.
- **Optimized Performance:** Only updates affected views/rows, reducing unnecessary computations.
- **Support for Dependency Events:** Trigger recomputations based on entity events like addition, modification, or deletion.
- **Custom Handlers:** Easily define handlers to compute views using your business logic.
- **Provider-Agnostic**: Seamlessly integrates with EFCore and supports any database provider, including MySQL, SQLite, Postgres, SQL Server, and more.

---

## Installation

Add the library to your project:

```bash
dotnet add package Phoesion.EFCore.PrecomputedViews
```

---

## Enabling Precomputed Views in your DbContext

Use the `AddPrecomputedViews` extension method to enable the interceptor,

either from your `AddDbContext<>` in the application host builder:
```csharp
builder.Services.AddDbContext<dbContext>(optionsBuilder =>
{
    //setup context..
    //optionsBuilder.UseSqlite("Data Source=mydb.db");

    //add pre-computed views interceptor
    optionsBuilder.AddPrecomputedViews();
});
```

or in `OnConfiguring` in you dbContext class:
```csharp
public class mydbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        //setup context..
        //optionsBuilder.UseSqlite("Data Source=mydb.db");

        //Add precomputed-views interceptor
        optionsBuilder.AddPrecomputedViews();
    }
}
```

## Defining Models with Dependencies

Annotate your models with `[DependsOn]` to specify the dependencies and handlers. 
The handler can be any class that implements the `IComputableView<TDbContext, TEntity>` interface.


### Example 1: Pre-compute the `Balance` for an `Account` based on `Transations`
```csharp
//the Account model
public class Account
{
    [Key]
    public long Id { get; set; }
    public string Name { get; set; }
}       

//the Transaction model
public class Transaction
{
    [Key]
    public long Id { get; set; }

    public enum TransactionTypes
    {
        Credit = 0,
        Debit = 1,
    }
    public TransactionTypes TransactionType { get; set; }

    public decimal Amount { get; set; }

    public long AccountId { get; set; }
    public Account Account { get; set; }
}

//the AccountView model, that will store the precomputed data
[DependsOn<Transaction>(DependencyEvents.Added)]  // <-- Register dependency (whenever a Transaction is Added)
public class AccountView : IComputableView<dbContext, Transaction>
{
    [Key]
    public long Id { get; set; }
    public decimal Balance { get; set; }
    public Account Account { get; set; }

    //compute view when a new transaction is added
    public async ValueTask ComputeView(dbContext context, DependencyEvents ev, IEnumerable<Transaction> changedDependencies)
    {
        //find accounts that are affected
        var accountIdsAffected = changedDependencies.Select(x => x.AccountId);

        //execute update 
        await context.AccountViews
                        .Where(s => accountIdsAffected.Contains(s.Id))     // limit search space to affected accounts only (otherwise the query will run on all account entities)
                        .ExecuteUpdateAsync(e => e.SetProperty(
                            x => x.Balance,
                            x => context.Transactions
                                            .Where(transaction => transaction.AccountId == x.Id)
                                            .Select(transaction => transaction.TransactionType == Transaction.TransactionTypes.Credit ? transaction.Amount : -transaction.Amount)
                                            .Sum()));
    }
}
```


### Example 2: Pre-compute a complex query whenever an Enrollment is added/removed (keep latest id)

```csharp
//An example Course model with dependency on Enrollment add/remove event, ot keep the latest added enrollment id
[DependsOn<Enrollment>(DependencyEvents.Added | DependencyEvents.Removed, typeof(EnrollmentAddedRemovedHandler))]
public class Course
{
    [Key]
    public int Id { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; }

    //This will point to the latest Enrollment added, without needing to update it manually, using the CourseViewEnrollmentAddedHandler.ComputeView() method.
    public int? LatestEnrollmentIdAddedId { get; set; }
    public Enrollment? LatestEnrollmentIdAdded { get; set; }

    //=======================
    // View handler
    //=======================
    class EnrollmentAddedRemovedHandler : IComputableView<dbContext, Enrollment>
    {
        public async ValueTask ComputeView(dbContext context, DependencyEvents ev, IEnumerable<Enrollment> changedDependencies)
        {
            //find courses that are affected
            var courseIds = changedDependencies.Select(e => e.CourseId);

            //execute update
            await context.Courses
                        .Where(c => courseIds.Contains(c.Id))
                        .ExecuteUpdateAsync(e => e.SetProperty(
                            c => c.LatestEnrollmentIdAddedId,
                            c => context.Enrollments
                                         .Where(e => e.CourseId == c.Id)
                                         .OrderByDescending(e => e.Id)
                                         .Select(e => (int?)e.Id)
                                         .FirstOrDefault()));
        }
    }
}
```


### Example 3: Pre-compute the `Enrollments.Count()` whenever an `Enrollment` is added

```csharp
//the Course model
public class Course
{
    [Key]
    public int Id { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; }
}

//the Enrollment  model
public class Enrollment
{
    [Key]
    public int Id { get; set; }

    public int CourseId { get; set; }
    public Course Course { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; }
}

//the Student model, with dependencies on Enrollment add/remove to pre-compute Count
[DependsOn<Enrollment>(DependencyEvents.Added | DependencyEvents.Removed, typeof(EnrollmentAddedRemovedHandler))]
public class Student
{
    [Key]
    public int Id { get; set; }
    public string LastName { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; }

    //have a pre-computed count of enrollments (this can be a read-heavy and compute-heavy count that we want to have pre-computed)
    public int EnrollmentCount { get; set; }

    //the handler that will update EnrollmentCount whenever an Enrollment is added/deleted
    class EnrollmentAddedRemovedHandler : IComputableView<dbContext, Enrollment>
    {
        public async ValueTask ComputeView(dbContext context, DependencyEvents ev, IEnumerable<Enrollment> changedDependencies)
        {
            //find students that are affected
            var studentIds = changedDependencies.Select(e => e.StudentId);

            //execute update 
            await context.Students
                        .Where(s => studentIds.Contains(s.Id))
                        .ExecuteUpdateAsync(e => e.SetProperty(
                            s => s.EnrollmentCount,
                            s => context.Enrollments.Count(e => e.StudentId == s.Id)));
        }
    }
}
```

---

## View Handlers (`IComputableView`)

Phoesion.EFCore.PrecomputedViews supports DI but it can also be used without it. 

### Lifetime
Every time the system detects that a handler needs to run, it will instanciate it and cache the instance for the lifetime of the interceptor.

### Activation
The activation will attempt the following steps, in this order, until a non-null instance is obtained  :
 - If an `IServiceProvider` is available it will try to request a handler instance from the ServiceProvider using `serviceProvider.GetService(handlerType)`
 - If an `IServiceProvider` is available it will use `ActivatorUtilities.CreateInstance(serviceProvider, handlerType)`
 - Create a new instance using `Activator.CreateInstance(handlerType, true)`

---

## Caveats / Warnings / Limitations

This library is an **EFCore interceptor**, thus it operates on models/entities **tracked by EFCore's Change Tracker**. 

The handler will not run in the following cases :
  - Data are deleted using `ExecuteDeleteAsync()` and `ExecuteDelete()`
  - Data are modified using `ExecuteUpdateAsync()` and `ExecuteUpdate()`
  - Data are added/deleted/modified in the database manually. _(or by processes not running EFCore with Phoesion.EFCore.PrecomputedViews activated)_

---

## Example Projects

The repository includes sample console applications demonstrating how to use Phoesion.EFCore.PrecomputedViews.

---

## Contributing

Contributions are welcome! Feel free to open issues, suggest improvements, or submit pull requests.

---

## License

This project is licensed under the MIT License.

