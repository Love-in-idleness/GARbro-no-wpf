namespace GARbro.Domain

open System
open System.IO
open System.Text

[<AbstractClass; Sealed>]
type ArchivePackingPlan private () =
    static member CommonRoot(paths: string[], directories: bool[]) =
        if isNull paths || paths.Length = 0 || isNull directories || paths.Length <> directories.Length then
            invalidArg "paths" "Select at least one regular file to create an archive."
        let paths = paths |> Array.map (fun path -> Path.TrimEndingDirectorySeparator(Path.GetFullPath path))
        let mutable root = if directories[0] then paths[0] else Path.GetDirectoryName(paths[0])
        for path in paths do
            let mutable relative = Path.GetRelativePath(root, path)
            while Path.IsPathRooted(relative) || relative = ".." || relative.StartsWith(".." + string Path.DirectorySeparatorChar, StringComparison.Ordinal) do
                root <- Path.GetDirectoryName(root)
                if isNull root then invalidArg "paths" "Packing inputs must share a filesystem root."
                relative <- Path.GetRelativePath(root, path)
        root

    static member EntryName(relative: string, hierarchical: bool) =
        let name = relative.Replace('\\', '/').Normalize(NormalizationForm.FormC)
        if hierarchical then name else name.Substring(name.LastIndexOf('/') + 1)
