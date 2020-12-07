#if INTERACTIVE
#r "nuget: Iride"
#endif

open System
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Parsing
open Iride
open System.IO

type System.Collections.Generic.IEnumerable<'a> with
    member this.Single = Seq.exactlyOne this

type INode with
    member this.Uri = (this :?> IUriNode).Uri
    member this.Types = 
        let uri = UriFactory.Create "http://www.w3.org/1999/02/22-rdf-syntax-ns#type"
        let typeNode = this.Graph.CreateUriNode uri
        this.Graph.GetTriplesWithSubjectPredicate(this, typeNode) 
        |> Seq.map (fun x -> x.Object.Uri)


let parseTurtle path =
    let graph = new Graph()
    FileLoader.Load(graph, path)
    graph

type W = GraphProvider<Schema = "schema.ttl">

let construct (query: SparqlQuery) (input: IGraph) =
    input.ExecuteQuery(query) :?> IGraph

let ask (query: SparqlQuery) (input: IGraph) =
    (input.ExecuteQuery(query) :?> SparqlResultSet).Result




type State = { 
    Id: int;
    Data: IGraph; 
    Step: W.Step; 
    Result: bool option } 

let exec 
    (configuration: IGraph) 
    (workflow: IGraph)
    (sparqlFactory: string -> SparqlQuery)
    (input: IGraph) =
    
    let information = new Graph()
    information.Merge configuration
    information.Merge input

    let w = W.Process.Get(workflow).Single

    let mutable state = { Id = 0; Data = information; Step = w.StartAt.Single; Result = None }

    while state.Result.IsNone do
        match state.Step.Node.Types.Single.AbsoluteUri with
        | "http://workflow.org/AskStep" ->
            let askStep = W.AskStep state.Step.Node
            if ask (sparqlFactory askStep.Sparql.Single) state.Data
            then state <- { state with Id = state.Id+1; Step = askStep.NextOnTrue.Single }
            else state <- { state with Id = state.Id+1; Step = askStep.NextOnFalse.Single }
        | "http://workflow.org/ConstructStep" ->
            let constructStep = W.ConstructStep state.Step.Node
            let info = construct (sparqlFactory constructStep.Sparql.Single) state.Data
            state.Data.Merge info
            state <- { state with
                        Id = state.Id+1
                        Step = constructStep.Next.Single }
        | "http://workflow.org/DatabaseUpdateStep" -> 
            let databaseUpdateStep = W.DatabaseUpdateStep state.Step.Node
            state <- { state with
                        Id = state.Id+1
                        //Data = construct (sparqlFactory databaseUpdateStep.Sparql.Single) input
                        Step = databaseUpdateStep.Next.Single }
        | "http://workflow.org/FinalStep" ->
            let finalStep = W.FinalStep state.Step.Node
            state <- {state with Id = state.Id+1; Result = Some finalStep.Success.Single }
        | x -> failwith $"Unknown step {x}"
    state
    

let run (configuration: FileInfo) (workflow: FileInfo) (input: FileInfo) =
    let queryFolder = workflow.Directory
    let configuration = parseTurtle configuration.FullName
    let workflow = parseTurtle workflow.FullName
    let input = parseTurtle input.FullName

    let queries = Collections.Generic.Dictionary<_,_>()
    let getQuery fileName =
        match queries.TryGetValue fileName with
        | true, query -> query
        | _ ->
            let query = 
                Path.Combine(queryFolder.FullName, fileName)
                |> SparqlQueryParser().ParseFromFile
            queries.Add(fileName, query)
            query

    exec configuration workflow getQuery input

[<EntryPoint>]
let main argv =
    match argv with
    | [| configuration; workflow; input |] ->
        let c, w, i = (FileInfo configuration), (FileInfo workflow), (FileInfo input)

        let result = run c w i

        printfn "Result %A" result.Result
        let resultName = $"{Path.GetFileNameWithoutExtension(i.Name)}.out{i.Extension}"
        let resultFile = Path.Combine(i.Directory.FullName, resultName)
        result.Data.SaveToFile resultFile
        0
    | _ -> 
        printfn "invalid arguments"
        1
