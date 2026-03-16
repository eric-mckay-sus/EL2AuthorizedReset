EL2Lockout
=========

Purpose
---------

`EL2Lockout` contains the program to be run on individual machines. It takes a CMMS number and reason, then documents the lockout in HistoricalLockouts.

How it works
---------

- The main application entry is in LockMachine.cs. CMMS number and reason (quoted) are passed in as command line arguments.

Build & run
---------

Build the project:

```bash
dotnet build EL2Lockout/EL2Lockout.csproj
```

Run from the project folder:

```bash
dotnet run --project EL2Lockout/EL2Lockout.csproj
```
