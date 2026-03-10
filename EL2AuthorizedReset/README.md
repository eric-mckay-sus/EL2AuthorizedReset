EL2AuthorizedReset
=========

Purpose
---------

`EL2AuthorizedReset` contains the program to be run on individual machines. It takes a badge number and CMMS number, then checks the database to see if there's a relationship between the two. There's not currently a way for it to communicate back to the machine whether the reset was authorized or not.

How it works
---------

- The main application entry is in AuthorizeReset.cs. Badge number and CMMS number are passed in as command line arguments.

Build & run
---------

Build the project:

```bash
dotnet build EL2AuthorizedReset/EL2AuthorizedReset.csproj
```

Run from the project folder:

```bash
dotnet run --project EL2AuthorizedReset/EL2AuthorizedReset.csproj
```
