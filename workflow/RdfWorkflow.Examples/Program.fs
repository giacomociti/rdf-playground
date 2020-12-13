open System.IO
open System.Collections.Generic
open VDS.RDF
open VDS.RDF.Parsing
open Utils
open RdfWorkflow

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
    let steps = Steps([], queryFactory workflow.Directory)
    let configuration = parseTurtle configuration.FullName
    let workflow = parseTurtle workflow.FullName
    let input = parseTurtle input.FullName
    Workflow(configuration, steps, workflow).Start input

[<EntryPoint>]
let main argv =
    match argv with
    | [| configuration; workflow; input |] ->
        let c, w, i = (FileInfo configuration), (FileInfo workflow), (FileInfo input)

        let res = runFromFiles c w i

        printfn $"{res.Status} <{res.StepUri}> after {res.StepNumber} steps" 
        let resultName = $"{Path.GetFileNameWithoutExtension(i.Name)}.out{i.Extension}"
        let resultFile = Path.Combine(i.Directory.FullName, resultName)
        res.Data.SaveToFile resultFile
        0
    | _ -> 
        printfn "invalid arguments"
        1
