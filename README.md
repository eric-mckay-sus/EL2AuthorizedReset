EL2 Authorized Reset Solution
=========

Overview
---------

This repository contains the EL2 Authorized Reset Solution and related projects used for parsing Maximo exports to CMMS-line name mappings, providing admin utilities such as adding/removing associate and their respective lines, and authorizing and logging resets by badge swipe on machines.

Top-level projects
---------

- EL2Authorized Reset — The program to be run on individual machines to verify that the associate has permission to reset the machine. See [EL2AuthorizedReset/README.md](EL2AuthorizedReset/README.md).
- CmmsCsvReader — A CSV parsing utility to get a new mapping of CMMS numbers to line names. Can run standalone via console, or can be accessed via Admin Interface in the Blazor app. See [CmmsCsvReader/README.md](CmmsCsvReader/README.md).
- AdminInterface — A Blazor-based UI providing CRUD operations (mostly CRD) for the AssociateInfo and AssociateToLine databases. See [AdminInterface/README.md](AdminInterface/README.md).

Quick start
---------

Build the solution:

```bash
dotnet build
```

Run the web UI (AdminInterface):

```bash
dotnet run --project HiokiNL2SQLMark1\AdminInterface.csproj
```
