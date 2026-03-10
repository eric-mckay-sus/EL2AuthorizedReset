CmmsCsvReader
=========

Purpose
---------

`CmmsCsvReader` contains utilities and a small console entrypoint for parsing Maximo exports. It's intended as a lightweight tool to update the CMMS-line name mappings.

How it works
------------

- The main application entry is in `UploadCsvToDb.cs` and takes a filename as a command line argument.
- Parsing utilities can be accessed either by running the project as described below or via the Blazor app `AdminInterface`.

Build & run
-----------

Build the project:

```bash
dotnet build CmmsCsvReader\CmmsCsvReader.csproj
```

Run from the project folder:

```bash
dotnet run --project CmmsCsvReader\CmmsCsvReader.csproj
```
