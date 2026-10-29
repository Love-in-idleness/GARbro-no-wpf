# Portable build and tests

Install a .NET 10 SDK, then run from the repository root:

```sh
dotnet test GARbro.NoWpf.slnx -c Release
python3 tools/inventory-formats.py --check
```

The historical `GARbro.sln` still targets .NET Framework 4.7.2 on Windows.
Its workflow exports the actual MEF catalog with `tools/export-legacy-catalog.ps1`.
The versioned source inventory records exports from explicit legacy project items;
it is not a substitute for the runtime catalog or successful extraction tests.
