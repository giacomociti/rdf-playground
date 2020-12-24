open System.IO
open System.Collections.Generic
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Shacl
open VDS.RDF.Parsing
open Utils
open RdfWorkflow

let factory (queryFolder: DirectoryInfo) =
    let queries = Dictionary<_,_>()
    let parser = SparqlQueryParser()
    { new IFactory with
        member _.CreateQuery fileName =
            match queries.TryGetValue fileName with
            | true, query -> query
            | _ ->
                let query = 
                    Path.Combine(queryFolder.FullName, fileName)
                    |> parser.ParseFromFile
                queries.Add(fileName, query)
                query 
        member _.CreateUpdate fileName =
            Path.Combine(queryFolder.FullName, fileName)
            |> File.ReadAllText
            |> SparqlParameterizedString

        member _.CreateShaclShape fileName =
            let graph = new Graph()
            let file = Path.Combine(queryFolder.FullName, fileName)
            FileLoader.Load(graph, file)
            new ShapesGraph(graph)

    }
let runFromFiles (configuration: FileInfo) (workflow: FileInfo) (input: FileInfo) =
    let steps = Steps([], factory workflow.Directory)
    let configuration = parseTurtleFile configuration.FullName
    let workflow = parseTurtleFile workflow.FullName
    let input = parseTurtleFile input.FullName
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
