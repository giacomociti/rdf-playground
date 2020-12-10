open System.IO
open System.Collections.Generic
open VDS.RDF
open VDS.RDF.Parsing
open Utils

let queryFactory (queryFolder: DirectoryInfo) =
    let queries = Dictionary<_,_>()
    let parser = SparqlQueryParser()
    fun fileName ->
        match queries.TryGetValue fileName with
        | true, query -> query
        | _ ->
            let query = 
                Path.Combine(queryFolder.FullName, fileName)
                |> parser.ParseFromFile
            queries.Add(fileName, query)
            query

let runFromFiles (configuration: FileInfo) (workflow: FileInfo) (input: FileInfo) =
    let getQuery = queryFactory workflow.Directory
    let configuration = parseTurtle configuration.FullName
    let workflow = parseTurtle workflow.FullName
    let input = parseTurtle input.FullName
    Workflow.run configuration getQuery workflow input

[<EntryPoint>]
let main argv =
    match argv with
    | [| configuration; workflow; input |] ->
        let c, w, i = (FileInfo configuration), (FileInfo workflow), (FileInfo input)

        let success, information = runFromFiles c w i

        printfn "Result %b" success
        let resultName = $"{Path.GetFileNameWithoutExtension(i.Name)}.out{i.Extension}"
        let resultFile = Path.Combine(i.Directory.FullName, resultName)
        information.SaveToFile resultFile
        0
    | _ -> 
        printfn "invalid arguments"
        1
