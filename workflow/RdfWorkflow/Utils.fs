module Utils

open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Update
open VDS.RDF.Parsing
open VDS.RDF.Shacl
open System.IO
open VDS.RDF.Query.Inference
open Iride

type System.Collections.Generic.IEnumerable<'a> with
    member this.Single = Seq.exactlyOne this

type INode with
    member this.Uri = (this :?> IUriNode).Uri
    member this.Types = 
        let uri = UriFactory.Create RdfSpecsHelper.RdfType
        let typeNode = this.Graph.CreateUriNode uri
        this.Graph.GetTriplesWithSubjectPredicate(this, typeNode) 
        |> Seq.map (fun x -> x.Object.Uri)

type Resource with
    member this.Uri = this.Node.Uri

let parseTurtleFile path =
    let graph = new Graph()
    FileLoader.Load(graph, path)
    graph

let parseTurtleText text =
    let graph = new Graph()
    TurtleParser().Load(graph, new StringReader(text))
    graph :> IGraph

let parseQuery (text:string) =
    SparqlQueryParser().ParseFromString text

let resolve pairs =
    let d = dict pairs
    fun x -> 
        match d.TryGetValue x with
        | true, value -> value
        | _ -> x

let construct (query: SparqlQuery) (input: IGraph) =
    input.ExecuteQuery query :?> IGraph

let ask (query: SparqlQuery) (input: IGraph) =
    (input.ExecuteQuery query :?> SparqlResultSet).Result

let shacl (shapes: ShapesGraph) (input: IGraph) =
    shapes.Validate input
    
let merge (g1: IGraph) (g2: IGraph) =
    g1.Merge g2 
    g1

let infer schemas (input: IGraph) =
    let reasoner = RdfsReasoner()
    if not (Seq.isEmpty schemas)
    then reasoner.Initialise (Seq.reduce merge schemas)
    reasoner.Apply input

let select (query: SparqlQuery) (input: IGraph) =
    input.ExecuteQuery query :?> SparqlResultSet

let remoteUpdate (endpoint: RemoteUpdateProcessor) (commands: SparqlUpdateCommandSet) =
    endpoint.ProcessCommandSet commands

let setArgs variables (sparql: SparqlParameterizedString) (args: SparqlResult) =
    for v in variables do sparql.SetVariable(v, args.[v])
    sparql.ToString()

let getCommands (sparql: SparqlParameterizedString) (args: SparqlResultSet) =
    args.Results 
    |> Seq.map (setArgs args.Variables sparql)
        
